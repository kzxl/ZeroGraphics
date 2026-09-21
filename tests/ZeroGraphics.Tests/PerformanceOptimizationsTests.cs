using System;
using System.Drawing;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.DirectX.Rhi;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;
using ZeroGraphics.Imaging.Gpu;
using ZeroGraphics.Rhi;
using ZeroGraphics.Rhi.Null;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Tests
{
    public class PerformanceOptimizationsTests
    {
        [Fact]
        public unsafe void NccTemplateMatcher_ZeroLOH_MatchesPatternAccurately()
        {
            // Create 128x128 search image with a cross pattern
            using var search = ImageBuffer.CreateGray8(128, 128);
            for (int y = 0; y < 128; y++)
            {
                byte* p = search.GetRowPointer(y);
                for (int x = 0; x < 128; x++)
                {
                    p[x] = (byte)(50 + (x + y) % 30);
                }
            }

            // Draw a high contrast cross at (40, 50)
            int targetX = 40;
            int targetY = 50;
            for (int dx = 0; dx < 16; dx++)
            {
                search.GetRowPointer(targetY + 8)[targetX + dx] = 240;
                search.GetRowPointer(targetY + dx)[targetX + 8] = 240;
            }

            // Create 16x16 template
            using var template = ImageBuffer.CreateGray8(16, 16);
            for (int y = 0; y < 16; y++)
            {
                byte* p = template.GetRowPointer(y);
                for (int x = 0; x < 16; x++)
                {
                    p[x] = 50;
                }
            }
            for (int dx = 0; dx < 16; dx++)
            {
                template.GetRowPointer(8)[dx] = 240;
                template.GetRowPointer(dx)[8] = 240;
            }

            // Match using zero-LOH pooled matcher
            var result = NccTemplateMatcher.Match(search, template, minScore: 0.70, subPixelRefinement: true);

            Assert.True(result.IsFound);
            Assert.True(result.Score > 0.85);
            Assert.InRange(result.X, targetX - 0.5, targetX + 0.5);
            Assert.InRange(result.Y, targetY - 0.5, targetY + 0.5);
        }

        [Fact]
        public unsafe void Thresholding_SimdBranchless_MatchesScalarPixelByPixel()
        {
            const int width = 137; // Non-multiple of 16/32 to test vector + scalar tail
            const int height = 64;

            using var src = ImageBuffer.CreateGray8(width, height);
            using var dstSimd = ImageBuffer.CreateGray8(width, height);
            using var dstReference = ImageBuffer.CreateGray8(width, height);

            // Populate with gradient and noise
            var rnd = new Random(42);
            for (int y = 0; y < height; y++)
            {
                byte* p = src.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    p[x] = (byte)rnd.Next(0, 256);
                }
            }

            byte threshold = 127;

            // Compute Reference Scalar
            for (int y = 0; y < height; y++)
            {
                byte* s = src.GetRowPointer(y);
                byte* d = dstReference.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    d[x] = s[x] >= threshold ? (byte)255 : (byte)0;
                }
            }

            // Compute Optimized (SIMD + branchless)
            Thresholding.ApplyBinaryThreshold(src, dstSimd, threshold, invert: false);

            // Verify bit-exact equality across entire frame
            for (int y = 0; y < height; y++)
            {
                byte* expected = dstReference.GetRowPointer(y);
                byte* actual = dstSimd.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    Assert.Equal(expected[x], actual[x]);
                }
            }
        }

        [Fact]
        public unsafe void AdaptiveThresholdBradley_ZeroLOH_ProducesBinaryOutput()
        {
            using var src = ImageBuffer.CreateGray8(64, 64);
            using var dst = ImageBuffer.CreateGray8(64, 64);

            for (int y = 0; y < 64; y++)
            {
                byte* p = src.GetRowPointer(y);
                for (int x = 0; x < 64; x++)
                {
                    p[x] = (byte)((x < 32) ? 80 : 200);
                }
            }

            // Draw a black mark in both dark and bright sides
            src.GetRowPointer(30)[15] = 10;
            src.GetRowPointer(30)[45] = 100;

            Thresholding.AdaptiveThresholdBradley(src, dst, windowRatio: 0.2f, percentage: 0.15f);

            // Verify both marks are binarized as foreground (0)
            Assert.Equal(0, dst.GetRowPointer(30)[15]);
            Assert.Equal(0, dst.GetRowPointer(30)[45]);

            // And background is white (255)
            Assert.Equal(255, dst.GetRowPointer(10)[10]);
            Assert.Equal(255, dst.GetRowPointer(10)[50]);
        }

        [Fact]
        public void RhiResourceBarrier_TracksStateTransitions()
        {
            using var device = new NullRhiDevice();
            var texDesc = new RhiTextureDesc(64, 64, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget | RhiTextureUsage.ShaderResource);
            using var texture = device.CreateTexture(texDesc);

            using var cmd = device.CreateCommandBuffer();
            cmd.Begin();

            var barrier1 = new RhiBarrier(texture, RhiResourceState.RenderTarget, RhiResourceState.ShaderResource);
            cmd.ResourceBarrier(barrier1);

            var barrier2 = new RhiBarrier(texture, RhiResourceState.ShaderResource, RhiResourceState.CopyDest);
            cmd.ResourceBarriers(new[] { barrier2 });

            cmd.End();

            var nullCmd = (NullRhiCommandBuffer)cmd;
            Assert.Contains(nullCmd.RecordedCommands, s => s.Contains("Barrier") && s.Contains("RenderTarget -> ShaderResource"));
            Assert.Contains(nullCmd.RecordedCommands, s => s.Contains("Barrier") && s.Contains("ShaderResource -> CopyDest"));
        }

        [Fact]
        public unsafe void AsyncStagingRingBuffer_CanBeInstantiatedAndOperated()
        {
            using var rhiDevice = D3D11RhiDevice.GetOrCreateShared();
            var d3dDevice = (D3D11RhiDevice)rhiDevice;
            var device = d3dDevice.NativeDevice;
            var context = d3dDevice.NativeContext;

            using var transfer = new GpuTextureTransfer(device, context);
            using var ring = transfer.CreateAsyncStagingRingBuffer(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, ringSize: 3);

            Assert.Equal(3, ring.Capacity);
            Assert.Equal(64, ring.Width);
            Assert.Equal(64, ring.Height);

            // Create a test texture
            var texDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = 64,
                Height = 64,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
                CPUAccessFlags = 0,
                MiscFlags = 0
            };
            using var gpuTex = device.CreateTexture2D(ref texDesc);

            int ticket = ring.EnqueueCopy(gpuTex);
            Assert.True(ticket >= 0 && ticket < 3);

            using var readbackBuf = ImageBuffer.CreateBgra32(64, 64);
            bool success = ring.TryReadback(ticket, readbackBuf, waitForGpu: true);
            Assert.True(success);
        }
    }
}

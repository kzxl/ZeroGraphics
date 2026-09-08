using System;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Tests
{
    public class GpuImagePipelineTests
    {
        [Fact]
        public void GpuImageContext_InitializationAndResources_Valid()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported)
            {
                return; // Gracefully skip on platforms without D3D11 hardware or WARP
            }

            using (var context = GpuImageContext.CreateDefault())
            {
                Assert.NotNull(context.Device);
                Assert.True(context.Device.IsValid);
                Assert.NotNull(context.ImmediateContext);
                Assert.True(context.ImmediateContext.IsValid);
                Assert.NotNull(context.TexturePool);
                Assert.NotNull(context.Transfer);

                // Verify compiled shaders
                Assert.NotNull(context.VsFullscreen);
                Assert.NotNull(context.PsResize);
                Assert.NotNull(context.PsColorAdjust);
                Assert.NotNull(context.PsGaussianBlur);
                Assert.NotNull(context.PsSobel);
                Assert.NotNull(context.PsSharpen);
                Assert.NotNull(context.PsThreshold);
                Assert.NotNull(context.PsFused);

                // Verify samplers & pipeline state
                Assert.NotNull(context.LinearSampler);
                Assert.NotNull(context.PointSampler);
                Assert.NotNull(context.RasterizerState);
                Assert.NotNull(context.ConstantBuffer);
            }
        }

        [Fact]
        public void GpuTexturePool_RecyclesIntermediateTexturesWithoutNewAllocations()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                var pool = context.TexturePool;
                Assert.Equal(0, pool.TotalAllocatedCount);
                Assert.Equal(0, pool.ActiveLeasedCount);

                // 1. Lease first texture (100x100)
                var tex1 = pool.Acquire(100, 100);
                Assert.Equal(1, pool.TotalAllocatedCount);
                Assert.Equal(1, pool.ActiveLeasedCount);
                Assert.Equal(100, tex1.Width);
                Assert.Equal(100, tex1.Height);

                // 2. Release texture
                pool.Release(tex1);
                Assert.Equal(1, pool.TotalAllocatedCount);
                Assert.Equal(0, pool.ActiveLeasedCount);

                // 3. Lease again with same dimensions -> must recycle tex1 without new VRAM allocation
                var tex2 = pool.Acquire(100, 100);
                Assert.Same(tex1, tex2);
                Assert.Equal(1, pool.TotalAllocatedCount);
                Assert.Equal(1, pool.ActiveLeasedCount);

                // 4. Lease different dimension (200x200) -> allocates new entry
                var tex3 = pool.Acquire(200, 200);
                Assert.NotSame(tex2, tex3);
                Assert.Equal(2, pool.TotalAllocatedCount);
                Assert.Equal(2, pool.ActiveLeasedCount);

                pool.Release(tex2);
                pool.Release(tex3);
                Assert.Equal(2, pool.TotalAllocatedCount);
                Assert.Equal(0, pool.ActiveLeasedCount);
            }
        }

        [Fact]
        public void GpuTextureTransfer_ZeroCopyRoundtrip_PreservesPixels()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            using (var dst = ImageBuffer.CreateBgra32(64, 64))
            {
                // Clear and write landmark pixels
                src.Clear(12, 34, 56, 255);
                unsafe
                {
                    byte* p = src.GetRowPointer(20);
                    // Pixel at (10, 20): B=64, G=128, R=250, A=255
                    p[10 * 4 + 0] = 64;
                    p[10 * 4 + 1] = 128;
                    p[10 * 4 + 2] = 250;
                    p[10 * 4 + 3] = 255;

                    byte* p2 = src.GetRowPointer(40);
                    // Pixel at (30, 40): B=210, G=99, R=5, A=255
                    p2[30 * 4 + 0] = 210;
                    p2[30 * 4 + 1] = 99;
                    p2[30 * 4 + 2] = 5;
                    p2[30 * 4 + 3] = 255;
                }

                // DMA Upload to GPU Texture
                var pooled = context.TexturePool.Acquire(64, 64);
                try
                {
                    context.Transfer.Upload(src, pooled.Texture);

                    // DMA Download from GPU Texture
                    context.Transfer.Download(pooled.Texture, dst);

                    // Validate roundtrip pixel values
                    unsafe
                    {
                        byte* p = dst.GetRowPointer(20);
                        Assert.Equal(64, p[10 * 4 + 0]);
                        Assert.Equal(128, p[10 * 4 + 1]);
                        Assert.Equal(250, p[10 * 4 + 2]);
                        Assert.Equal(255, p[10 * 4 + 3]);

                        byte* p2 = dst.GetRowPointer(40);
                        Assert.Equal(210, p2[30 * 4 + 0]);
                        Assert.Equal(99, p2[30 * 4 + 1]);
                        Assert.Equal(5, p2[30 * 4 + 2]);
                        Assert.Equal(255, p2[30 * 4 + 3]);

                        // Also verify default clear pixel at (0, 0)
                        byte* p0 = dst.GetRowPointer(0);
                        Assert.Equal(56, p0[0]); // B
                        Assert.Equal(34, p0[1]); // G
                        Assert.Equal(12, p0[2]); // R
                        Assert.Equal(255, p0[3]); // A
                    }
                }
                finally
                {
                    context.TexturePool.Release(pooled);
                }
            }
        }

        [Fact]
        public void ImagePipelineBuilder_OperationFusion_FusesCompatiblePasses()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                // Sequence: Resize -> ColorAdjust -> Sharpen -> Threshold
                // All 4 compatible operations can fuse into a single FusedPassNode
                var builder = new ImagePipelineBuilder(context)
                    .AddResize(128, 128)
                    .AddColorAdjust(brightness: 0.1f, contrast: 1.2f)
                    .AddSharpen(0.8f)
                    .AddThreshold(0.5f);

                var pipeline = builder.Compile();

                Assert.Equal(4, pipeline.OriginalPassCount);
                Assert.Equal(1, pipeline.OptimizedPassCount);
                Assert.True(pipeline.WasOperationFused);
            }
        }

        [Fact]
        public void ImagePipeline_EndToEndExecution_TransformsImageCorrectly()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            {
                // Fill source with mid-gray (128, 128, 128)
                src.Clear(128, 128, 128, 255);

                // Build a multi-pass pipeline:
                // 1. Resize to 32x32 + Brightness +0.2 -> Fused Pass 1 (32x32)
                // 2. Gaussian Blur (non-fused kernel) -> Pass 2 (32x32)
                var pipeline = new ImagePipelineBuilder(context)
                    .AddResize(32, 32)
                    .AddColorAdjust(brightness: 0.2f)
                    .AddGaussianBlur(1.0f)
                    .Compile();

                Assert.Equal(3, pipeline.OriginalPassCount);
                Assert.Equal(2, pipeline.OptimizedPassCount); // (Resize+Color) + GaussianBlur
                Assert.True(pipeline.WasOperationFused);

                // Execute end-to-end to CPU destination buffer
                using (var dst = pipeline.Execute(src))
                {
                    Assert.Equal(32, dst.Width);
                    Assert.Equal(32, dst.Height);

                    unsafe
                    {
                        byte* p = dst.GetRowPointer(16);
                        // Due to brightness +0.2 and blur, pixels should be brighter than 128
                        int b = p[16 * 4 + 0];
                        int g = p[16 * 4 + 1];
                        int r = p[16 * 4 + 2];
                        Assert.True(r > 130, $"Expected R > 130, got {r}");
                        Assert.True(g > 130, $"Expected G > 130, got {g}");
                        Assert.True(b > 130, $"Expected B > 130, got {b}");
                    }
                }

                // Execute directly to GPU texture (100% VRAM resident)
                using (var gpuResult = pipeline.ExecuteToGpu(src))
                {
                    Assert.NotNull(gpuResult);
                    Assert.Equal(32, gpuResult.Width);
                    Assert.Equal(32, gpuResult.Height);
                    Assert.NotNull(gpuResult.Texture);
                    Assert.NotNull(gpuResult.Srv);
                    Assert.NotNull(gpuResult.Rtv);
                }
            }
        }
    }
}

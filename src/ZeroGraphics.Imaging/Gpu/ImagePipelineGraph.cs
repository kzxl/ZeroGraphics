using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Base class for a Render Graph pass node.
    /// </summary>
    public readonly struct ImageDimensions
    {
        public int Width { get; }
        public int Height { get; }
        public ImageDimensions(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    public abstract class ImageGraphNode
    {
        public abstract ImageDimensions GetOutputSize(int inputWidth, int inputHeight);
        public abstract void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output);
    }

    /// <summary>
    /// Resizing pass node using bilinear or point interpolation.
    /// </summary>
    public sealed class ResizePassNode : ImageGraphNode
    {
        public int TargetWidth { get; }
        public int TargetHeight { get; }
        public bool Bilinear { get; }

        public ResizePassNode(int targetWidth, int targetHeight, bool bilinear = true)
        {
            if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
            if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            Bilinear = bilinear;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(TargetWidth, TargetHeight);

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsResize);
            ctx.PSSetSamplers(0, Bilinear ? context.LinearSampler : context.PointSampler);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Color adjustment pass node (Brightness, Contrast, BT.709 Grayscale, Invert).
    /// </summary>
    public sealed class ColorPassNode : ImageGraphNode
    {
        public float Brightness { get; }
        public float Contrast { get; }
        public bool Grayscale { get; }
        public bool Invert { get; }

        public ColorPassNode(float brightness = 0.0f, float contrast = 1.0f, bool grayscale = false, bool invert = false)
        {
            Brightness = brightness;
            Contrast = contrast;
            Grayscale = grayscale;
            Invert = invert;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct ColorConstants
        {
            public float Brightness;
            public float Contrast;
            public float Grayscale;
            public float Invert;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new ColorConstants
            {
                Brightness = Brightness,
                Contrast = Contrast,
                Grayscale = Grayscale ? 1.0f : 0.0f,
                Invert = Invert ? 1.0f : 0.0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsColorAdjust);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Separable 1D Gaussian Blur pass node.
    /// Executes Horizontal pass then Vertical pass using a ping-pong intermediate texture.
    /// </summary>
    public sealed class GaussianBlurPassNode : ImageGraphNode
    {
        public float Sigma { get; }

        public GaussianBlurPassNode(float sigma = 1.5f)
        {
            Sigma = Math.Max(0.1f, sigma);
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct BlurConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float DirX;
            public float DirY;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var pool = context.TexturePool;

            // 1. Lease temporary intermediate texture for horizontal pass
            var tempH = pool.Acquire(input.Width, input.Height, input.Format);
            try
            {
                // Pass 1: Horizontal Blur (input -> tempH)
                var cbH = new BlurConstants
                {
                    TexelSizeX = 1.0f / input.Width,
                    TexelSizeY = 1.0f / input.Height,
                    DirX = 1.0f,
                    DirY = 0.0f
                };
                ctx.UpdateSubresource(context.ConstantBuffer, ref cbH);
                ctx.OMSetRenderTargets(tempH.Rtv);
                ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, tempH.Width, tempH.Height));
                ctx.PSSetShader(context.PsGaussianBlur);
                ctx.PSSetSamplers(0, context.LinearSampler);
                ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
                ctx.PSSetShaderResources(0, input.Srv);
                ctx.Draw(3, 0);
                ctx.PSSetShaderResources(0, null!);

                // Pass 2: Vertical Blur (tempH -> output)
                var cbV = new BlurConstants
                {
                    TexelSizeX = 1.0f / input.Width,
                    TexelSizeY = 1.0f / input.Height,
                    DirX = 0.0f,
                    DirY = 1.0f
                };
                ctx.UpdateSubresource(context.ConstantBuffer, ref cbV);
                ctx.OMSetRenderTargets(output.Rtv);
                ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
                ctx.PSSetShader(context.PsGaussianBlur);
                ctx.PSSetShaderResources(0, tempH.Srv);
                ctx.Draw(3, 0);
                ctx.PSSetShaderResources(0, null!);
            }
            finally
            {
                pool.Release(tempH);
            }
        }
    }

    /// <summary>
    /// Laplacian 3x3 Sharpening pass node.
    /// </summary>
    public sealed class SharpenPassNode : ImageGraphNode
    {
        public float Strength { get; }

        public SharpenPassNode(float strength = 1.0f)
        {
            Strength = strength;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct SharpenConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float Strength;
            public float Padding;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new SharpenConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Strength = Strength,
                Padding = 0.0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsSharpen);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Sobel 3x3 Gradient edge detection pass node.
    /// </summary>
    public sealed class SobelPassNode : ImageGraphNode
    {
        public float Multiplier { get; }
        public float Threshold { get; }

        public SobelPassNode(float multiplier = 1.0f, float threshold = 0.0f)
        {
            Multiplier = multiplier;
            Threshold = threshold;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct SobelConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float Multiplier;
            public float Threshold;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new SobelConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Multiplier = Multiplier,
                Threshold = Threshold
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsSobel);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Binary Thresholding pass node.
    /// </summary>
    public sealed class ThresholdPassNode : ImageGraphNode
    {
        public float Cutoff { get; }
        public bool Invert { get; }

        public ThresholdPassNode(float cutoff = 0.5f, bool invert = false)
        {
            Cutoff = cutoff;
            Invert = invert;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct ThresholdConstants
        {
            public float Cutoff;
            public float Invert;
            public float Pad1;
            public float Pad2;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new ThresholdConstants
            {
                Cutoff = Cutoff,
                Invert = Invert ? 1.0f : 0.0f,
                Pad1 = 0.0f,
                Pad2 = 0.0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsThreshold);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Operation Fusion pass node.
    /// Fuses Resize/Sampling + Color/Grayscale + Sharpen + Threshold into a SINGLE GPU draw pass.
    /// </summary>
    public sealed class FusedPassNode : ImageGraphNode
    {
        public int TargetWidth { get; }
        public int TargetHeight { get; }
        public float Brightness { get; }
        public float Contrast { get; }
        public bool Grayscale { get; }
        public bool Invert { get; }
        public float SharpenStrength { get; }
        public float ThresholdCutoff { get; }

        public FusedPassNode(
            int targetWidth,
            int targetHeight,
            float brightness = 0.0f,
            float contrast = 1.0f,
            bool grayscale = false,
            bool invert = false,
            float sharpenStrength = 0.0f,
            float thresholdCutoff = -1.0f)
        {
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            Brightness = brightness;
            Contrast = contrast;
            Grayscale = grayscale;
            Invert = invert;
            SharpenStrength = sharpenStrength;
            ThresholdCutoff = thresholdCutoff;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
        {
            int w = TargetWidth > 0 ? TargetWidth : inputWidth;
            int h = TargetHeight > 0 ? TargetHeight : inputHeight;
            return new ImageDimensions(w, h);
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct FusedConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float Brightness;
            public float Contrast;

            public float Grayscale;
            public float Invert;
            public float SharpenStrength;
            public float ThresholdCutoff;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new FusedConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Brightness = Brightness,
                Contrast = Contrast,
                Grayscale = Grayscale ? 1.0f : 0.0f,
                Invert = Invert ? 1.0f : 0.0f,
                SharpenStrength = SharpenStrength,
                ThresholdCutoff = ThresholdCutoff
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsFused);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Compiled and optimized GPU Image Pipeline ready for execution.
    /// Guarantees zero-allocation steady state and automated ping-pong buffer management.
    /// </summary>
    public sealed class CompiledImagePipeline
    {
        private readonly GpuImageContext _context;
        private readonly IReadOnlyList<ImageGraphNode> _passes;

        public int OriginalPassCount { get; }
        public int OptimizedPassCount => _passes.Count;
        public bool WasOperationFused => OptimizedPassCount < OriginalPassCount;

        public CompiledImagePipeline(GpuImageContext context, IReadOnlyList<ImageGraphNode> passes, int originalPassCount)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passes = passes ?? throw new ArgumentNullException(nameof(passes));
            OriginalPassCount = originalPassCount;
        }

        /// <summary>
        /// Executes the pipeline: uploads CPU ImageBuffer -> executes all GPU passes in VRAM -> downloads back to CPU.
        /// </summary>
        public ImageBuffer Execute(ImageBuffer source, ImageBuffer? destination = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Determine final output dimensions
            int curW = source.Width;
            int curH = source.Height;
            for (int i = 0; i < _passes.Count; i++)
            {
                var nextSize = _passes[i].GetOutputSize(curW, curH);
                curW = nextSize.Width;
                curH = nextSize.Height;
            }

            destination ??= new ImageBuffer(curW, curH, source.Format);

            // Execute on GPU and download
            using (var finalGpuTexture = ExecuteToGpu(source))
            {
                _context.Transfer.Download(finalGpuTexture.Texture, destination);
            }

            return destination;
        }

        /// <summary>
        /// Executes the entire pipeline 100% on GPU and returns the final VRAM texture.
        /// Zero CPU download overhead; optimal for Direct2D or SwapChain on-screen rendering.
        /// Caller is responsible for releasing or disposing the returned PooledGpuTexture.
        /// </summary>
        public PooledGpuTexture ExecuteToGpu(ImageBuffer source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var pool = _context.TexturePool;
            var ctx = _context.ImmediateContext;

            // 1. Common Pipeline State
            ctx.IASetInputLayout(null!);
            ctx.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            ctx.VSSetShader(_context.VsFullscreen);
            ctx.RSSetState(_context.RasterizerState);
            ctx.OMSetBlendState(null);

            // 2. Upload source to initial GPU texture
            var currentGpu = pool.Acquire(source.Width, source.Height);
            _context.Transfer.Upload(source, currentGpu.Texture);

            // If no passes, return uploaded texture directly
            if (_passes.Count == 0)
            {
                return currentGpu;
            }

            // 3. Execute passes with Ping-Pong texture recycling
            int currentW = source.Width;
            int currentH = source.Height;

            for (int i = 0; i < _passes.Count; i++)
            {
                var pass = _passes[i];
                var nextSize = pass.GetOutputSize(currentW, currentH);
                var nextGpu = pool.Acquire(nextSize.Width, nextSize.Height, currentGpu.Format);

                pass.Execute(_context, currentGpu, nextGpu);

                // Release previous texture back to pool
                pool.Release(currentGpu);

                currentGpu = nextGpu;
                currentW = nextSize.Width;
                currentH = nextSize.Height;
            }

            return currentGpu;
        }
    }

    /// <summary>
    /// Fluent builder for constructing and optimizing GPU Image Processing Graphs.
    /// </summary>
    public sealed class ImagePipelineBuilder
    {
        private readonly GpuImageContext _context;
        private readonly List<ImageGraphNode> _nodes = new List<ImageGraphNode>();

        public ImagePipelineBuilder(GpuImageContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ImagePipelineBuilder AddResize(int targetWidth, int targetHeight, bool bilinear = true)
        {
            _nodes.Add(new ResizePassNode(targetWidth, targetHeight, bilinear));
            return this;
        }

        public ImagePipelineBuilder AddColorAdjust(float brightness = 0.0f, float contrast = 1.0f, bool grayscale = false, bool invert = false)
        {
            _nodes.Add(new ColorPassNode(brightness, contrast, grayscale, invert));
            return this;
        }

        public ImagePipelineBuilder AddGaussianBlur(float sigma = 1.5f)
        {
            _nodes.Add(new GaussianBlurPassNode(sigma));
            return this;
        }

        public ImagePipelineBuilder AddSharpen(float strength = 1.0f)
        {
            _nodes.Add(new SharpenPassNode(strength));
            return this;
        }

        public ImagePipelineBuilder AddSobel(float multiplier = 1.0f, float threshold = 0.0f)
        {
            _nodes.Add(new SobelPassNode(multiplier, threshold));
            return this;
        }

        public ImagePipelineBuilder AddThreshold(float cutoff = 0.5f, bool invert = false)
        {
            _nodes.Add(new ThresholdPassNode(cutoff, invert));
            return this;
        }

        public ImagePipelineBuilder AddCustomNode(ImageGraphNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _nodes.Add(node);
            return this;
        }

        /// <summary>
        /// Compiles the graph and executes Operation Fusion optimization.
        /// Merges compatible consecutive passes into single-pass fused kernels.
        /// </summary>
        public CompiledImagePipeline Compile()
        {
            int originalCount = _nodes.Count;
            var optimized = OptimizeNodes(_nodes);
            return new CompiledImagePipeline(_context, optimized, originalCount);
        }

        private static List<ImageGraphNode> OptimizeNodes(List<ImageGraphNode> nodes)
        {
            var result = new List<ImageGraphNode>();
            int i = 0;

            while (i < nodes.Count)
            {
                var current = nodes[i];

                // Check if we can fuse a sequence: Resize? -> ColorAdjust? -> Sharpen? -> Threshold?
                int targetW = -1;
                int targetH = -1;
                float brightness = 0.0f;
                float contrast = 1.0f;
                bool grayscale = false;
                bool invert = false;
                float sharpenStrength = 0.0f;
                float thresholdCutoff = -1.0f;

                int fusedNodesCount = 0;
                int j = i;

                if (j < nodes.Count && nodes[j] is ResizePassNode resize)
                {
                    targetW = resize.TargetWidth;
                    targetH = resize.TargetHeight;
                    j++;
                    fusedNodesCount++;
                }

                if (j < nodes.Count && nodes[j] is ColorPassNode color)
                {
                    brightness = color.Brightness;
                    contrast = color.Contrast;
                    grayscale = color.Grayscale;
                    invert = color.Invert;
                    j++;
                    fusedNodesCount++;
                }

                if (j < nodes.Count && nodes[j] is SharpenPassNode sharpen)
                {
                    sharpenStrength = sharpen.Strength;
                    j++;
                    fusedNodesCount++;
                }

                if (j < nodes.Count && nodes[j] is ThresholdPassNode threshold)
                {
                    thresholdCutoff = threshold.Cutoff;
                    if (threshold.Invert) invert = !invert;
                    j++;
                    fusedNodesCount++;
                }

                // If 2 or more nodes were combined, create a FusedPassNode
                if (fusedNodesCount >= 2)
                {
                    result.Add(new FusedPassNode(
                        targetW, targetH,
                        brightness, contrast, grayscale, invert,
                        sharpenStrength, thresholdCutoff));
                    i = j;
                }
                else
                {
                    result.Add(current);
                    i++;
                }
            }

            return result;
        }
    }
}

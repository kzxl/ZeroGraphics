using System;

namespace ZeroGraphics.Rhi
{
    /// <summary>
    /// Core GPU device and factory interface.
    /// Manages hardware resource allocation and pipeline state compilation.
    /// </summary>
    public interface IRhiDevice : IDisposable
    {
        RhiBackend Backend { get; }
        string DeviceName { get; }

        IRhiBuffer CreateBuffer(in RhiBufferDesc desc, ReadOnlySpan<byte> initialData = default);
        IRhiTexture CreateTexture(in RhiTextureDesc desc, ReadOnlySpan<byte> initialData = default);
        IRhiShader CreateShader(RhiShaderStage stage, byte[] bytecode);
        IRhiPipelineState CreatePipelineState(RhiPipelineStateDesc desc);
        IRhiSwapChain CreateSwapChain(IntPtr windowHandle, int width, int height, RhiPresentMode presentMode = RhiPresentMode.Fifo);
        IRhiCommandBuffer CreateCommandBuffer();
    }

    /// <summary>
    /// GPU memory buffer encapsulation (Vertex, Index, Constant, Staging, Structured).
    /// </summary>
    public interface IRhiBuffer : IDisposable
    {
        RhiBufferType Type { get; }
        RhiBufferUsage Usage { get; }
        int SizeInBytes { get; }

        void UpdateData<T>(ReadOnlySpan<T> data, int offsetBytes = 0) where T : unmanaged;
    }

    /// <summary>
    /// GPU 2D/3D Texture surface encapsulation.
    /// </summary>
    public interface IRhiTexture : IDisposable
    {
        int Width { get; }
        int Height { get; }
        RhiFormat Format { get; }
        RhiTextureUsage Usage { get; }
        int MipLevels { get; }

        void UpdateData<T>(ReadOnlySpan<T> data, int rowPitch) where T : unmanaged;
    }

    /// <summary>
    /// Compiled programmable shader module.
    /// </summary>
    public interface IRhiShader : IDisposable
    {
        RhiShaderStage Stage { get; }
        byte[] Bytecode { get; }
    }

    /// <summary>
    /// Immutable Pipeline State Object (PSO).
    /// </summary>
    public interface IRhiPipelineState : IDisposable
    {
        RhiPipelineStateDesc Description { get; }
    }

    /// <summary>
    /// Window swapchain managing backbuffers and display presentation.
    /// </summary>
    public interface IRhiSwapChain : IDisposable
    {
        int Width { get; }
        int Height { get; }
        IRhiTexture BackBuffer { get; }

        void Resize(int width, int height);
        void Present(int syncInterval = 1);
    }

    /// <summary>
    /// Command recording interface for batch GPU rendering operations.
    /// </summary>
    public interface IRhiCommandBuffer : IDisposable
    {
        void Begin();
        void End();

        void SetViewport(in RhiViewport viewport);
        void SetScissorRect(in RhiRect scissorRect);

        void BeginRenderPass(IRhiTexture renderTarget, RhiClearFlags clearFlags = RhiClearFlags.None, RhiColor clearColor = default);
        void EndRenderPass();

        void SetPipelineState(IRhiPipelineState pipelineState);
        void SetVertexBuffer(int slot, IRhiBuffer buffer, int stride, int offset = 0);
        void SetIndexBuffer(IRhiBuffer buffer, RhiIndexFormat format, int offset = 0);
        void SetConstantBuffer(int slot, IRhiBuffer buffer, RhiShaderStage stage = RhiShaderStage.Vertex | RhiShaderStage.Pixel);
        void SetShaderResource(int slot, IRhiTexture texture, RhiShaderStage stage = RhiShaderStage.Pixel);

        void Draw(int vertexCount, int startVertex = 0);
        void DrawIndexed(int indexCount, int startIndex = 0, int baseVertex = 0);
        void DispatchCompute(int groupCountX, int groupCountY, int groupCountZ);

        void ResourceBarrier(in RhiBarrier barrier);
        void ResourceBarriers(ReadOnlySpan<RhiBarrier> barriers);
    }
}

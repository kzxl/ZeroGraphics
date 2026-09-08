using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.DirectX.Native
{
    /// <summary>
    /// High-performance COM VTable invoker.
    /// Directly dispatches DirectX COM interface methods by slot index without heavy external COM dependencies.
    /// </summary>
    public static unsafe class ComVTableHelper
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int QueryInterfaceDelegate(IntPtr thisPtr, [In] ref Guid riid, out IntPtr ppvObject);

        public static int QueryInterface(IntPtr comPtr, ref Guid riid, out IntPtr ppvObject)
        {
            if (comPtr == IntPtr.Zero)
            {
                ppvObject = IntPtr.Zero;
                return unchecked((int)0x80004003); // E_POINTER
            }
            IntPtr methodPtr = (*(IntPtr**)comPtr)[0];
            return Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(methodPtr)(comPtr, ref riid, out ppvObject);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint ReleaseDelegate(IntPtr thisPtr);

        public static uint Release(IntPtr comPtr)
        {
            if (comPtr == IntPtr.Zero) return 0;
            IntPtr methodPtr = (*(IntPtr**)comPtr)[2];
            return Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(methodPtr)(comPtr);
        }

        // =========================================================================
        // IDXGIDevice1
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int SetMaximumFrameLatencyDelegate(IntPtr thisPtr, uint maxLatency);

        public static int SetMaximumFrameLatency(IntPtr dxgiDevice1, uint maxLatency)
        {
            if (dxgiDevice1 == IntPtr.Zero) return unchecked((int)0x80004003);
            IntPtr methodPtr = (*(IntPtr**)dxgiDevice1)[12];
            return Marshal.GetDelegateForFunctionPointer<SetMaximumFrameLatencyDelegate>(methodPtr)(dxgiDevice1, maxLatency);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetMaximumFrameLatencyDelegate(IntPtr thisPtr, out uint maxLatency);

        public static int GetMaximumFrameLatency(IntPtr dxgiDevice1, out uint maxLatency)
        {
            if (dxgiDevice1 == IntPtr.Zero)
            {
                maxLatency = 0;
                return unchecked((int)0x80004003);
            }
            IntPtr methodPtr = (*(IntPtr**)dxgiDevice1)[13];
            return Marshal.GetDelegateForFunctionPointer<GetMaximumFrameLatencyDelegate>(methodPtr)(dxgiDevice1, out maxLatency);
        }


        // =========================================================================
        // IDXGIFactory / IDXGIFactory1
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateSwapChainDelegate(
            IntPtr thisPtr,
            IntPtr pDevice,
            ref DXGI_SWAP_CHAIN_DESC pDesc,
            out IntPtr ppSwapChain);

        public static int CreateSwapChain(IntPtr factory, IntPtr pDevice, ref DXGI_SWAP_CHAIN_DESC desc, out IntPtr ppSwapChain)
        {
            IntPtr methodPtr = (*(IntPtr**)factory)[10];
            return Marshal.GetDelegateForFunctionPointer<CreateSwapChainDelegate>(methodPtr)(factory, pDevice, ref desc, out ppSwapChain);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int EnumAdapters1Delegate(
            IntPtr thisPtr,
            uint adapterIndex,
            out IntPtr ppAdapter);

        public static int EnumAdapters1(IntPtr factory1, uint adapterIndex, out IntPtr ppAdapter)
        {
            IntPtr methodPtr = (*(IntPtr**)factory1)[12];
            return Marshal.GetDelegateForFunctionPointer<EnumAdapters1Delegate>(methodPtr)(factory1, adapterIndex, out ppAdapter);
        }

        // =========================================================================
        // IDXGIAdapter1
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetDesc1Delegate(
            IntPtr thisPtr,
            out DXGI_ADAPTER_DESC1 pDesc);

        public static int GetDesc1(IntPtr adapter1, out DXGI_ADAPTER_DESC1 desc)
        {
            IntPtr methodPtr = (*(IntPtr**)adapter1)[10];
            return Marshal.GetDelegateForFunctionPointer<GetDesc1Delegate>(methodPtr)(adapter1, out desc);
        }

        // =========================================================================
        // IDXGISwapChain
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int PresentDelegate(IntPtr thisPtr, uint syncInterval, uint flags);

        public static int Present(IntPtr swapChain, uint syncInterval, uint flags)
        {
            IntPtr methodPtr = (*(IntPtr**)swapChain)[8];
            return Marshal.GetDelegateForFunctionPointer<PresentDelegate>(methodPtr)(swapChain, syncInterval, flags);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetBufferDelegate(IntPtr thisPtr, uint bufferIndex, ref Guid riid, out IntPtr ppSurface);

        public static int GetBuffer(IntPtr swapChain, uint bufferIndex, ref Guid riid, out IntPtr ppSurface)
        {
            IntPtr methodPtr = (*(IntPtr**)swapChain)[9];
            return Marshal.GetDelegateForFunctionPointer<GetBufferDelegate>(methodPtr)(swapChain, bufferIndex, ref riid, out ppSurface);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int ResizeBuffersDelegate(
            IntPtr thisPtr,
            uint bufferCount,
            uint width,
            uint height,
            DXGI_FORMAT newFormat,
            uint swapChainFlags);

        public static int ResizeBuffers(IntPtr swapChain, uint bufferCount, uint width, uint height, DXGI_FORMAT newFormat, uint flags)
        {
            IntPtr methodPtr = (*(IntPtr**)swapChain)[13];
            return Marshal.GetDelegateForFunctionPointer<ResizeBuffersDelegate>(methodPtr)(swapChain, bufferCount, width, height, newFormat, flags);
        }

        // =========================================================================
        // ID3D11Device
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateBufferDelegate(
            IntPtr thisPtr,
            ref D3D11_BUFFER_DESC pDesc,
            IntPtr pInitialData,
            out IntPtr ppBuffer);

        public static int CreateBuffer(IntPtr device, ref D3D11_BUFFER_DESC desc, IntPtr initialData, out IntPtr ppBuffer)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[3];
            return Marshal.GetDelegateForFunctionPointer<CreateBufferDelegate>(methodPtr)(device, ref desc, initialData, out ppBuffer);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateTexture2DDelegate(
            IntPtr thisPtr,
            ref D3D11_TEXTURE2D_DESC pDesc,
            IntPtr pInitialData,
            out IntPtr ppTexture2D);

        public static int CreateTexture2D(
            IntPtr device,
            ref D3D11_TEXTURE2D_DESC desc,
            IntPtr initialData,
            out IntPtr ppTexture2D)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[5];
            return Marshal.GetDelegateForFunctionPointer<CreateTexture2DDelegate>(methodPtr)(
                device, ref desc, initialData, out ppTexture2D);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateShaderResourceViewDelegate(
            IntPtr thisPtr,
            IntPtr pResource,
            IntPtr pDesc,
            out IntPtr ppSRView);

        public static int CreateShaderResourceView(IntPtr device, IntPtr resource, IntPtr desc, out IntPtr ppSRView)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[7];
            return Marshal.GetDelegateForFunctionPointer<CreateShaderResourceViewDelegate>(methodPtr)(device, resource, desc, out ppSRView);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateUnorderedAccessViewDelegate(
            IntPtr thisPtr,
            IntPtr pResource,
            IntPtr pDesc,
            out IntPtr ppUAView);

        public static int CreateUnorderedAccessView(IntPtr device, IntPtr resource, IntPtr desc, out IntPtr ppUAView)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[8];
            return Marshal.GetDelegateForFunctionPointer<CreateUnorderedAccessViewDelegate>(methodPtr)(device, resource, desc, out ppUAView);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateRenderTargetViewDelegate(
            IntPtr thisPtr,
            IntPtr pResource,
            IntPtr pDesc,
            out IntPtr ppRTView);

        public static int CreateRenderTargetView(IntPtr device, IntPtr resource, IntPtr desc, out IntPtr ppRtv)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[9];
            return Marshal.GetDelegateForFunctionPointer<CreateRenderTargetViewDelegate>(methodPtr)(device, resource, desc, out ppRtv);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateInputLayoutDelegate(
            IntPtr thisPtr,
            [In] D3D11_INPUT_ELEMENT_DESC[] pInputElementDescs,
            uint numElements,
            IntPtr pShaderBytecodeWithInputSignature,
            UIntPtr bytecodeLength,
            out IntPtr ppInputLayout);

        public static int CreateInputLayout(
            IntPtr device,
            D3D11_INPUT_ELEMENT_DESC[] descs,
            uint numElements,
            IntPtr bytecode,
            UIntPtr bytecodeLength,
            out IntPtr ppInputLayout)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[11];
            return Marshal.GetDelegateForFunctionPointer<CreateInputLayoutDelegate>(methodPtr)(
                device, descs, numElements, bytecode, bytecodeLength, out ppInputLayout);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateVertexShaderDelegate(
            IntPtr thisPtr,
            IntPtr pShaderBytecode,
            UIntPtr bytecodeLength,
            IntPtr pClassLinkage,
            out IntPtr ppVertexShader);

        public static int CreateVertexShader(
            IntPtr device,
            IntPtr bytecode,
            UIntPtr bytecodeLength,
            IntPtr classLinkage,
            out IntPtr ppVertexShader)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[12];
            return Marshal.GetDelegateForFunctionPointer<CreateVertexShaderDelegate>(methodPtr)(
                device, bytecode, bytecodeLength, classLinkage, out ppVertexShader);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreatePixelShaderDelegate(
            IntPtr thisPtr,
            IntPtr pShaderBytecode,
            UIntPtr bytecodeLength,
            IntPtr pClassLinkage,
            out IntPtr ppPixelShader);

        public static int CreatePixelShader(
            IntPtr device,
            IntPtr bytecode,
            UIntPtr bytecodeLength,
            IntPtr classLinkage,
            out IntPtr ppPixelShader)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[15];
            return Marshal.GetDelegateForFunctionPointer<CreatePixelShaderDelegate>(methodPtr)(
                device, bytecode, bytecodeLength, classLinkage, out ppPixelShader);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateComputeShaderDelegate(
            IntPtr thisPtr,
            IntPtr pShaderBytecode,
            UIntPtr bytecodeLength,
            IntPtr pClassLinkage,
            out IntPtr ppComputeShader);

        public static int CreateComputeShader(
            IntPtr device,
            IntPtr bytecode,
            UIntPtr bytecodeLength,
            IntPtr classLinkage,
            out IntPtr ppComputeShader)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[18];
            return Marshal.GetDelegateForFunctionPointer<CreateComputeShaderDelegate>(methodPtr)(
                device, bytecode, bytecodeLength, classLinkage, out ppComputeShader);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateBlendStateDelegate(
            IntPtr thisPtr,
            ref D3D11_BLEND_DESC pBlendStateDesc,
            out IntPtr ppBlendState);

        public static int CreateBlendState(IntPtr device, ref D3D11_BLEND_DESC desc, out IntPtr ppBlendState)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[20];
            return Marshal.GetDelegateForFunctionPointer<CreateBlendStateDelegate>(methodPtr)(device, ref desc, out ppBlendState);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateRasterizerStateDelegate(
            IntPtr thisPtr,
            ref D3D11_RASTERIZER_DESC pRasterizerDesc,
            out IntPtr ppRasterizerState);

        public static int CreateRasterizerState(IntPtr device, ref D3D11_RASTERIZER_DESC desc, out IntPtr ppRasterizerState)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[22];
            return Marshal.GetDelegateForFunctionPointer<CreateRasterizerStateDelegate>(methodPtr)(device, ref desc, out ppRasterizerState);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateSamplerStateDelegate(
            IntPtr thisPtr,
            ref D3D11_SAMPLER_DESC pSamplerDesc,
            out IntPtr ppSamplerState);

        public static int CreateSamplerState(IntPtr device, ref D3D11_SAMPLER_DESC desc, out IntPtr ppSamplerState)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[23];
            return Marshal.GetDelegateForFunctionPointer<CreateSamplerStateDelegate>(methodPtr)(device, ref desc, out ppSamplerState);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateQueryDelegate(
            IntPtr thisPtr,
            ref D3D11_QUERY_DESC pDesc,
            out IntPtr ppQuery);

        public static int CreateQuery(IntPtr device, ref D3D11_QUERY_DESC desc, out IntPtr ppQuery)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[24];
            return Marshal.GetDelegateForFunctionPointer<CreateQueryDelegate>(methodPtr)(device, ref desc, out ppQuery);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetDeviceRemovedReasonDelegate(IntPtr thisPtr);

        public static int GetDeviceRemovedReason(IntPtr device)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[39];
            return Marshal.GetDelegateForFunctionPointer<GetDeviceRemovedReasonDelegate>(methodPtr)(device);
        }

        // =========================================================================
        // ID3D11DeviceContext
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void VSSetConstantBuffersDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numBuffers,
            [In] IntPtr[] ppConstantBuffers);

        public static void VSSetConstantBuffers(IntPtr context, uint startSlot, uint numBuffers, IntPtr[] buffers)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[7];
            Marshal.GetDelegateForFunctionPointer<VSSetConstantBuffersDelegate>(methodPtr)(context, startSlot, numBuffers, buffers);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PSSetShaderResourcesDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numViews,
            [In] IntPtr[] ppShaderResourceViews);

        public static void PSSetShaderResources(IntPtr context, uint startSlot, uint numViews, IntPtr[] views)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[8];
            Marshal.GetDelegateForFunctionPointer<PSSetShaderResourcesDelegate>(methodPtr)(context, startSlot, numViews, views);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PSSetShaderDelegate(
            IntPtr thisPtr,
            IntPtr pPixelShader,
            [In] IntPtr[]? ppClassInstances,
            uint numClassInstances);

        public static void PSSetShader(IntPtr context, IntPtr pixelShader)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[9];
            Marshal.GetDelegateForFunctionPointer<PSSetShaderDelegate>(methodPtr)(context, pixelShader, null, 0);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PSSetSamplersDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numSamplers,
            [In] IntPtr[] ppSamplers);

        public static void PSSetSamplers(IntPtr context, uint startSlot, uint numSamplers, IntPtr[] samplers)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[10];
            Marshal.GetDelegateForFunctionPointer<PSSetSamplersDelegate>(methodPtr)(context, startSlot, numSamplers, samplers);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void VSSetShaderDelegate(
            IntPtr thisPtr,
            IntPtr pVertexShader,
            [In] IntPtr[]? ppClassInstances,
            uint numClassInstances);

        public static void VSSetShader(IntPtr context, IntPtr vertexShader)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[11];
            Marshal.GetDelegateForFunctionPointer<VSSetShaderDelegate>(methodPtr)(context, vertexShader, null, 0);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DrawDelegate(IntPtr thisPtr, uint vertexCount, uint startVertexLocation);

        public static void Draw(IntPtr context, uint vertexCount, uint startVertexLocation)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[13];
            Marshal.GetDelegateForFunctionPointer<DrawDelegate>(methodPtr)(context, vertexCount, startVertexLocation);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int MapDelegate(
            IntPtr thisPtr,
            IntPtr pResource,
            uint subresource,
            D3D11_MAP mapType,
            uint mapFlags,
            out D3D11_MAPPED_SUBRESOURCE pMappedResource);

        public static int Map(IntPtr context, IntPtr resource, uint subresource, D3D11_MAP mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE mapped)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[14];
            return Marshal.GetDelegateForFunctionPointer<MapDelegate>(methodPtr)(context, resource, subresource, mapType, mapFlags, out mapped);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void UnmapDelegate(
            IntPtr thisPtr,
            IntPtr pResource,
            uint subresource);

        public static void Unmap(IntPtr context, IntPtr resource, uint subresource)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[15];
            Marshal.GetDelegateForFunctionPointer<UnmapDelegate>(methodPtr)(context, resource, subresource);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PSSetConstantBuffersDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numBuffers,
            [In] IntPtr[] ppConstantBuffers);

        public static void PSSetConstantBuffers(IntPtr context, uint startSlot, uint numBuffers, IntPtr[] buffers)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[16];
            Marshal.GetDelegateForFunctionPointer<PSSetConstantBuffersDelegate>(methodPtr)(context, startSlot, numBuffers, buffers);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void IASetInputLayoutDelegate(IntPtr thisPtr, IntPtr pInputLayout);

        public static void IASetInputLayout(IntPtr context, IntPtr inputLayout)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[17];
            Marshal.GetDelegateForFunctionPointer<IASetInputLayoutDelegate>(methodPtr)(context, inputLayout);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void IASetVertexBuffersDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numBuffers,
            [In] IntPtr[] ppVertexBuffers,
            [In] uint[] pStrides,
            [In] uint[] pOffsets);

        public static void IASetVertexBuffers(IntPtr context, uint startSlot, uint numBuffers, IntPtr[] buffers, uint[] strides, uint[] offsets)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[18];
            Marshal.GetDelegateForFunctionPointer<IASetVertexBuffersDelegate>(methodPtr)(context, startSlot, numBuffers, buffers, strides, offsets);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DrawInstancedDelegate(
            IntPtr thisPtr,
            uint vertexCountPerInstance,
            uint instanceCount,
            uint startVertexLocation,
            uint startInstanceLocation);

        public static void DrawInstanced(IntPtr context, uint vertexCountPerInstance, uint instanceCount, uint startVertexLocation, uint startInstanceLocation)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[21];
            Marshal.GetDelegateForFunctionPointer<DrawInstancedDelegate>(methodPtr)(context, vertexCountPerInstance, instanceCount, startVertexLocation, startInstanceLocation);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void IASetPrimitiveTopologyDelegate(IntPtr thisPtr, D3D11_PRIMITIVE_TOPOLOGY topology);

        public static void IASetPrimitiveTopology(IntPtr context, D3D11_PRIMITIVE_TOPOLOGY topology)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[24];
            Marshal.GetDelegateForFunctionPointer<IASetPrimitiveTopologyDelegate>(methodPtr)(context, topology);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void OMSetRenderTargetsDelegate(
            IntPtr thisPtr,
            uint numViews,
            [In] IntPtr[] ppRenderTargetViews,
            IntPtr pDepthStencilView);

        public static void OMSetRenderTargets(IntPtr context, uint numViews, IntPtr[] rtv, IntPtr dsv)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[33];
            Marshal.GetDelegateForFunctionPointer<OMSetRenderTargetsDelegate>(methodPtr)(context, numViews, rtv, dsv);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void OMSetBlendStateDelegate(
            IntPtr thisPtr,
            IntPtr pBlendState,
            [In] float[]? blendFactor,
            uint sampleMask);

        public static void OMSetBlendState(IntPtr context, IntPtr blendState, float[]? blendFactor, uint sampleMask = 0xFFFFFFFF)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[35];
            Marshal.GetDelegateForFunctionPointer<OMSetBlendStateDelegate>(methodPtr)(context, blendState, blendFactor, sampleMask);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RSSetStateDelegate(IntPtr thisPtr, IntPtr pRasterizerState);

        public static void RSSetState(IntPtr context, IntPtr rasterizerState)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[43];
            Marshal.GetDelegateForFunctionPointer<RSSetStateDelegate>(methodPtr)(context, rasterizerState);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RSSetViewportsDelegate(
            IntPtr thisPtr,
            uint numViewports,
            [In] D3D11_VIEWPORT[] pViewports);

        public static void RSSetViewports(IntPtr context, uint numViewports, D3D11_VIEWPORT[] viewports)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[44];
            Marshal.GetDelegateForFunctionPointer<RSSetViewportsDelegate>(methodPtr)(context, numViewports, viewports);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RSSetScissorRectsDelegate(
            IntPtr thisPtr,
            uint numRects,
            [In] D3D11_RECT[]? pRects);

        public static void RSSetScissorRects(IntPtr context, uint numRects, D3D11_RECT[]? rects)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[45];
            Marshal.GetDelegateForFunctionPointer<RSSetScissorRectsDelegate>(methodPtr)(context, numRects, rects);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void CopyResourceDelegate(
            IntPtr thisPtr,
            IntPtr pDstResource,
            IntPtr pSrcResource);

        public static void CopyResource(IntPtr context, IntPtr dstResource, IntPtr srcResource)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[47];
            Marshal.GetDelegateForFunctionPointer<CopyResourceDelegate>(methodPtr)(context, dstResource, srcResource);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void UpdateSubresourceDelegate(
            IntPtr thisPtr,
            IntPtr pDstResource,
            uint dstSubresource,
            IntPtr pDstBox,
            IntPtr pSrcData,
            uint srcRowPitch,
            uint srcDepthPitch);

        public static void UpdateSubresource(IntPtr context, IntPtr dstResource, uint dstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint srcRowPitch, uint srcDepthPitch)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[48];
            Marshal.GetDelegateForFunctionPointer<UpdateSubresourceDelegate>(methodPtr)(context, dstResource, dstSubresource, pDstBox, pSrcData, srcRowPitch, srcDepthPitch);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ClearRenderTargetViewDelegate(
            IntPtr thisPtr,
            IntPtr pRenderTargetView,
            [In] float[] colorRGBA);

        public static void ClearRenderTargetView(IntPtr context, IntPtr rtv, float[] colorRGBA)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[50];
            Marshal.GetDelegateForFunctionPointer<ClearRenderTargetViewDelegate>(methodPtr)(context, rtv, colorRGBA);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void FlushDelegate(IntPtr thisPtr);

        public static void Flush(IntPtr context)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[111];
            Marshal.GetDelegateForFunctionPointer<FlushDelegate>(methodPtr)(context);
        }

        // =========================================================================
        // DirectCompute 5.0 (ID3D11DeviceContext)
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DispatchDelegate(IntPtr thisPtr, uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ);

        public static void Dispatch(IntPtr context, uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[41];
            Marshal.GetDelegateForFunctionPointer<DispatchDelegate>(methodPtr)(context, threadGroupCountX, threadGroupCountY, threadGroupCountZ);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void CSSetShaderResourcesDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numViews,
            [In] IntPtr[] ppShaderResourceViews);

        public static void CSSetShaderResources(IntPtr context, uint startSlot, uint numViews, IntPtr[] views)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[67];
            Marshal.GetDelegateForFunctionPointer<CSSetShaderResourcesDelegate>(methodPtr)(context, startSlot, numViews, views);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void CSSetUnorderedAccessViewsDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numUAVs,
            [In] IntPtr[] ppUnorderedAccessViews,
            [In] uint[]? pUAVInitialCounts);

        public static void CSSetUnorderedAccessViews(IntPtr context, uint startSlot, uint numUAVs, IntPtr[] uavs, uint[]? initialCounts)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[68];
            Marshal.GetDelegateForFunctionPointer<CSSetUnorderedAccessViewsDelegate>(methodPtr)(context, startSlot, numUAVs, uavs, initialCounts);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void CSSetShaderDelegate(
            IntPtr thisPtr,
            IntPtr pComputeShader,
            [In] IntPtr[]? ppClassInstances,
            uint numClassInstances);

        public static void CSSetShader(IntPtr context, IntPtr computeShader)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[69];
            Marshal.GetDelegateForFunctionPointer<CSSetShaderDelegate>(methodPtr)(context, computeShader, null, 0);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void CSSetSamplersDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numSamplers,
            [In] IntPtr[] ppSamplers);

        public static void CSSetSamplers(IntPtr context, uint startSlot, uint numSamplers, IntPtr[] samplers)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[70];
            Marshal.GetDelegateForFunctionPointer<CSSetSamplersDelegate>(methodPtr)(context, startSlot, numSamplers, samplers);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void CSSetConstantBuffersDelegate(
            IntPtr thisPtr,
            uint startSlot,
            uint numBuffers,
            [In] IntPtr[] ppConstantBuffers);

        public static void CSSetConstantBuffers(IntPtr context, uint startSlot, uint numBuffers, IntPtr[] buffers)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[71];
            Marshal.GetDelegateForFunctionPointer<CSSetConstantBuffersDelegate>(methodPtr)(context, startSlot, numBuffers, buffers);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void BeginDelegate(IntPtr thisPtr, IntPtr pAsync);

        public static void Begin(IntPtr context, IntPtr asyncObj)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[27];
            Marshal.GetDelegateForFunctionPointer<BeginDelegate>(methodPtr)(context, asyncObj);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void EndDelegate(IntPtr thisPtr, IntPtr pAsync);

        public static void End(IntPtr context, IntPtr asyncObj)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[28];
            Marshal.GetDelegateForFunctionPointer<EndDelegate>(methodPtr)(context, asyncObj);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetDataDelegate(
            IntPtr thisPtr,
            IntPtr pAsync,
            IntPtr pData,
            uint dataSize,
            uint getDataFlags);

        public static int GetData(IntPtr context, IntPtr asyncObj, IntPtr pData, uint dataSize, uint getDataFlags)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[29];
            return Marshal.GetDelegateForFunctionPointer<GetDataDelegate>(methodPtr)(context, asyncObj, pData, dataSize, getDataFlags);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateDeferredContextDelegate(
            IntPtr thisPtr,
            uint contextFlags,
            out IntPtr ppDeferredContext);

        public static int CreateDeferredContext(IntPtr device, uint contextFlags, out IntPtr ppDeferredContext)
        {
            IntPtr methodPtr = (*(IntPtr**)device)[27];
            return Marshal.GetDelegateForFunctionPointer<CreateDeferredContextDelegate>(methodPtr)(device, contextFlags, out ppDeferredContext);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int FinishCommandListDelegate(
            IntPtr thisPtr,
            int restoreDeferredContextState,
            out IntPtr ppCommandList);

        public static int FinishCommandList(IntPtr context, int restoreDeferredContextState, out IntPtr ppCommandList)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[114];
            return Marshal.GetDelegateForFunctionPointer<FinishCommandListDelegate>(methodPtr)(context, restoreDeferredContextState, out ppCommandList);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ExecuteCommandListDelegate(
            IntPtr thisPtr,
            IntPtr pCommandList,
            int restoreContextState);

        public static void ExecuteCommandList(IntPtr context, IntPtr pCommandList, int restoreContextState)
        {
            IntPtr methodPtr = (*(IntPtr**)context)[115];
            Marshal.GetDelegateForFunctionPointer<ExecuteCommandListDelegate>(methodPtr)(context, pCommandList, restoreContextState);
        }
    }
}

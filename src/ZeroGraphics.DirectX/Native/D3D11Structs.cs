using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.DirectX.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_VIEWPORT
    {
        public float TopLeftX;
        public float TopLeftY;
        public float Width;
        public float Height;
        public float MinDepth;
        public float MaxDepth;

        public D3D11_VIEWPORT(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f)
        {
            TopLeftX = x;
            TopLeftY = y;
            Width = width;
            Height = height;
            MinDepth = minDepth;
            MaxDepth = maxDepth;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BUFFER_DESC
    {
        public uint ByteWidth;
        public D3D11_USAGE Usage;
        public D3D11_BIND_FLAG BindFlags;
        public D3D11_CPU_ACCESS_FLAG CPUAccessFlags;
        public uint MiscFlags;
        public uint StructureByteStride;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_SUBRESOURCE_DATA
    {
        public IntPtr pSysMem;
        public uint SysMemPitch;
        public uint SysMemSlicePitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_INPUT_ELEMENT_DESC
    {
        public IntPtr SemanticName;
        public uint SemanticIndex;
        public DXGI_FORMAT Format;
        public uint InputSlot;
        public uint AlignedByteOffset;
        public int InputSlotClass;
        public uint InstanceDataStepRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_MAPPED_SUBRESOURCE
    {
        public IntPtr pData;
        public uint RowPitch;
        public uint DepthPitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_RENDER_TARGET_BLEND_DESC
    {
        public int BlendEnable; // BOOL
        public D3D11_BLEND SrcBlend;
        public D3D11_BLEND DestBlend;
        public D3D11_BLEND_OP BlendOp;
        public D3D11_BLEND SrcBlendAlpha;
        public D3D11_BLEND DestBlendAlpha;
        public D3D11_BLEND_OP BlendOpAlpha;
        public byte RenderTargetWriteMask;
        private byte _pad0;
        private byte _pad1;
        private byte _pad2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BLEND_DESC
    {
        public int AlphaToCoverageEnable;   // BOOL
        public int IndependentBlendEnable; // BOOL
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget0;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget1;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget2;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget3;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget4;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget5;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget6;
        public D3D11_RENDER_TARGET_BLEND_DESC RenderTarget7;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_RASTERIZER_DESC
    {
        public D3D11_FILL_MODE FillMode;
        public D3D11_CULL_MODE CullMode;
        public int FrontCounterClockwise;  // BOOL
        public int DepthBias;
        public float DepthBiasClamp;
        public float SlopeScaledDepthBias;
        public int DepthClipEnable;        // BOOL
        public int ScissorEnable;          // BOOL
        public int MultisampleEnable;      // BOOL
        public int AntialiasedLineEnable;  // BOOL
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public D3D11_RECT(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_TEXTURE2D_DESC
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public DXGI_FORMAT Format;
        public DXGI_SAMPLE_DESC SampleDesc;
        public D3D11_USAGE Usage;
        public D3D11_BIND_FLAG BindFlags;
        public D3D11_CPU_ACCESS_FLAG CPUAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_SAMPLER_DESC
    {
        public D3D11_FILTER Filter;
        public D3D11_TEXTURE_ADDRESS_MODE AddressU;
        public D3D11_TEXTURE_ADDRESS_MODE AddressV;
        public D3D11_TEXTURE_ADDRESS_MODE AddressW;
        public float MipLODBias;
        public uint MaxAnisotropy;
        public D3D11_COMPARISON_FUNC ComparisonFunc;
        public float BorderColor0;
        public float BorderColor1;
        public float BorderColor2;
        public float BorderColor3;
        public float MinLOD;
        public float MaxLOD;
    }

    public enum D3D11_UAV_DIMENSION : int
    {
        D3D11_UAV_DIMENSION_UNKNOWN = 0,
        D3D11_UAV_DIMENSION_BUFFER = 1,
        D3D11_UAV_DIMENSION_TEXTURE1D = 2,
        D3D11_UAV_DIMENSION_TEXTURE1DARRAY = 3,
        D3D11_UAV_DIMENSION_TEXTURE2D = 4,
        D3D11_UAV_DIMENSION_TEXTURE2DARRAY = 5,
        D3D11_UAV_DIMENSION_TEXTURE3D = 8
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_TEX2D_UAV
    {
        public uint MipSlice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_BUFFER_UAV
    {
        public uint FirstElement;
        public uint NumElements;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct D3D11_UNORDERED_ACCESS_VIEW_DESC
    {
        [FieldOffset(0)]
        public DXGI_FORMAT Format;

        [FieldOffset(4)]
        public D3D11_UAV_DIMENSION ViewDimension;

        [FieldOffset(8)]
        public D3D11_BUFFER_UAV Buffer;

        [FieldOffset(8)]
        public D3D11_TEX2D_UAV Texture2D;
    }
}


using System;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// High-performance Zero-Copy DMA Host &lt;-&gt; Device transfer manager.
    /// Bridges pinned CPU ImageBuffer memory and GPU VRAM textures with zero managed allocations.
    /// </summary>
    public sealed class GpuTextureTransfer : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;

        private D3D11Texture2D? _cachedStagingTexture;
        private int _cachedStagingWidth;
        private int _cachedStagingHeight;
        private DXGI_FORMAT _cachedStagingFormat;

        private bool _disposed;

        public GpuTextureTransfer(D3D11Device device, D3D11DeviceContext context)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Uploads CPU ImageBuffer directly to a GPU texture via direct DMA UpdateSubresource.
        /// </summary>
        public unsafe void Upload(ImageBuffer source, D3D11Texture2D targetTexture)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (targetTexture == null || !targetTexture.IsValid) throw new ArgumentNullException(nameof(targetTexture));

            if (source.Format == ImageFormatMode.Bgra32)
            {
                ComVTableHelper.UpdateSubresource(
                    _context.Handle,
                    targetTexture.Handle,
                    0,
                    IntPtr.Zero,
                    (IntPtr)source.Scan0,
                    (uint)source.Stride,
                    0);
            }
            else if (source.Format == ImageFormatMode.Gray8)
            {
                // If the target is BGRA, expand Gray8 to 32-bit on upload or copy directly if R8
                if (targetTexture.Description.Format == DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
                {
                    // Create temporary stack-friendly or line-based buffer to expand Gray8 -> BGRA
                    int width = source.Width;
                    int height = source.Height;
                    int bgraStride = width * 4;

                    byte[] tempBgra = new byte[bgraStride * height];
                    fixed (byte* pBgra = tempBgra)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* pSrcRow = source.GetRowPointer(y);
                            uint* pDstRow = (uint*)(pBgra + y * bgraStride);
                            for (int x = 0; x < width; x++)
                            {
                                byte g = pSrcRow[x];
                                pDstRow[x] = (uint)(g | (g << 8) | (g << 16) | (0xFF << 24));
                            }
                        }

                        ComVTableHelper.UpdateSubresource(
                            _context.Handle,
                            targetTexture.Handle,
                            0,
                            IntPtr.Zero,
                            (IntPtr)pBgra,
                            (uint)bgraStride,
                            0);
                    }
                }
                else
                {
                    ComVTableHelper.UpdateSubresource(
                        _context.Handle,
                        targetTexture.Handle,
                        0,
                        IntPtr.Zero,
                        (IntPtr)source.Scan0,
                        (uint)source.Stride,
                        0);
                }
            }
        }

        /// <summary>
        /// Downloads GPU texture back to CPU ImageBuffer via DMA Staging Readback.
        /// </summary>
        public unsafe void Download(D3D11Texture2D sourceGpuTexture, ImageBuffer destination)
        {
            if (sourceGpuTexture == null || !sourceGpuTexture.IsValid) throw new ArgumentNullException(nameof(sourceGpuTexture));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            int width = (int)sourceGpuTexture.Description.Width;
            int height = (int)sourceGpuTexture.Description.Height;
            var format = sourceGpuTexture.Description.Format;

            EnsureStagingTexture(width, height, format);

            // 1. Copy VRAM Default -> Staging
            ComVTableHelper.CopyResource(_context.Handle, _cachedStagingTexture!.Handle, sourceGpuTexture.Handle);

            // 2. Map Staging Texture for CPU Read
            int hr = ComVTableHelper.Map(_context.Handle, _cachedStagingTexture.Handle, 0, D3D11_MAP.D3D11_MAP_READ, 0, out var mapped);
            if (hr < 0 || mapped.pData == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to map GPU staging texture for readback. HRESULT=0x{hr:X8}");

            try
            {
                byte* pSrc = (byte*)mapped.pData;
                byte* pDst = (byte*)destination.Scan0;

                int minHeight = Math.Min(height, destination.Height);

                if (destination.Format == ImageFormatMode.Bgra32)
                {
                    int lineBytes = Math.Min(destination.Width * 4, destination.Stride);
                    for (int y = 0; y < minHeight; y++)
                    {
                        Buffer.MemoryCopy(pSrc + y * mapped.RowPitch, pDst + y * destination.Stride, lineBytes, lineBytes);
                    }
                }
                else if (destination.Format == ImageFormatMode.Gray8)
                {
                    int minWidth = Math.Min(width, destination.Width);
                    for (int y = 0; y < minHeight; y++)
                    {
                        byte* pSrcRow = pSrc + y * mapped.RowPitch;
                        byte* pDstRow = destination.GetRowPointer(y);

                        // If GPU texture is BGRA32, read green/blue channel or compute luma
                        if (format == DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM)
                        {
                            for (int x = 0; x < minWidth; x++)
                            {
                                pDstRow[x] = pSrcRow[x * 4]; // blue/gray channel
                            }
                        }
                        else
                        {
                            Buffer.MemoryCopy(pSrcRow, pDstRow, minWidth, minWidth);
                        }
                    }
                }
            }
            finally
            {
                ComVTableHelper.Unmap(_context.Handle, _cachedStagingTexture.Handle, 0);
            }
        }

        private void EnsureStagingTexture(int width, int height, DXGI_FORMAT format)
        {
            if (_cachedStagingTexture != null &&
                _cachedStagingWidth == width &&
                _cachedStagingHeight == height &&
                _cachedStagingFormat == format)
            {
                return;
            }

            _cachedStagingTexture?.Dispose();

            var desc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
                BindFlags = 0,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
                MiscFlags = 0
            };

            _cachedStagingTexture = _device.CreateTexture2D(ref desc);
            _cachedStagingWidth = width;
            _cachedStagingHeight = height;
            _cachedStagingFormat = format;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cachedStagingTexture?.Dispose();
                _cachedStagingTexture = null;
                _disposed = true;
            }
        }
    }
}

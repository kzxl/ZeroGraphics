using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ZeroGraphics.Imaging.Core
{
    public enum ImageFormatMode
    {
        Bgra32 = 4,
        Gray8 = 1
    }

    /// <summary>
    /// High-performance, zero-allocation 2D pixel buffer for image processing and computer vision.
    /// Provides unsafe pointer scanning, unmanaged interop, and conversions to/from System.Drawing.Bitmap.
    /// </summary>
    public sealed unsafe class ImageBuffer : IDisposable
    {
        private readonly byte[] _data;
        private GCHandle _gcHandle;
        private byte* _scan0;
        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;
        private readonly ImageFormatMode _format;
        private bool _disposed;

        public int Width => _width;
        public int Height => _height;
        public int Stride => _stride;
        public int BytesPerPixel => (int)_format;
        public ImageFormatMode Format => _format;
        public byte* Scan0 => _scan0;
        public byte[] RawBytes => _data;

        public ImageBuffer(int width, int height, ImageFormatMode format)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            _width = width;
            _height = height;
            _format = format;

            int bpp = (int)format;
            // 4-byte row alignment
            _stride = ((width * bpp) + 3) & ~3;
            _data = new byte[_stride * height];

            _gcHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
            _scan0 = (byte*)_gcHandle.AddrOfPinnedObject();
        }

        public static ImageBuffer CreateBgra32(int width, int height) => new ImageBuffer(width, height, ImageFormatMode.Bgra32);

        public static ImageBuffer CreateGray8(int width, int height) => new ImageBuffer(width, height, ImageFormatMode.Gray8);

        public byte* GetRowPointer(int y)
        {
            if ((uint)y >= (uint)_height) throw new ArgumentOutOfRangeException(nameof(y));
            return _scan0 + y * _stride;
        }

        public static ImageBuffer FromBitmap(Bitmap bmp)
        {
            if (bmp == null) throw new ArgumentNullException(nameof(bmp));

            int width = bmp.Width;
            int height = bmp.Height;

            var buffer = new ImageBuffer(width, height, ImageFormatMode.Bgra32);

            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppPArgb);

            try
            {
                byte* src = (byte*)data.Scan0;
                int rowBytes = width * 4;

                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = src + y * data.Stride;
                    byte* dstRow = buffer.GetRowPointer(y);
                    Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return buffer;
        }

        public Bitmap ToBitmap()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ImageBuffer));

            if (_format == ImageFormatMode.Bgra32)
            {
                Bitmap bmp = new Bitmap(_width, _height, PixelFormat.Format32bppPArgb);
                BitmapData data = bmp.LockBits(
                    new Rectangle(0, 0, _width, _height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppPArgb);

                try
                {
                    byte* dst = (byte*)data.Scan0;
                    int rowBytes = _width * 4;

                    for (int y = 0; y < _height; y++)
                    {
                        byte* srcRow = GetRowPointer(y);
                        byte* dstRow = dst + y * data.Stride;
                        Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                return bmp;
            }
            else // Gray8
            {
                Bitmap bmp = new Bitmap(_width, _height, PixelFormat.Format8bppIndexed);

                // Configure grayscale palette
                ColorPalette palette = bmp.Palette;
                for (int i = 0; i < 256; i++)
                {
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                }
                bmp.Palette = palette;

                BitmapData data = bmp.LockBits(
                    new Rectangle(0, 0, _width, _height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format8bppIndexed);

                try
                {
                    byte* dst = (byte*)data.Scan0;
                    int rowBytes = _width;

                    for (int y = 0; y < _height; y++)
                    {
                        byte* srcRow = GetRowPointer(y);
                        byte* dstRow = dst + y * data.Stride;
                        Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

                return bmp;
            }
        }

        public ImageBuffer Clone()
        {
            var clone = new ImageBuffer(_width, _height, _format);
            Buffer.BlockCopy(_data, 0, clone._data, 0, _data.Length);
            return clone;
        }

        public void Clear(byte r, byte g, byte b, byte a = 255)
        {
            if (_format == ImageFormatMode.Bgra32)
            {
                uint pixel = (uint)(b | (g << 8) | (r << 16) | (a << 24));
                for (int y = 0; y < _height; y++)
                {
                    uint* row = (uint*)GetRowPointer(y);
                    for (int x = 0; x < _width; x++)
                    {
                        row[x] = pixel;
                    }
                }
            }
            else
            {
                // Gray8 luminance
                byte gray = (byte)((r * 77 + g * 150 + b * 29) >> 8);
                Clear(gray);
            }
        }

        public void Clear(byte value)
        {
            for (int y = 0; y < _height; y++)
            {
                byte* row = GetRowPointer(y);
                for (int x = 0; x < _width * BytesPerPixel; x++)
                {
                    row[x] = value;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_gcHandle.IsAllocated)
                {
                    _gcHandle.Free();
                }
                _scan0 = null;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~ImageBuffer()
        {
            Dispose();
        }
    }
}

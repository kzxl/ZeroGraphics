using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;

namespace ZeroGraphics.Vision.Edge
{
    /// <summary>
    /// High-performance Canny Edge Detector with Gaussian smoothing, Sobel gradients,
    /// 4-direction non-maximum suppression, and dual-threshold hysteresis tracking.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public static class CannyEdgeDetector
    {
        private const byte NonEdge = 0;
        private const byte WeakEdge = 128;
        private const byte StrongEdge = 255;

        /// <summary>
        /// Detects edges in an image using the Canny edge detection algorithm.
        /// </summary>
        /// <param name="image">Source image (Gray8 or Bgra32).</param>
        /// <param name="lowThreshold">Hysteresis lower threshold.</param>
        /// <param name="highThreshold">Hysteresis upper threshold.</param>
        /// <param name="applyGaussian">Whether to apply a 5x5 Gaussian noise smoothing filter.</param>
        /// <returns>A binary Gray8 ImageBuffer where edge pixels are 255 and background is 0.</returns>
        public static unsafe ImageBuffer Detect(
            ImageBuffer image,
            double lowThreshold = 20.0,
            double highThreshold = 50.0,
            bool applyGaussian = true)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (lowThreshold < 0) throw new ArgumentOutOfRangeException(nameof(lowThreshold));
            if (highThreshold < lowThreshold) throw new ArgumentException("highThreshold must be >= lowThreshold.");

            int w = image.Width;
            int h = image.Height;

            // 1. Ensure Grayscale image
            ImageBuffer? grayOwned = null;
            ImageBuffer grayImage = image;
            if (image.Format != ImageFormatMode.Gray8)
            {
                grayOwned = ImageBuffer.CreateGray8(w, h);
                ColorTransform.ToGrayscale(image, grayOwned);
                grayImage = grayOwned;
            }

            try
            {
                // 2. Gaussian smoothing (5x5 binomial separable [1, 4, 6, 4, 1] / 16)
                byte[] smoothed = new byte[w * h];
                if (applyGaussian && w >= 5 && h >= 5)
                {
                    ApplyGaussian5x5(grayImage, smoothed, w, h);
                }
                else
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* pRow = grayImage.GetRowPointer(y);
                        for (int x = 0; x < w; x++)
                        {
                            smoothed[y * w + x] = pRow[x];
                        }
                    }
                }

                // 3. Sobel Gradients (Gx, Gy) & Magnitude & Sector Orientation
                float[] magnitude = new float[w * h];
                byte[] orientation = new byte[w * h]; // 0: 0 deg, 1: 45 deg, 2: 90 deg, 3: 135 deg

                fixed (byte* pSmooth = smoothed)
                fixed (float* pMag = magnitude)
                fixed (byte* pOri = orientation)
                {
                    for (int y = 1; y < h - 1; y++)
                    {
                        int yPrev = (y - 1) * w;
                        int yCur = y * w;
                        int yNext = (y + 1) * w;

                        for (int x = 1; x < w - 1; x++)
                        {
                            // Sobel X kernel:
                            // -1  0  1
                            // -2  0  2
                            // -1  0  1
                            int gx = (pSmooth[yPrev + (x + 1)] - pSmooth[yPrev + (x - 1)]) +
                                     2 * (pSmooth[yCur + (x + 1)] - pSmooth[yCur + (x - 1)]) +
                                     (pSmooth[yNext + (x + 1)] - pSmooth[yNext + (x - 1)]);

                            // Sobel Y kernel:
                            // -1 -2 -1
                            //  0  0  0
                            //  1  2  1
                            int gy = (pSmooth[yNext + (x - 1)] - pSmooth[yPrev + (x - 1)]) +
                                     2 * (pSmooth[yNext + x] - pSmooth[yPrev + x]) +
                                     (pSmooth[yNext + (x + 1)] - pSmooth[yPrev + (x + 1)]);

                            float mag = (float)Math.Sqrt(gx * gx + gy * gy);
                            int idx = yCur + x;
                            pMag[idx] = mag;

                            // Angle sector calculation (-pi to pi)
                            double angle = Math.Atan2(gy, gx) * (180.0 / Math.PI);
                            if (angle < 0) angle += 180.0;

                            // 0 deg: [0, 22.5) or [157.5, 180]
                            // 45 deg: [22.5, 67.5)
                            // 90 deg: [67.5, 112.5)
                            // 135 deg: [112.5, 157.5)
                            if (angle < 22.5 || angle >= 157.5)
                                pOri[idx] = 0;
                            else if (angle < 67.5)
                                pOri[idx] = 1;
                            else if (angle < 112.5)
                                pOri[idx] = 2;
                            else
                                pOri[idx] = 3;
                        }
                    }
                }

                // 4. Non-Maximum Suppression (NMS)
                byte[] nms = new byte[w * h];
                float lowThreshF = (float)lowThreshold;
                float highThreshF = (float)highThreshold;

                fixed (float* pMag = magnitude)
                fixed (byte* pOri = orientation)
                fixed (byte* pNms = nms)
                {
                    for (int y = 1; y < h - 1; y++)
                    {
                        int yCur = y * w;
                        int yPrev = (y - 1) * w;
                        int yNext = (y + 1) * w;

                        for (int x = 1; x < w - 1; x++)
                        {
                            int idx = yCur + x;
                            float m = pMag[idx];

                            if (m < lowThreshF)
                                continue;

                            float m1 = 0, m2 = 0;
                            switch (pOri[idx])
                            {
                                case 0: // Horizontal -> compare (x-1, y) and (x+1, y)
                                    m1 = pMag[yCur + (x - 1)];
                                    m2 = pMag[yCur + (x + 1)];
                                    break;
                                case 1: // 45 deg -> compare (x+1, y-1) and (x-1, y+1)
                                    m1 = pMag[yPrev + (x + 1)];
                                    m2 = pMag[yNext + (x - 1)];
                                    break;
                                case 2: // 90 deg -> compare (x, y-1) and (x, y+1)
                                    m1 = pMag[yPrev + x];
                                    m2 = pMag[yNext + x];
                                    break;
                                case 3: // 135 deg -> compare (x-1, y-1) and (x+1, y+1)
                                    m1 = pMag[yPrev + (x - 1)];
                                    m2 = pMag[yNext + (x + 1)];
                                    break;
                            }

                            if (m >= m1 && m >= m2)
                            {
                                pNms[idx] = m >= highThreshF ? StrongEdge : WeakEdge;
                            }
                        }
                    }
                }

                // 5. Hysteresis Edge Tracking (Edge Linking)
                var result = ImageBuffer.CreateGray8(w, h);
                var queue = new Queue<int>();

                // Enqueue all strong edge pixels
                fixed (byte* pNms = nms)
                {
                    for (int y = 1; y < h - 1; y++)
                    {
                        int rowOffset = y * w;
                        for (int x = 1; x < w - 1; x++)
                        {
                            int idx = rowOffset + x;
                            if (pNms[idx] == StrongEdge)
                            {
                                queue.Enqueue(idx);
                                *(result.GetRowPointer(y) + x) = StrongEdge;
                            }
                        }
                    }

                    // 8-way connectivity expansion for weak edges
                    int[] neighborOffsets = new int[]
                    {
                        -w - 1, -w, -w + 1,
                        -1,          1,
                         w - 1,  w,  w + 1
                    };

                    while (queue.Count > 0)
                    {
                        int currIdx = queue.Dequeue();
                        int cx = currIdx % w;
                        int cy = currIdx / w;

                        if (cx <= 0 || cx >= w - 1 || cy <= 0 || cy >= h - 1)
                            continue;

                        for (int k = 0; k < 8; k++)
                        {
                            int nIdx = currIdx + neighborOffsets[k];
                            if (pNms[nIdx] == WeakEdge)
                            {
                                pNms[nIdx] = StrongEdge; // Mark visited
                                int nx = nIdx % w;
                                int ny = nIdx / w;
                                *(result.GetRowPointer(ny) + nx) = StrongEdge;
                                queue.Enqueue(nIdx);
                            }
                        }
                    }
                }

                return result;
            }
            finally
            {
                grayOwned?.Dispose();
            }
        }

        private static unsafe void ApplyGaussian5x5(ImageBuffer src, byte[] dst, int w, int h)
        {
            // Separable 1D Gaussian kernel: [1, 4, 6, 4, 1] / 16
            int[] temp = new int[w * h];

            // Horizontal pass
            for (int y = 0; y < h; y++)
            {
                byte* pSrcRow = src.GetRowPointer(y);
                int rowOffset = y * w;

                for (int x = 2; x < w - 2; x++)
                {
                    temp[rowOffset + x] =
                        pSrcRow[x - 2] * 1 +
                        pSrcRow[x - 1] * 4 +
                        pSrcRow[x]     * 6 +
                        pSrcRow[x + 1] * 4 +
                        pSrcRow[x + 2] * 1;
                }

                // Handle borders
                temp[rowOffset + 0] = pSrcRow[0] * 16;
                temp[rowOffset + 1] = pSrcRow[1] * 16;
                temp[rowOffset + (w - 2)] = pSrcRow[w - 2] * 16;
                temp[rowOffset + (w - 1)] = pSrcRow[w - 1] * 16;
            }

            // Vertical pass
            for (int y = 2; y < h - 2; y++)
            {
                int y0 = (y - 2) * w;
                int y1 = (y - 1) * w;
                int y2 = y * w;
                int y3 = (y + 1) * w;
                int y4 = (y + 2) * w;

                for (int x = 0; x < w; x++)
                {
                    int sum =
                        temp[y0 + x] * 1 +
                        temp[y1 + x] * 4 +
                        temp[y2 + x] * 6 +
                        temp[y3 + x] * 4 +
                        temp[y4 + x] * 1;

                    dst[y2 + x] = (byte)(sum >> 8); // divide by 256
                }
            }

            // Copy border rows directly
            for (int x = 0; x < w; x++)
            {
                dst[0 * w + x] = *(src.GetRowPointer(0) + x);
                dst[1 * w + x] = *(src.GetRowPointer(1) + x);
                dst[(h - 2) * w + x] = *(src.GetRowPointer(h - 2) + x);
                dst[(h - 1) * w + x] = *(src.GetRowPointer(h - 1) + x);
            }
        }
    }
}

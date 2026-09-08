using System;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Advanced image binarization and thresholding engines:
    /// - Otsu's Global Optimal Thresholding (Maximizing inter-class variance)
    /// - Bradley-Roth Local Adaptive Thresholding (O(1) Integral Image)
    /// </summary>
    public static unsafe class Thresholding
    {
        /// <summary>
        /// Computes Otsu's optimal global binarization threshold for a Gray8 image buffer.
        /// </summary>
        public static byte ComputeOtsuThreshold(ImageBuffer gray)
        {
            if (gray == null) throw new ArgumentNullException(nameof(gray));
            if (gray.Format != ImageFormatMode.Gray8) throw new ArgumentException("Buffer must be Gray8 format.");

            int width = gray.Width;
            int height = gray.Height;
            int totalPixels = width * height;

            // 1. Calculate histogram
            int[] hist = new int[256];
            for (int y = 0; y < height; y++)
            {
                byte* row = gray.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    hist[row[x]]++;
                }
            }

            // 2. Compute total sum for mean
            float sumAll = 0.0f;
            for (int i = 0; i < 256; i++)
            {
                sumAll += i * hist[i];
            }

            float sumB = 0.0f;
            int weightB = 0;
            float maxVariance = -1.0f;
            long bestSum = 0;
            int bestCount = 0;

            // 3. Scan thresholds to maximize inter-class variance
            for (int t = 0; t < 256; t++)
            {
                weightB += hist[t];
                if (weightB == 0) continue;

                int weightF = totalPixels - weightB;
                if (weightF == 0) break;

                sumB += t * hist[t];

                float meanB = sumB / weightB;
                float meanF = (sumAll - sumB) / weightF;

                // sigma_B^2 = wB * wF * (meanB - meanF)^2
                float diff = meanB - meanF;
                float variance = (float)weightB * (float)weightF * diff * diff;

                if (variance > maxVariance + 0.0001f)
                {
                    maxVariance = variance;
                    bestSum = t;
                    bestCount = 1;
                }
                else if (global::System.Math.Abs(variance - maxVariance) <= 0.0001f)
                {
                    bestSum += t;
                    bestCount++;
                }
            }

            return bestCount > 0 ? (byte)(bestSum / bestCount) : (byte)128;
        }

        /// <summary>
        /// Applies Otsu binarization to automatically compute threshold and generate a clean binary 0/255 image.
        /// </summary>
        public static byte OtsuBinarize(ImageBuffer src, ImageBuffer dst, bool invert = false)
        {
            byte threshold = ComputeOtsuThreshold(src);
            ApplyBinaryThreshold(src, dst, threshold, invert);
            return threshold;
        }

        /// <summary>
        /// Binarizes a Gray8 image buffer using a fixed threshold value.
        /// </summary>
        public static void ApplyBinaryThreshold(ImageBuffer src, ImageBuffer dst, byte threshold, bool invert = false)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("Both buffers must be Gray8 format.");

            int width = src.Width;
            int height = src.Height;

            byte fg = invert ? (byte)0 : (byte)255;
            byte bg = invert ? (byte)255 : (byte)0;

            for (int y = 0; y < height; y++)
            {
                byte* srcRow = src.GetRowPointer(y);
                byte* dstRow = dst.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    dstRow[x] = srcRow[x] >= threshold ? fg : bg;
                }
            }
        }

        /// <summary>
        /// Bradley-Roth Adaptive Thresholding using an O(1) Integral Image (Summed Area Table).
        /// Ideal for barcode scanning and camera inspection under non-uniform illumination and curved surfaces.
        /// </summary>
        /// <param name="src">Source Gray8 buffer.</param>
        /// <param name="dst">Destination Gray8 binary buffer.</param>
        /// <param name="windowRatio">Ratio of window size relative to image width (default 1/8th = 0.125f).</param>
        /// <param name="percentage">Percentage below local mean to classify as black/foreground (default 0.15f = 15%).</param>
        public static void AdaptiveThresholdBradley(ImageBuffer src, ImageBuffer dst, float windowRatio = 0.125f, float percentage = 0.15f)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("Buffers must be Gray8 format.");

            int width = src.Width;
            int height = src.Height;

            int s = (int)(width * windowRatio);
            if (s < 3) s = 3;
            int s2 = s / 2;

            // 1. Compute Integral Image (Summed Area Table)
            // Array size: (width + 1) * (height + 1)
            long[] integral = new long[(width + 1) * (height + 1)];

            fixed (long* pInt = integral)
            {
                int intStride = width + 1;

                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = src.GetRowPointer(y);
                    long rowSum = 0;

                    for (int x = 0; x < width; x++)
                    {
                        rowSum += srcRow[x];
                        // integral[y+1, x+1] = integral[y, x+1] + rowSum
                        pInt[(y + 1) * intStride + (x + 1)] = pInt[y * intStride + (x + 1)] + rowSum;
                    }
                }

                // 2. Perform Adaptive Thresholding using O(1) rectangular sum lookups
                float factor = 1.0f - percentage;

                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = src.GetRowPointer(y);
                    byte* dstRow = dst.GetRowPointer(y);

                    int y1 = global::System.Math.Max(0, y - s2);
                    int y2 = global::System.Math.Min(height - 1, y + s2);

                    for (int x = 0; x < width; x++)
                    {
                        int x1 = global::System.Math.Max(0, x - s2);
                        int x2 = global::System.Math.Min(width - 1, x + s2);

                        int count = (x2 - x1 + 1) * (y2 - y1 + 1);

                        // Rectangular sum from Integral Image:
                        // Sum = I(y2+1, x2+1) - I(y1, x2+1) - I(y2+1, x1) + I(y1, x1)
                        long sum = pInt[(y2 + 1) * intStride + (x2 + 1)]
                                 - pInt[y1 * intStride + (x2 + 1)]
                                 - pInt[(y2 + 1) * intStride + x1]
                                 + pInt[y1 * intStride + x1];

                        // If pixel value is significantly below local average, mark as black (0), else white (255)
                        dstRow[x] = (srcRow[x] * count) <= (sum * factor) ? (byte)0 : (byte)255;
                    }
                }
            }
        }
    }
}

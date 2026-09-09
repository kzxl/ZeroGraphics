using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Utility for extracting 1D intensity profiles and Run-Length Encoded (RLE) bars and spaces
    /// from grayscale image scanlines with zero unnecessary heap allocations.
    /// </summary>
    public static class BarcodeScanline
    {
        /// <summary>
        /// Minimum contrast (max - min) along a scanline required to consider it valid barcode content.
        /// </summary>
        public const int DefaultMinContrast = 30;

        /// <summary>
        /// Extracts alternating black/white run-lengths along a horizontal row.
        /// </summary>
        /// <param name="image">Grayscale 8-bit image buffer.</param>
        /// <param name="y">Y row coordinate.</param>
        /// <param name="runs">Output list of consecutive run lengths in pixels.</param>
        /// <param name="firstIsBlack">True if the first run in the list corresponds to a black bar.</param>
        /// <param name="minContrast">Minimum intensity contrast required.</param>
        /// <returns>True if a valid contrast profile was found and converted to runs; false otherwise.</returns>
        public static unsafe bool ExtractHorizontalRuns(
            ImageBuffer image,
            int y,
            out List<int> runs,
            out bool firstIsBlack,
            int minContrast = DefaultMinContrast)
        {
            runs = new List<int>();
            firstIsBlack = false;

            if (image == null || y < 0 || y >= image.Height || image.Format != ImageFormatMode.Gray8)
                return false;

            int width = image.Width;
            if (width < 20)
                return false;

            byte* row = image.GetRowPointer(y);

            // 1. Determine min and max intensity along the line
            int minVal = 255;
            int maxVal = 0;
            for (int x = 0; x < width; x++)
            {
                byte val = row[x];
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }

            if ((maxVal - minVal) < minContrast)
                return false;

            // 2. Midpoint threshold
            int threshold = (minVal + maxVal) / 2;

            // 3. Run-length encoding
            bool currentIsBlack = (row[0] < threshold);
            firstIsBlack = currentIsBlack;
            int currentLength = 1;

            for (int x = 1; x < width; x++)
            {
                bool isBlack = (row[x] < threshold);
                if (isBlack == currentIsBlack)
                {
                    currentLength++;
                }
                else
                {
                    runs.Add(currentLength);
                    currentIsBlack = isBlack;
                    currentLength = 1;
                }
            }
            runs.Add(currentLength);

            return runs.Count >= 6;
        }

        /// <summary>
        /// Extracts alternating black/white run-lengths along a vertical column.
        /// </summary>
        public static unsafe bool ExtractVerticalRuns(
            ImageBuffer image,
            int x,
            out List<int> runs,
            out bool firstIsBlack,
            int minContrast = DefaultMinContrast)
        {
            runs = new List<int>();
            firstIsBlack = false;

            if (image == null || x < 0 || x >= image.Width || image.Format != ImageFormatMode.Gray8)
                return false;

            int height = image.Height;
            if (height < 20)
                return false;

            // 1. Determine min and max intensity along the line
            int minVal = 255;
            int maxVal = 0;
            for (int y = 0; y < height; y++)
            {
                byte val = image.GetRowPointer(y)[x];
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }

            if ((maxVal - minVal) < minContrast)
                return false;

            int threshold = (minVal + maxVal) / 2;

            bool currentIsBlack = (image.GetRowPointer(0)[x] < threshold);
            firstIsBlack = currentIsBlack;
            int currentLength = 1;

            for (int y = 1; y < height; y++)
            {
                bool isBlack = (image.GetRowPointer(y)[x] < threshold);
                if (isBlack == currentIsBlack)
                {
                    currentLength++;
                }
                else
                {
                    runs.Add(currentLength);
                    currentIsBlack = isBlack;
                    currentLength = 1;
                }
            }
            runs.Add(currentLength);

            return runs.Count >= 6;
        }
    }
}

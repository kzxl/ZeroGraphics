using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// Multi-scanline 1D barcode scanner supporting horizontal, vertical, and bidirectional orientations
    /// for Code 128 and Code 39 symbologies with zero external dependencies.
    /// </summary>
    public static class BarcodeReader1D
    {
        /// <summary>
        /// Attempts to decode a 1D barcode from a grayscale image buffer.
        /// </summary>
        /// <param name="image">Grayscale 8-bit image buffer.</param>
        /// <param name="expectedSymbology">Expected symbology, or Unknown to auto-detect all supported symbologies.</param>
        /// <param name="scanlineStepFraction">Fraction of image height/width between scanline passes (default 0.15 = ~7 passes).</param>
        /// <returns>Decoded BarcodeResult, or null if no valid barcode was found.</returns>
        public static BarcodeResult? Decode(
            ImageBuffer image,
            BarcodeSymbology expectedSymbology = BarcodeSymbology.Unknown,
            double scanlineStepFraction = 0.15)
        {
            if (image == null || image.Width < 20 || image.Height < 20)
                return null;

            ImageBuffer? grayOwned = null;
            ImageBuffer grayImage = image;
            if (image.Format != ImageFormatMode.Gray8)
            {
                grayOwned = ImageBuffer.CreateGray8(image.Width, image.Height);
                ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(image, grayOwned);
                grayImage = grayOwned;
            }

            try
            {
                int stepH = Math.Max(2, (int)(grayImage.Height * scanlineStepFraction));
                int stepW = Math.Max(2, (int)(grayImage.Width * scanlineStepFraction));

                // 1. Horizontal scanline passes (0° and 180° barcodes)
                for (int y = stepH; y < grayImage.Height; y += stepH)
                {
                    if (BarcodeScanline.ExtractHorizontalRuns(grayImage, y, out var runs, out bool firstIsBlack))
                    {
                        var result = TryDecodeScanline(runs, firstIsBlack, expectedSymbology, isHorizontal: true, coord: y);
                        if (result != null)
                            return result;

                        // Also try reversed scanline (180° inverted barcode)
                        var reversed = ReverseRuns(runs);
                        bool lastIsBlack = (runs.Count % 2 != 0) ? firstIsBlack : !firstIsBlack;
                        result = TryDecodeScanline(reversed, lastIsBlack, expectedSymbology, isHorizontal: true, coord: y);
                        if (result != null)
                            return result;
                    }
                }

                // 2. Vertical scanline passes (90° and 270° barcodes)
                for (int x = stepW; x < grayImage.Width; x += stepW)
                {
                    if (BarcodeScanline.ExtractVerticalRuns(grayImage, x, out var runs, out bool firstIsBlack))
                    {
                        var result = TryDecodeScanline(runs, firstIsBlack, expectedSymbology, isHorizontal: false, coord: x);
                        if (result != null)
                            return result;

                        var reversed = ReverseRuns(runs);
                        bool lastIsBlack = (runs.Count % 2 != 0) ? firstIsBlack : !firstIsBlack;
                        result = TryDecodeScanline(reversed, lastIsBlack, expectedSymbology, isHorizontal: false, coord: x);
                        if (result != null)
                            return result;
                    }
                }

                return null;
            }
            finally
            {
                grayOwned?.Dispose();
            }
        }

        private static BarcodeResult? TryDecodeScanline(
            List<int> runs,
            bool firstIsBlack,
            BarcodeSymbology expectedSymbology,
            bool isHorizontal,
            int coord)
        {
            // Code 128
            if (expectedSymbology == BarcodeSymbology.Unknown || expectedSymbology == BarcodeSymbology.Code128)
            {
                if (Code128Decoder.TryDecode(runs, firstIsBlack, out string text128, out double conf128))
                {
                    var points = isHorizontal
                        ? new PointF[] { new PointF(0, coord), new PointF(100, coord) }
                        : new PointF[] { new PointF(coord, 0), new PointF(coord, 100) };

                    return new BarcodeResult(
                        text: text128,
                        symbology: BarcodeSymbology.Code128,
                        rawBytes: System.Text.Encoding.ASCII.GetBytes(text128),
                        cornerPoints: points,
                        confidence: conf128);
                }
            }

            // Code 39
            if (expectedSymbology == BarcodeSymbology.Unknown || expectedSymbology == BarcodeSymbology.Code39)
            {
                if (Code39Decoder.TryDecode(runs, firstIsBlack, out string text39, out double conf39))
                {
                    var points = isHorizontal
                        ? new PointF[] { new PointF(0, coord), new PointF(100, coord) }
                        : new PointF[] { new PointF(coord, 0), new PointF(coord, 100) };

                    return new BarcodeResult(
                        text: text39,
                        symbology: BarcodeSymbology.Code39,
                        rawBytes: System.Text.Encoding.ASCII.GetBytes(text39),
                        cornerPoints: points,
                        confidence: conf39);
                }
            }

            return null;
        }

        private static List<int> ReverseRuns(List<int> original)
        {
            var rev = new List<int>(original.Count);
            for (int i = original.Count - 1; i >= 0; i--)
            {
                rev.Add(original[i]);
            }
            return rev;
        }
    }
}

using System;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Matching
{
    /// <summary>
    /// Industrial Normalized Cross-Correlation (NCC) Template Matcher.
    /// Provides linear brightness/contrast invariant pattern search with sub-pixel peak interpolation.
    /// </summary>
    public static class NccTemplateMatcher
    {
        /// <summary>
        /// Matches a template within a search image using Normalized Cross Correlation (NCC).
        /// </summary>
        /// <param name="searchImage">Source inspection image to search within.</param>
        /// <param name="templateImage">Target pattern to locate.</param>
        /// <param name="minScore">Minimum correlation threshold (default 0.75).</param>
        /// <param name="subPixelRefinement">Whether to compute sub-pixel parabolic peak refinement.</param>
        /// <returns>Matching result with sub-pixel center coordinates and confidence score.</returns>
        public static unsafe TemplateMatchResult Match(
            ImageBuffer searchImage,
            ImageBuffer templateImage,
            double minScore = 0.75,
            bool subPixelRefinement = true)
        {
            if (searchImage == null) throw new ArgumentNullException(nameof(searchImage));
            if (templateImage == null) throw new ArgumentNullException(nameof(templateImage));

            int sw = searchImage.Width;
            int sh = searchImage.Height;
            int tw = templateImage.Width;
            int th = templateImage.Height;

            if (tw > sw || th > sh || tw <= 0 || th <= 0)
                return TemplateMatchResult.NotFound;

            // 1. Ensure Grayscale representation for both images
            ImageBuffer? graySearchOwned = null;
            ImageBuffer? grayTemplateOwned = null;

            ImageBuffer graySearch = searchImage;
            if (searchImage.Format != ImageFormatMode.Gray8)
            {
                graySearchOwned = ImageBuffer.CreateGray8(sw, sh);
                ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(searchImage, graySearchOwned);
                graySearch = graySearchOwned;
            }

            ImageBuffer grayTemplate = templateImage;
            if (templateImage.Format != ImageFormatMode.Gray8)
            {
                grayTemplateOwned = ImageBuffer.CreateGray8(tw, th);
                ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(templateImage, grayTemplateOwned);
                grayTemplate = grayTemplateOwned;
            }

            try
            {
                int n = tw * th;

                // 2. Precompute Template Mean and Standard Deviation
                double tSum = 0.0;
                double[] tPixels = new double[n];
                int tIdx = 0;

                for (int ty = 0; ty < th; ty++)
                {
                    byte* pRow = grayTemplate.GetRowPointer(ty);
                    for (int tx = 0; tx < tw; tx++)
                    {
                        double val = pRow[tx];
                        tPixels[tIdx++] = val;
                        tSum += val;
                    }
                }

                double tMean = tSum / n;
                double tVarianceSum = 0.0;
                for (int i = 0; i < n; i++)
                {
                    tPixels[i] -= tMean; // Zero-mean template
                    tVarianceSum += tPixels[i] * tPixels[i];
                }

                double tStdDev = Math.Sqrt(tVarianceSum);
                if (tStdDev < 1e-6)
                {
                    // Template has uniform flat intensity (no contrast features)
                    return TemplateMatchResult.NotFound;
                }

                // 3. Compute Integral Images of Search Image for O(1) Window Mean/Variance
                // II: sum of I, II2: sum of I^2
                int iiW = sw + 1;
                int iiH = sh + 1;
                double[] ii = new double[iiW * iiH];
                double[] ii2 = new double[iiW * iiH];

                for (int y = 0; y < sh; y++)
                {
                    byte* pRow = graySearch.GetRowPointer(y);
                    double rowSum = 0.0;
                    double rowSum2 = 0.0;

                    int iiRowIdx = (y + 1) * iiW;
                    int iiPrevRowIdx = y * iiW;

                    for (int x = 0; x < sw; x++)
                    {
                        double val = pRow[x];
                        rowSum += val;
                        rowSum2 += val * val;

                        ii[iiRowIdx + (x + 1)] = ii[iiPrevRowIdx + (x + 1)] + rowSum;
                        ii2[iiRowIdx + (x + 1)] = ii2[iiPrevRowIdx + (x + 1)] + rowSum2;
                    }
                }

                // Helper local functions for O(1) rectangle queries
                double GetWindowSum(double[] table, int x, int y, int w, int h)
                {
                    int x2 = x + w;
                    int y2 = y + h;
                    return table[y2 * iiW + x2] - table[y * iiW + x2] - table[y2 * iiW + x] + table[y * iiW + x];
                }

                // 4. Slide window across search image
                int outW = sw - tw + 1;
                int outH = sh - th + 1;

                double bestScore = -1.0;
                int bestX = -1;
                int bestY = -1;

                // Cache correlation surface for sub-pixel interpolation
                double[]? scoreMap = subPixelRefinement ? new double[outW * outH] : null;

                for (int y = 0; y < outH; y++)
                {
                    for (int x = 0; x < outW; x++)
                    {
                        // O(1) Window variance computation
                        double iSum = GetWindowSum(ii, x, y, tw, th);
                        double iSum2 = GetWindowSum(ii2, x, y, tw, th);
                        double iVar = iSum2 - (iSum * iSum) / n;

                        if (iVar <= 1e-6)
                        {
                            if (scoreMap != null) scoreMap[y * outW + x] = 0.0;
                            continue;
                        }

                        double iStdDev = Math.Sqrt(iVar);

                        // Cross-product sum: sum( (T - tMean) * I )
                        double crossSum = 0.0;
                        int pixelIdx = 0;

                        for (int ty = 0; ty < th; ty++)
                        {
                            byte* pSearchRow = graySearch.GetRowPointer(y + ty) + x;
                            for (int tx = 0; tx < tw; tx++)
                            {
                                crossSum += tPixels[pixelIdx++] * pSearchRow[tx];
                            }
                        }

                        double score = crossSum / (tStdDev * iStdDev);
                        if (scoreMap != null) scoreMap[y * outW + x] = score;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestX = x;
                            bestY = y;
                        }
                    }
                }

                if (bestScore < minScore || bestX < 0)
                {
                    return TemplateMatchResult.NotFound;
                }

                // 5. 2D Parabolic Sub-Pixel Peak Interpolation
                double finalX = bestX;
                double finalY = bestY;

                if (subPixelRefinement && scoreMap != null)
                {
                    if (bestX > 0 && bestX < outW - 1)
                    {
                        double l = scoreMap[bestY * outW + (bestX - 1)];
                        double c = scoreMap[bestY * outW + bestX];
                        double r = scoreMap[bestY * outW + (bestX + 1)];
                        double denom = 2.0 * (l - 2.0 * c + r);
                        if (Math.Abs(denom) > 1e-8)
                        {
                            double deltaX = (l - r) / denom;
                            if (Math.Abs(deltaX) <= 1.0) finalX += deltaX;
                        }
                    }

                    if (bestY > 0 && bestY < outH - 1)
                    {
                        double t = scoreMap[(bestY - 1) * outW + bestX];
                        double c = scoreMap[bestY * outW + bestX];
                        double b = scoreMap[(bestY + 1) * outW + bestX];
                        double denom = 2.0 * (t - 2.0 * c + b);
                        if (Math.Abs(denom) > 1e-8)
                        {
                            double deltaY = (t - b) / denom;
                            if (Math.Abs(deltaY) <= 1.0) finalY += deltaY;
                        }
                    }
                }

                return new TemplateMatchResult(finalX, finalY, tw, th, bestScore);
            }
            finally
            {
                graySearchOwned?.Dispose();
                grayTemplateOwned?.Dispose();
            }
        }
    }
}

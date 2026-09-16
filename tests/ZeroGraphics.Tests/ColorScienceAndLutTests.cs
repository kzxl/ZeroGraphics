using System;
using Xunit;
using ZeroGraphics.Imaging.Filters;

namespace ZeroGraphics.Tests
{
    public class ColorScienceAndLutTests
    {
        [Theory]
        [InlineData(0.0f, 0.0f, 0.0f)]       // Black
        [InlineData(1.0f, 1.0f, 1.0f)]       // White
        [InlineData(0.18f, 0.18f, 0.18f)]   // Middle gray
        [InlineData(1.0f, 0.0f, 0.0f)]       // Red
        [InlineData(0.0f, 1.0f, 0.0f)]       // Green
        [InlineData(0.0f, 0.0f, 1.0f)]       // Blue
        public void Oklab_RoundTrip_Invertible(float r, float g, float b)
        {
            OklabColor.LinearRgbToOklab(r, g, b, out float L, out float a, out float okB);
            OklabColor.OklabToLinearRgb(L, a, okB, out float rRec, out float gRec, out float bRec);

            Assert.InRange(rRec, r - 1e-4f, r + 1e-4f);
            Assert.InRange(gRec, g - 1e-4f, g + 1e-4f);
            Assert.InRange(bRec, b - 1e-4f, b + 1e-4f);
        }

        [Fact]
        public void Oklab_GamutCompression_PreservesHue()
        {
            float L = 0.95f;
            float a = 0.35f;
            float b = 0.20f;

            float originalHue = (float)Math.Atan2(b, a);

            OklabColor.CompressToGamut(ref L, ref a, ref b);
            OklabColor.OklabToLinearRgb(L, a, b, out float r, out float g, out float outB);

            Assert.True(OklabColor.IsInGamut(r, g, outB, 1e-3f));

            float newHue = (float)Math.Atan2(b, a);
            Assert.Equal(originalHue, newHue, 3);
        }

        [Theory]
        [InlineData(0.0f)]
        [InlineData(0.33f)]
        [InlineData(0.67f)]
        [InlineData(1.0f)]
        public void ColorLut3D_Tetrahedral_PreservesNeutralAxis(float v)
        {
            int size = 2;
            var table = new float[size * size * size * 3];
            for (int b = 0; b < size; b++)
            for (int g = 0; g < size; g++)
            for (int r = 0; r < size; r++)
            {
                int idx = ((b * size + g) * size + r) * 3;
                table[idx] = r / (float)(size - 1);
                table[idx + 1] = g / (float)(size - 1);
                table[idx + 2] = b / (float)(size - 1);
            }

            var lut = new ColorLut3D(size, table);
            lut.Sample(v, v, v, out float or, out float og, out float ob);

            Assert.InRange(or, v - 1e-4f, v + 1e-4f);
            Assert.InRange(og, v - 1e-4f, v + 1e-4f);
            Assert.InRange(ob, v - 1e-4f, v + 1e-4f);
        }

        [Fact]
        public void ZoneSystemMeter_ClassifiesAnselAdamsZones()
        {
            Assert.Equal(0, ZoneSystemMeter.GetZoneFromSrgbByte(0));    // Black -> Zone 0
            Assert.Equal(5, ZoneSystemMeter.GetZoneFromSrgbByte(118));  // 18% Middle Gray -> Zone V
            Assert.Equal(8, ZoneSystemMeter.GetZoneFromSrgbByte(215));  // Highlight -> Zone VIII
            Assert.Equal(10, ZoneSystemMeter.GetZoneFromSrgbByte(255)); // Specular Clip -> Zone X

            var pixels = new byte[] { 0, 0, 0, 255, 118, 118, 118, 255 };
            ZoneSystemMeter.MapBgraPixelsToZoneMask(pixels);

            Assert.Equal(40, pixels[0]);  // Zone 0 Blue
            Assert.Equal(158, pixels[4]); // Zone V Gray
        }

        [Fact]
        public void DehazeFilter_IdentityWhenZeroAmount()
        {
            float[] pixels = new float[] { 0.2f, 0.5f, 0.8f, 1.0f, 0.1f, 0.3f, 0.6f, 1.0f };
            float[] clone = (float[])pixels.Clone();

            DehazeFilter.ApplyRgbaFloat(pixels, 2, 1, amount: 0.0f);

            Assert.Equal(clone, pixels);
        }

        [Fact]
        public void DehazeFilter_RemovesHazeAndBoostsContrast()
        {
            int w = 16, h = 16;
            float[] pixels = new float[w * h * 4];

            // Create synthetic hazy image: dark scene (0.05 to 0.4) blended with bright haze (0.75)
            float air = 0.75f;
            float t = 0.4f; // 40% transmission (60% haze)
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                float trueScene = (x + y) / (float)(w + h) * 0.4f;
                float hazyVal = trueScene * t + air * (1f - t);
                pixels[p] = hazyVal;
                pixels[p + 1] = hazyVal;
                pixels[p + 2] = hazyVal;
                pixels[p + 3] = 1.0f;
            }

            float initialMin = pixels[0];
            DehazeFilter.ApplyRgbaFloat(pixels, w, h, amount: 0.9f);

            // After dehazing, the darkest areas should recover towards their true dark values (< initialMin)
            Assert.True(pixels[0] < initialMin, $"Dehazed min {pixels[0]} should be darker than hazy input {initialMin}");
        }

        [Fact]
        public void DefringeFilter_RemovesPurpleFringeOnHighContrastEdge()
        {
            int w = 8, h = 8;
            float[] pixels = new float[w * h * 4];

            // Background white (1.0)
            for (int i = 0; i < w * h; i++)
            {
                pixels[i * 4] = 1.0f;
                pixels[i * 4 + 1] = 1.0f;
                pixels[i * 4 + 2] = 1.0f;
                pixels[i * 4 + 3] = 1.0f;
            }

            // Dark object on left half
            for (int y = 0; y < h; y++)
            for (int x = 0; x < 4; x++)
            {
                int p = (y * w + x) * 4;
                pixels[p] = 0.05f;
                pixels[p + 1] = 0.05f;
                pixels[p + 2] = 0.05f;
            }

            // Purple fringe right along the edge boundary (x = 4, y = 4)
            int fringeIdx = (4 * w + 4) * 4;
            pixels[fringeIdx] = 0.85f;     // R
            pixels[fringeIdx + 1] = 0.15f; // G
            pixels[fringeIdx + 2] = 0.85f; // B

            float initialChroma = Math.Abs(pixels[fringeIdx] - pixels[fringeIdx + 1]);

            DefringeFilter.ApplyRgbaFloat(pixels, w, h, purpleAmount: 1.0f, greenAmount: 0.0f, edgeThreshold: 0.1f);

            float postChroma = Math.Abs(pixels[fringeIdx] - pixels[fringeIdx + 1]);
            Assert.True(postChroma < initialChroma, $"Fringe chroma {postChroma} should be significantly reduced from {initialChroma}");
        }

        [Fact]
        public void DefringeFilter_PreservesPurpleSubjectOnFlatRegion()
        {
            int w = 8, h = 8;
            float[] pixels = new float[w * h * 4];

            // Completely flat purple subject (no edge)
            for (int i = 0; i < w * h; i++)
            {
                pixels[i * 4] = 0.80f;     // R
                pixels[i * 4 + 1] = 0.20f; // G
                pixels[i * 4 + 2] = 0.80f; // B
                pixels[i * 4 + 3] = 1.0f;
            }

            float origR = pixels[0];
            float origG = pixels[1];

            DefringeFilter.ApplyRgbaFloat(pixels, w, h, purpleAmount: 1.0f, greenAmount: 1.0f, edgeThreshold: 0.1f);

            // Because flat region has 0 edge magnitude, purple subject remains completely untouched!
            Assert.Equal(origR, pixels[0], 3);
            Assert.Equal(origG, pixels[1], 3);
        }
    }
}


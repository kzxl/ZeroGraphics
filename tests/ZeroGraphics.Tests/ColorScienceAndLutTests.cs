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
    }
}

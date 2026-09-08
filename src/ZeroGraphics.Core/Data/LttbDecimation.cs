using System;

namespace ZeroGraphics.Core.Data
{
    public readonly struct TimePoint : IEquatable<TimePoint>
    {
        public double X { get; }
        public double Y { get; }

        public TimePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TimePoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is TimePoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public static bool operator ==(TimePoint left, TimePoint right) => left.Equals(right);
        public static bool operator !=(TimePoint left, TimePoint right) => !left.Equals(right);
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    /// <summary>
    /// Zero-allocation Largest-Triangle-Three-Buckets (LTTB) decimation algorithm.
    /// Downsamples millions of telemetry points to viewport resolution while preserving visual peaks and valleys.
    /// </summary>
    public static class LttbDecimation
    {
        public static int Downsample(ReadOnlySpan<TimePoint> data, Span<TimePoint> destination, int targetCount)
        {
            int dataLength = data.Length;
            if (targetCount >= dataLength || targetCount <= 2 || dataLength <= 2)
            {
                int copyCount = System.Math.Min(dataLength, destination.Length);
                data.Slice(0, copyCount).CopyTo(destination);
                return copyCount;
            }

            int sampledIndex = 0;
            double every = (double)(dataLength - 2) / (targetCount - 2);

            int a = 0;
            destination[sampledIndex++] = data[a];

            for (int i = 0; i < targetCount - 2; i++)
            {
                double avgX = 0;
                double avgY = 0;
                int avgRangeStart = (int)System.Math.Floor((i + 1) * every) + 1;
                int avgRangeEnd = (int)System.Math.Floor((i + 2) * every) + 1;
                if (avgRangeEnd > dataLength) avgRangeEnd = dataLength;

                int avgRangeLength = avgRangeEnd - avgRangeStart;
                if (avgRangeLength > 0)
                {
                    for (int j = avgRangeStart; j < avgRangeEnd; j++)
                    {
                        ref readonly var pt = ref data[j];
                        avgX += pt.X;
                        avgY += pt.Y;
                    }
                    avgX /= avgRangeLength;
                    avgY /= avgRangeLength;
                }
                else
                {
                    ref readonly var last = ref data[dataLength - 1];
                    avgX = last.X;
                    avgY = last.Y;
                }

                int rangeOffs = (int)System.Math.Floor(i * every) + 1;
                int rangeTo = (int)System.Math.Floor((i + 1) * every) + 1;
                if (rangeTo > dataLength) rangeTo = dataLength;

                ref readonly var pointA = ref data[a];
                double pointAx = pointA.X;
                double pointAy = pointA.Y;

                double maxArea = -1;
                int maxAreaIndex = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    ref readonly var pt = ref data[j];
                    double area = System.Math.Abs(
                        (pointAx - avgX) * (pt.Y - pointAy) -
                        (pointAx - pt.X) * (avgY - pointAy)
                    ) * 0.5;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxAreaIndex = j;
                    }
                }

                destination[sampledIndex++] = data[maxAreaIndex];
                a = maxAreaIndex;
            }

            destination[sampledIndex++] = data[dataLength - 1];
            return sampledIndex;
        }
    }
}

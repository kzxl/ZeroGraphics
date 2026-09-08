using System;
using System.Collections.Generic;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// Fitted 2D straight line represented in normalized form: A*x + B*y + C = 0, where A^2 + B^2 = 1.
    /// </summary>
    public readonly struct FittedLine2D
    {
        public double A { get; }
        public double B { get; }
        public double C { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public double AngleDegrees { get; }
        public double RmsError { get; }

        public FittedLine2D(double a, double b, double c, double cx, double cy, double angleDeg, double rms)
        {
            A = a;
            B = b;
            C = c;
            CenterX = cx;
            CenterY = cy;
            AngleDegrees = angleDeg;
            RmsError = rms;
        }

        /// <summary>
        /// Computes perpendicular orthogonal distance from a point to this line.
        /// </summary>
        public double DistanceTo(double px, double py)
            => Math.Abs(A * px + B * py + C);

        public override string ToString()
            => $"[Line] {A:F4}x + {B:F4}y + {C:F2} = 0, Angle={AngleDegrees:F2}°, RMS={RmsError:F4}";
    }

    /// <summary>
    /// Fitted 2D circle with center, radius, and fitting accuracy.
    /// </summary>
    public readonly struct FittedCircle2D
    {
        public double CenterX { get; }
        public double CenterY { get; }
        public double Radius { get; }
        public double Diameter => Radius * 2.0;
        public double RmsError { get; }

        public FittedCircle2D(double cx, double cy, double r, double rms)
        {
            CenterX = cx;
            CenterY = cy;
            Radius = r;
            RmsError = rms;
        }

        /// <summary>
        /// Radial deviation of a point from the circle boundary (positive = outside, negative = inside).
        /// </summary>
        public double RadialDistanceTo(double px, double py)
        {
            double dx = px - CenterX;
            double dy = py - CenterY;
            return Math.Sqrt(dx * dx + dy * dy) - Radius;
        }

        public override string ToString()
            => $"[Circle] Center=({CenterX:F3}, {CenterY:F3}), R={Radius:F3}, Dia={Diameter:F3}, RMS={RmsError:F4}";
    }

    /// <summary>
    /// Robust orthogonal geometric fitters for machine vision metrology (TLS Line Fit & Taubin Circle Fit).
    /// </summary>
    public static class GeometryFitters
    {
        /// <summary>
        /// Fits a line to a set of 2D points using Total Least Squares (Orthogonal Distance Regression).
        /// </summary>
        public static FittedLine2D FitLine(IReadOnlyList<VisionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 2) throw new ArgumentException("At least 2 points are required to fit a line.");

            int n = points.Count;
            double sumX = 0.0;
            double sumY = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += points[i].X;
                sumY += points[i].Y;
            }

            double meanX = sumX / n;
            double meanY = sumY / n;

            // Covariance moments
            double sxx = 0.0;
            double syy = 0.0;
            double sxy = 0.0;

            for (int i = 0; i < n; i++)
            {
                double dx = points[i].X - meanX;
                double dy = points[i].Y - meanY;
                sxx += dx * dx;
                syy += dy * dy;
                sxy += dx * dy;
            }

            // Normal vector angle via eigendecomposition of covariance matrix
            double theta = 0.5 * Math.Atan2(-2.0 * sxy, syy - sxx);
            double a = Math.Cos(theta);
            double b = Math.Sin(theta);
            double c = -(a * meanX + b * meanY);

            // Calculate RMS error
            double sqErrorSum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double dist = a * points[i].X + b * points[i].Y + c;
                sqErrorSum += dist * dist;
            }
            double rms = Math.Sqrt(sqErrorSum / n);

            // Direction angle of the line (perpendicular to normal)
            double lineAngleRad = Math.Atan2(-a, b);
            double lineAngleDeg = lineAngleRad * (180.0 / Math.PI);
            if (lineAngleDeg < 0.0) lineAngleDeg += 180.0;

            return new FittedLine2D(a, b, c, meanX, meanY, lineAngleDeg, rms);
        }

        /// <summary>
        /// Fits a line to detected caliper edge points.
        /// </summary>
        public static FittedLine2D FitLine(IReadOnlyList<CaliperEdge> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            var points = new VisionPoint2D[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                points[i] = new VisionPoint2D(edges[i].X, edges[i].Y);
            return FitLine(points);
        }

        /// <summary>
        /// Fits a circle to a set of 2D points using the Taubin Algebraic Circle Fitting algorithm.
        /// Unbiased and robust even for partial arc segments.
        /// </summary>
        public static FittedCircle2D FitCircle(IReadOnlyList<VisionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 3) throw new ArgumentException("At least 3 points are required to fit a circle.");

            int n = points.Count;
            double sumX = 0.0;
            double sumY = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += points[i].X;
                sumY += points[i].Y;
            }

            double meanX = sumX / n;
            double meanY = sumY / n;

            // Moments relative to centroid
            double mxx = 0.0, myy = 0.0, mxy = 0.0;
            double mxz = 0.0, myz = 0.0, mzz = 0.0;

            for (int i = 0; i < n; i++)
            {
                double xi = points[i].X - meanX;
                double yi = points[i].Y - meanY;
                double zi = xi * xi + yi * yi;

                mxx += xi * xi;
                myy += yi * yi;
                mxy += xi * yi;
                mxz += xi * zi;
                myz += yi * zi;
                mzz += zi * zi;
            }

            mxx /= n;
            myy /= n;
            mxy /= n;
            mxz /= n;
            myz /= n;
            mzz /= n;

            // Characteristic polynomial coefficients
            double mz = mxx + myy;
            double covXy = mxx * myy - mxy * mxy;
            double a3 = 4.0 * mz;
            double a2 = -3.0 * mz * mz - mzz;
            double a1 = mzz * mz + 4.0 * covXy * mz - mxz * mxz - myz * myz - mz * mz * mz;
            double a0 = mxz * mxz * myy + myz * myz * mxx - 2.0 * mxz * myz * mxy - covXy * mzz;

            // Solve root near zero using Newton-Raphson iteration
            double eta = 0.0;
            for (int iter = 0; iter < 20; iter++)
            {
                double f = a0 + eta * (a1 + eta * (a2 + eta * (a3 + eta)));
                double fPrime = a1 + eta * (2.0 * a2 + eta * (3.0 * a3 + 4.0 * eta));
                if (Math.Abs(fPrime) < 1e-12) break;
                double step = f / fPrime;
                eta -= step;
                if (Math.Abs(step) < 1e-10) break;
            }

            // Determine center and radius
            double det = (mxx - eta) * (myy - eta) - mxy * mxy;
            if (Math.Abs(det) < 1e-12) det = 1e-12;

            double uc = (mxz * (myy - eta) - myz * mxy) / (2.0 * det);
            double vc = (myz * (mxx - eta) - mxz * mxy) / (2.0 * det);

            double cx = uc + meanX;
            double cy = vc + meanY;
            double r = Math.Sqrt(uc * uc + vc * vc + mz);

            // Compute RMS error
            double sqErrSum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = Math.Sqrt((points[i].X - cx) * (points[i].X - cx) + (points[i].Y - cy) * (points[i].Y - cy)) - r;
                sqErrSum += d * d;
            }
            double rms = Math.Sqrt(sqErrSum / n);

            return new FittedCircle2D(cx, cy, r, rms);
        }

        /// <summary>
        /// Fits a circle to detected caliper edge points.
        /// </summary>
        public static FittedCircle2D FitCircle(IReadOnlyList<CaliperEdge> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            var points = new VisionPoint2D[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                points[i] = new VisionPoint2D(edges[i].X, edges[i].Y);
            return FitCircle(points);
        }
    }

    /// <summary>
    /// Industrial Metrology Measurements & Tolerance Verification.
    /// </summary>
    public static class MetrologyMeasurements
    {
        /// <summary>
        /// Euclidean distance between two 2D points.
        /// </summary>
        public static double Distance(VisionPoint2D p1, VisionPoint2D p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Distance from a point to a fitted line.
        /// </summary>
        public static double Distance(VisionPoint2D p, FittedLine2D line)
            => line.DistanceTo(p.X, p.Y);

        /// <summary>
        /// Average perpendicular distance between two roughly parallel lines.
        /// </summary>
        public static double DistanceBetweenLines(FittedLine2D line1, FittedLine2D line2)
        {
            double d1 = line2.DistanceTo(line1.CenterX, line1.CenterY);
            double d2 = line1.DistanceTo(line2.CenterX, line2.CenterY);
            return (d1 + d2) * 0.5;
        }

        /// <summary>
        /// Angular difference between two lines in degrees [0, 90].
        /// </summary>
        public static double AngleBetweenLines(FittedLine2D line1, FittedLine2D line2)
        {
            double diff = Math.Abs(line1.AngleDegrees - line2.AngleDegrees);
            if (diff > 90.0) diff = 180.0 - diff;
            return diff;
        }

        /// <summary>
        /// Concentricity (center-to-center offset error) between two circles.
        /// 0.0 indicates perfect concentric alignment.
        /// </summary>
        public static double Concentricity(FittedCircle2D c1, FittedCircle2D c2)
        {
            double dx = c1.CenterX - c2.CenterX;
            double dy = c1.CenterY - c2.CenterY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}

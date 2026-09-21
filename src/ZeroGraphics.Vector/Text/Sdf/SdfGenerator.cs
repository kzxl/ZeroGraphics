using System;
using System.Collections.Generic;
using ZeroGraphics.Vector.Geometry;
using ZeroGraphics.Vector.Tessellation;

namespace ZeroGraphics.Vector.Text.Sdf
{
    /// <summary>
    /// Computes Euclidean Signed Distance Fields (SDF) from 2D vector paths.
    /// Converts continuous Bézier vector glyphs into high-resolution distance textures for GPU rendering.
    /// </summary>
    public static class SdfGenerator
    {
        private struct Segment
        {
            public float Ax, Ay;
            public float Bx, By;
            public float MinX, MaxX;
            public float MinY, MaxY;

            public Segment(float ax, float ay, float bx, float by)
            {
                Ax = ax;
                Ay = ay;
                Bx = bx;
                By = by;
                MinX = Math.Min(ax, bx);
                MaxX = Math.Max(ax, bx);
                MinY = Math.Min(ay, by);
                MaxY = Math.Max(ay, by);
            }
        }

        /// <summary>
        /// Generates an 8-bit unsigned distance field buffer for the specified Path2D within the given bounds.
        /// </summary>
        /// <param name="path">Vector path to rasterize.</param>
        /// <param name="width">Width of the output distance bitmap.</param>
        /// <param name="height">Height of the output distance bitmap.</param>
        /// <param name="boundsMinX">Minimum X in vector coordinates.</param>
        /// <param name="boundsMinY">Minimum Y in vector coordinates.</param>
        /// <param name="boundsMaxX">Maximum X in vector coordinates.</param>
        /// <param name="boundsMaxY">Maximum Y in vector coordinates.</param>
        /// <param name="spread">Distance spread in vector coordinate units across which the field transitions from 0 to 255.</param>
        /// <param name="flattenTolerance">Tolerance for Bézier curve flattening.</param>
        /// <returns>A byte array of size (width * height) containing normalized SDF values (128 = boundary, >128 = inside, &lt;128 = outside).</returns>
        public static byte[] GenerateSdf(
            Path2D path,
            int width,
            int height,
            float boundsMinX,
            float boundsMinY,
            float boundsMaxX,
            float boundsMaxY,
            float spread,
            float flattenTolerance = 0.25f)
        {
            if (width <= 0 || height <= 0) return Array.Empty<byte>();

            var output = new byte[width * height];
            if (path == null || path.VerbCount == 0 || spread <= 1e-6f)
            {
                return output;
            }

            var subpaths = AdaptiveFlattening.Flatten(path, flattenTolerance);
            var segments = new List<Segment>(64);

            for (int s = 0; s < subpaths.Count; s++)
            {
                var pts = subpaths[s];
                int count = pts.Count;
                if (count < 2) continue;

                for (int i = 0; i < count - 1; i++)
                {
                    segments.Add(new Segment(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y));
                }

                // If closed contour, add closing segment if not already closed
                if ((pts[count - 1] - pts[0]).LengthSquared > 1e-5f)
                {
                    segments.Add(new Segment(pts[count - 1].X, pts[count - 1].Y, pts[0].X, pts[0].Y));
                }
            }

            int numSegments = segments.Count;
            if (numSegments == 0)
            {
                return output;
            }

            float rangeX = Math.Max(boundsMaxX - boundsMinX, 1e-4f);
            float rangeY = Math.Max(boundsMaxY - boundsMinY, 1e-4f);

            float stepX = rangeX / width;
            float stepY = rangeY / height;
            float invSpread = 1.0f / spread;

            for (int y = 0; y < height; y++)
            {
                float py = boundsMinY + (y + 0.5f) * stepY;
                int rowOffset = y * width;

                for (int x = 0; x < width; x++)
                {
                    float px = boundsMinX + (x + 0.5f) * stepX;

                    float minSqDist = float.MaxValue;
                    int intersections = 0;

                    for (int i = 0; i < numSegments; i++)
                    {
                        var seg = segments[i];

                        // Distance from point (px, py) to line segment
                        float sqDist = PointToSegmentSquaredDistance(px, py, seg.Ax, seg.Ay, seg.Bx, seg.By);
                        if (sqDist < minSqDist)
                        {
                            minSqDist = sqDist;
                        }

                        // Ray-casting along +X axis for inside/outside determination (Even-Odd)
                        if ((seg.Ay <= py && seg.By > py) || (seg.By <= py && seg.Ay > py))
                        {
                            float t = (py - seg.Ay) / (seg.By - seg.Ay);
                            float intersectX = seg.Ax + t * (seg.Bx - seg.Ax);
                            if (intersectX > px)
                            {
                                intersections++;
                            }
                        }
                    }

                    float dist = (float)Math.Sqrt(minSqDist);
                    bool inside = (intersections % 2) != 0;
                    float signedDist = inside ? dist : -dist;

                    // Normalize to [0..1] range, centered at 0.5 (128) on the glyph boundary
                    float norm = 0.5f + (signedDist * 0.5f * invSpread);
                    if (norm < 0.0f) norm = 0.0f;
                    else if (norm > 1.0f) norm = 1.0f;

                    output[rowOffset + x] = (byte)(norm * 255.0f);
                }
            }

            return output;
        }

        private static float PointToSegmentSquaredDistance(
            float px, float py,
            float ax, float ay,
            float bx, float by)
        {
            float abx = bx - ax;
            float aby = by - ay;
            float apx = px - ax;
            float apy = py - ay;

            float abLenSq = abx * abx + aby * aby;
            if (abLenSq < 1e-8f)
            {
                return apx * apx + apy * apy;
            }

            float t = (apx * abx + apy * aby) / abLenSq;
            if (t < 0.0f) t = 0.0f;
            else if (t > 1.0f) t = 1.0f;

            float projX = ax + t * abx;
            float projY = ay + t * aby;
            float dx = px - projX;
            float dy = py - projY;

            return dx * dx + dy * dy;
        }
    }
}

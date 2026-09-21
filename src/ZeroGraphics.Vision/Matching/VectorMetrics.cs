using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace ZeroGraphics.Vision.Matching
{
    /// <summary>
    /// Hardware-accelerated SIMD (AVX2/FMA) metrics for high-dimensional image feature vectors and embeddings.
    /// Provides sub-microsecond DotProduct, CosineSimilarity, EuclideanDistance, and binary HammingDistance.
    /// </summary>
    public static unsafe class VectorMetrics
    {
        /// <summary>
        /// Computes the dot product of two vectors: sum(a[i] * b[i]).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var sumVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            sumVec = Fma.MultiplyAdd(va, vb, sumVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            sumVec += va * vb;
                        }
                    }

                    float sum = Vector256.Sum(sumVec);
                    for (; i < len; i++)
                    {
                        sum += pA[i] * pB[i];
                    }
                    return sum;
                }
#endif
                return DotProductScalar(pA, pB, len);
            }
        }

        /// <summary>
        /// Computes the Cosine Similarity between two vectors in [-1.0, 1.0]: (a . b) / (||a|| * ||b||).
        /// Accumulates dot product, norm(a), and norm(b) simultaneously in a single memory pass.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var dotVec = Vector256<float>.Zero;
                    var normAVec = Vector256<float>.Zero;
                    var normBVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            dotVec = Fma.MultiplyAdd(va, vb, dotVec);
                            normAVec = Fma.MultiplyAdd(va, va, normAVec);
                            normBVec = Fma.MultiplyAdd(vb, vb, normBVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            dotVec += va * vb;
                            normAVec += va * va;
                            normBVec += vb * vb;
                        }
                    }

                    float dot = Vector256.Sum(dotVec);
                    float normA = Vector256.Sum(normAVec);
                    float normB = Vector256.Sum(normBVec);

                    for (; i < len; i++)
                    {
                        float fa = pA[i];
                        float fb = pB[i];
                        dot += fa * fb;
                        normA += fa * fa;
                        normB += fb * fb;
                    }

                    if (normA <= 1e-12f || normB <= 1e-12f) return 0f;
                    float denom = (float)Math.Sqrt(normA * normB);
                    float cos = dot / denom;
                    return cos > 1.0f ? 1.0f : (cos < -1.0f ? -1.0f : cos);
                }
#endif
                return CosineSimilarityScalar(pA, pB, len);
            }
        }

        /// <summary>
        /// Computes the Squared Euclidean (L2) distance: sum((a[i] - b[i])^2).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistanceSquared(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var distVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            var diff = va - vb;
                            distVec = Fma.MultiplyAdd(diff, diff, distVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            var diff = va - vb;
                            distVec += diff * diff;
                        }
                    }

                    float distSq = Vector256.Sum(distVec);
                    for (; i < len; i++)
                    {
                        float d = pA[i] - pB[i];
                        distSq += d * d;
                    }
                    return distSq;
                }
#endif
                return EuclideanDistanceSquaredScalar(pA, pB, len);
            }
        }

        /// <summary>
        /// Computes Euclidean (L2) distance: sqrt(sum((a[i] - b[i])^2)).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            return (float)Math.Sqrt(EuclideanDistanceSquared(a, b));
        }

        /// <summary>
        /// Computes the bitwise Hamming distance between two byte buffers (e.g. 256-bit ORB/BRIEF binary descriptors).
        /// Uses 64-bit hardware POPCNT instructions for maximum throughput.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HammingDistance(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Buffer lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0;

            int totalDistance = 0;
            int i = 0;

            fixed (byte* pA = a, pB = b)
            {
                // 64-bit word chunks
                int ulongLimit = len - 7;
                for (; i < ulongLimit; i += 8)
                {
                    ulong wordA = *(ulong*)(pA + i);
                    ulong wordB = *(ulong*)(pB + i);
                    totalDistance += PopCount64(wordA ^ wordB);
                }

                // Byte remainder
                for (; i < len; i++)
                {
                    byte diff = (byte)(pA[i] ^ pB[i]);
                    totalDistance += PopCountByte(diff);
                }
            }

            return totalDistance;
        }

        /// <summary>
        /// Normalizes a vector in-place to unit length (L2 norm = 1.0).
        /// </summary>
        public static void NormalizeL2(Span<float> vector)
        {
            int len = vector.Length;
            if (len == 0) return;

            float normSq = 0.0f;
            fixed (float* p = vector)
            {
                normSq = DotProduct(vector, vector);
                if (normSq <= 1e-12f) return;

                float invNorm = 1.0f / (float)Math.Sqrt(normSq);
                for (int i = 0; i < len; i++)
                {
                    p[i] *= invNorm;
                }
            }
        }

        // =========================================================================
        // Scalar reference implementations (for bit-exact validation and fallback)
        // =========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProductScalar(float* a, float* b, int length)
        {
            float sum0 = 0f, sum1 = 0f, sum2 = 0f, sum3 = 0f;
            int i = 0;
            int limit = length - 3;

            for (; i < limit; i += 4)
            {
                sum0 += a[i] * b[i];
                sum1 += a[i + 1] * b[i + 1];
                sum2 += a[i + 2] * b[i + 2];
                sum3 += a[i + 3] * b[i + 3];
            }

            float total = sum0 + sum1 + sum2 + sum3;
            for (; i < length; i++)
            {
                total += a[i] * b[i];
            }
            return total;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosineSimilarityScalar(float* a, float* b, int length)
        {
            float dot = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < length; i++)
            {
                float fa = a[i];
                float fb = b[i];
                dot += fa * fb;
                normA += fa * fa;
                normB += fb * fb;
            }

            if (normA <= 1e-12f || normB <= 1e-12f) return 0f;
            float cos = dot / (float)Math.Sqrt(normA * normB);
            return cos > 1.0f ? 1.0f : (cos < -1.0f ? -1.0f : cos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistanceSquaredScalar(float* a, float* b, int length)
        {
            float sum0 = 0f, sum1 = 0f, sum2 = 0f, sum3 = 0f;
            int i = 0;
            int limit = length - 3;

            for (; i < limit; i += 4)
            {
                float d0 = a[i] - b[i];
                float d1 = a[i + 1] - b[i + 1];
                float d2 = a[i + 2] - b[i + 2];
                float d3 = a[i + 3] - b[i + 3];
                sum0 += d0 * d0;
                sum1 += d1 * d1;
                sum2 += d2 * d2;
                sum3 += d3 * d3;
            }

            float total = sum0 + sum1 + sum2 + sum3;
            for (; i < length; i++)
            {
                float d = a[i] - b[i];
                total += d * d;
            }
            return total;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount64(ulong x)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(x);
#else
            // Branchless 64-bit Hamming weight SWAR
            x -= (x >> 1) & 0x5555555555555555UL;
            x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
            x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((x * 0x0101010101010101UL) >> 56);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCountByte(byte b)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(b);
#else
            uint x = b;
            x -= (x >> 1) & 0x55;
            x = (x & 0x33) + ((x >> 2) & 0x33);
            return (int)((x + (x >> 4)) & 0x0F);
#endif
        }
    }
}

using System;
using System.Collections.Generic;

namespace ZeroGraphics.Vision.Matching
{
    public enum VectorMetricType
    {
        Cosine,
        EuclideanSquared,
        DotProduct
    }

    public readonly struct VectorSearchResult : IComparable<VectorSearchResult>
    {
        public int Id { get; }
        public float Score { get; }

        public VectorSearchResult(int id, float score)
        {
            Id = id;
            Score = score;
        }

        public int CompareTo(VectorSearchResult other)
        {
            return Score.CompareTo(other.Score);
        }

        public override string ToString() => $"VectorSearchResult(Id={Id}, Score={Score:F4})";
    }

    /// <summary>
    /// Contiguous cache-aligned vector database for high-throughput image feature similarity search.
    /// Stores high-dimensional vector embeddings in flat contiguous memory and performs batch SIMD evaluation.
    /// </summary>
    public sealed class FeatureVectorIndex
    {
        private readonly int _dimension;
        private float[] _data;
        private int[] _ids;
        private int _count;
        private readonly object _lock = new object();

        public int Dimension => _dimension;
        public int Count => _count;

        public FeatureVectorIndex(int dimension, int initialCapacity = 64)
        {
            if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be positive.");
            if (initialCapacity < 16) initialCapacity = 16;

            _dimension = dimension;
            _data = new float[initialCapacity * dimension];
            _ids = new int[initialCapacity];
            _count = 0;
        }

        /// <summary>
        /// Adds a vector embedding associated with the specified identifier into the contiguous index.
        /// </summary>
        public void Add(int id, ReadOnlySpan<float> vector)
        {
            if (vector.Length != _dimension)
                throw new ArgumentException($"Vector dimension {vector.Length} does not match index dimension {_dimension}.");

            lock (_lock)
            {
                EnsureCapacity(_count + 1);
                int offset = _count * _dimension;
                vector.CopyTo(_data.AsSpan(offset, _dimension));
                _ids[_count] = id;
                _count++;
            }
        }

        /// <summary>
        /// Searches for the Top-K nearest neighbors to the query vector.
        /// Returns results sorted from most relevant to least relevant.
        /// </summary>
        public VectorSearchResult[] SearchTopK(ReadOnlySpan<float> query, int k, VectorMetricType metric = VectorMetricType.Cosine)
        {
            if (query.Length != _dimension)
                throw new ArgumentException($"Query vector dimension {query.Length} must match index dimension {_dimension}.");

            if (k <= 0 || _count == 0) return Array.Empty<VectorSearchResult>();

            int actualK = Math.Min(k, _count);
            var candidates = new List<VectorSearchResult>(_count);

            lock (_lock)
            {
                for (int i = 0; i < _count; i++)
                {
                    int offset = i * _dimension;
                    var storedSpan = new ReadOnlySpan<float>(_data, offset, _dimension);
                    float score = metric switch
                    {
                        VectorMetricType.Cosine => VectorMetrics.CosineSimilarity(query, storedSpan),
                        VectorMetricType.EuclideanSquared => VectorMetrics.EuclideanDistanceSquared(query, storedSpan),
                        VectorMetricType.DotProduct => VectorMetrics.DotProduct(query, storedSpan),
                        _ => VectorMetrics.CosineSimilarity(query, storedSpan)
                    };

                    candidates.Add(new VectorSearchResult(_ids[i], score));
                }
            }

            // For Cosine and DotProduct, highest score is best (descending)
            // For Euclidean, lowest distance is best (ascending)
            if (metric == VectorMetricType.EuclideanSquared)
            {
                candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            }
            else
            {
                candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            }

            var result = new VectorSearchResult[actualK];
            for (int i = 0; i < actualK; i++)
            {
                result[i] = candidates[i];
            }

            return result;
        }

        /// <summary>
        /// Clears all vectors from the index.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _count = 0;
            }
        }

        private void EnsureCapacity(int minCapacity)
        {
            if (_ids.Length >= minCapacity) return;

            int newCap = Math.Max(_ids.Length * 2, minCapacity);
            Array.Resize(ref _ids, newCap);
            Array.Resize(ref _data, newCap * _dimension);
        }
    }
}

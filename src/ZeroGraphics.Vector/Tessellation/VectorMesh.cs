using System;
using System.Collections.Generic;

namespace ZeroGraphics.Vector.Tessellation
{
    /// <summary>
    /// Holds triangulated vector geometry ready for direct uploading into GPU vertex and index buffers.
    /// </summary>
    public sealed class VectorMesh
    {
        private readonly List<VectorVertex> _vertices;
        private readonly List<uint> _indices;

        public IReadOnlyList<VectorVertex> Vertices => _vertices;
        public IReadOnlyList<uint> Indices => _indices;

        public int VertexCount => _vertices.Count;
        public int IndexCount => _indices.Count;
        public int TriangleCount => _indices.Count / 3;

        public VectorMesh(int initialVertexCapacity = 256, int initialIndexCapacity = 512)
        {
            _vertices = new List<VectorVertex>(initialVertexCapacity);
            _indices = new List<uint>(initialIndexCapacity);
        }

        public uint AddVertex(in VectorVertex vertex)
        {
            uint index = (uint)_vertices.Count;
            _vertices.Add(vertex);
            return index;
        }

        public uint AddVertex(float x, float y, uint color)
        {
            return AddVertex(new VectorVertex(x, y, 0f, 0f, color));
        }

        public void AddIndex(uint index)
        {
            _indices.Add(index);
        }

        public void AddTriangle(uint i0, uint i1, uint i2)
        {
            _indices.Add(i0);
            _indices.Add(i1);
            _indices.Add(i2);
        }

        public void AddQuad(uint i0, uint i1, uint i2, uint i3)
        {
            // First triangle: i0, i1, i2
            _indices.Add(i0);
            _indices.Add(i1);
            _indices.Add(i2);

            // Second triangle: i0, i2, i3
            _indices.Add(i0);
            _indices.Add(i2);
            _indices.Add(i3);
        }

        public void Append(VectorMesh other)
        {
            if (other == null || other.VertexCount == 0) return;

            uint baseIndex = (uint)_vertices.Count;
            _vertices.AddRange(other.Vertices);

            for (int i = 0; i < other.IndexCount; i++)
            {
                _indices.Add(baseIndex + other.Indices[i]);
            }
        }

        public void Clear()
        {
            _vertices.Clear();
            _indices.Clear();
        }
    }
}

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace MocastStudio.Presentation.ProceduralMeshGenerator
{
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WireframeGrid : MonoBehaviour
    {
        [SerializeField] int _size = 1;

        int N;
        Mesh _mesh;

        void Awake()
        {
            _size = (_size <= 0) ? 1 : _size;
            N = 2 * _size + 1;

            _mesh = new();
            if (TryGetComponent(out MeshFilter meshFilter))
            {
                meshFilter.mesh = _mesh;
            }

            SetVertices();
            SetIndices();
        }

        void OnDestroy() => Destroy(_mesh);

        void SetVertices()
        {
            var vertices = new List<Vector3>();

            for (var row = 0; row < N; row++)
            {
                var z = math.remap(0, N - 1, -_size, _size, row);

                for (var column = 0; column < N; column++)
                {
                    var x = math.remap(0, N - 1, -_size, _size, column);
                    vertices.Add(new Vector3(x, 0f, z));
                }
            }

            _mesh.SetVertices(vertices);
        }

        void SetIndices()
        {
            var indices = new List<int>();

            // Row lines
            var index = 0;
            for (var row = 0; row < N; row++)
            {
                for (var column = 0; column < N - 1; column++)
                {
                    indices.Add(index);
                    indices.Add(index + 1);
                    index++;
                }
                index++;
            }

            // Column lines
            index = 0;
            for (var column = 0; column < N; column++)
            {
                for (var row = 0; row < N - 1; row++)
                {
                    indices.Add(index);
                    indices.Add(index + N);
                    index++;
                }
            }

            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }
    }
}

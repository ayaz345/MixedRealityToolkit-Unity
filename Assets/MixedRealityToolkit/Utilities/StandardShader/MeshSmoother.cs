﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Utilities
{
    /// <summary>
    /// TODO
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class MeshSmoother : MonoBehaviour
    {
        private const int smoothNormalUVChannel = 2;

        [Tooltip("TODO")]
        [SerializeField]
        private bool smoothNormalsOnAwake = false;

        private MeshFilter meshFilter = null;
        private static Dictionary<Mesh, Mesh> processedMeshes = new Dictionary<Mesh, Mesh>();

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();

            if (smoothNormalsOnAwake)
            {
                SmoothNormalsAsync();
            }
        }

        public void SmoothNormals()
        {
            Mesh mesh;

            if (AcquirePreprocessedMesh(out mesh))
            {
                return;
            }

            var result = CalculateSmoothNormals(mesh.vertices, mesh.normals);
            mesh.SetUVs(smoothNormalUVChannel, result);
        }

        public Task SmoothNormalsAsync()
        {
            Mesh mesh;

            if (AcquirePreprocessedMesh(out mesh))
            {
                return Task.CompletedTask;
            }

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var asyncTask = Task.Run(() => CalculateSmoothNormals(vertices, normals));

            return asyncTask.ContinueWith((i) =>
            {
                mesh.SetUVs(smoothNormalUVChannel, i.Result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private bool AcquirePreprocessedMesh(out Mesh mesh)
        {
            // Check if a mesh has already been processed, and if so simply assign that mesh to this filter.
            var sharedMesh = meshFilter.sharedMesh;

            if (processedMeshes.TryGetValue(sharedMesh, out mesh))
            {
                meshFilter.mesh = mesh;

                return true;
            }

            // Else, clone the current mesh and add shared mesh and cloned mesh to the table of previously smoothed meshes.
            mesh = meshFilter.mesh;
            processedMeshes[sharedMesh] = mesh;
            processedMeshes[mesh] = mesh;

            return false;
        }

        private static List<Vector3> CalculateSmoothNormals(Vector3[] verticies, Vector3[] normals)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Group all vertices that share the same location in space.
            var groupedVerticies = new Dictionary<Vector3, List<KeyValuePair<int, Vector3>>>();

            for (int i = 0; i < verticies.Length; ++i)
            {
                var vertex = verticies[i];
                List<KeyValuePair<int, Vector3>> group;

                if (!groupedVerticies.TryGetValue(vertex, out group))
                {
                    group = new List<KeyValuePair<int, Vector3>>();
                    groupedVerticies[vertex] = group;
                }

                group.Add(new KeyValuePair<int, Vector3>(i, vertex));
            }

            var smoothNormals = new List<Vector3>(normals);

            // If we don't hit the degenerate case of each vertex is it's own group (no vertices shared a location), average the normals of each group.
            if (groupedVerticies.Count != verticies.Length)
            {
                foreach (var group in groupedVerticies)
                {
                    var smoothingGroup = group.Value;

                    // No need to smooth a group of one.
                    if (smoothingGroup.Count != 1)
                    {
                        var smoothedNormal = Vector3.zero;

                        foreach (var vertex in smoothingGroup)
                        {
                            smoothedNormal += normals[vertex.Key];
                        }

                        smoothedNormal.Normalize();

                        foreach (var vertex in smoothingGroup)
                        {
                            smoothNormals[vertex.Key] = smoothedNormal;
                        }
                    }
                }
            }

            Debug.LogFormat("CalculateSmoothNormals took {0} ms on {1} vertices.", watch.ElapsedMilliseconds, verticies.Length);

            return smoothNormals;
        }
    }
}

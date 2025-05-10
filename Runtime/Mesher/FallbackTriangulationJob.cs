using System;
using jedjoud.VoxelTerrain.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Plastic.Antlr3.Runtime;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct FallbackTriangulationJob : IJob {
        // total vertices, last FALLBACK_MAX_VERTS vertices (starting from the end obv) contain the fallback vertices buffer
        public NativeArray<float3> vertices;
        public NativeList<float4> debugData;
        public NativeCounter.Concurrent indexCounter;
        public NativeArray<int> indices;
        public int sourceChunkVertexCount;
        public NativeList<StitchUtils.MissingVerticesEdgeCrossing> casesWithMissingVertices;

        public void Execute() {
            int fallbackVerticesBaseIndex = vertices.Length - StitchUtils.FALLBACK_MAX_VERTS;
            NativeHashMap<uint3, int> lookup = new NativeHashMap<uint3, int>(StitchUtils.FALLBACK_MAX_VERTS, Allocator.Temp);
            int currentFallbackVertexCount = 0;

            // first pass, keep track of missing vertices and create lil average
            uint3x4 invalidPos = new uint3x4(uint.MaxValue);

            // loop through the quads/tris that are missing a vertex and keep track of that vertex
            // also modifies the indices so that we can properly created the index in the next pass 
            for (int i = 0; i < casesWithMissingVertices.Length; i++) {
                StitchUtils.MissingVerticesEdgeCrossing data = casesWithMissingVertices[i];
                bool4 invalid = data.indices == int.MinValue;

                if (math.cmax(data.indices) == int.MaxValue || math.all(invalid)) {
                    // sorry bro can't do nun
                    continue;
                }

                // find lod1 vertex
                float3 lod1 = -1;
                for (int b = 0; b < 4; b++) {
                    if (!invalid[b] && data.indices[b] >= sourceChunkVertexCount) {
                        lod1 = vertices[data.indices[b]];
                    }
                }

                for (int b = 0; b < 4; b++) {
                    // if the vertex is invalid
                    uint3 pos = data.positions[b];
                    if (invalid[b]) {
                        // check if we already defined it before
                        if (!lookup.ContainsKey(pos)) {
                            // instantiate new vertex if not (with invalid pos for now)
                            lookup.Add(pos, currentFallbackVertexCount);
                            currentFallbackVertexCount += 1;
                        }

                        // use remapper
                        int fallbackLocalIndex = lookup[pos];
                        data.indices[b] = fallbackLocalIndex + fallbackVerticesBaseIndex;

                        // write lod1 if we found lod1
                        // if this fails hopefully another quad in the next iterations finds it for us
                        if (math.all(lod1 != -1)) {
                            vertices[fallbackLocalIndex + fallbackVerticesBaseIndex] = lod1;
                        }
                    }
                }

                // create quad/tri with updated indices
                casesWithMissingVertices[i] = data;
                //StitchUtils.AddQuadsOrTris(data.flip, data.indices, ref indexCounter, ref indices);
            }

            for (int i = 0; i < casesWithMissingVertices.Length; i++) {
                StitchUtils.MissingVerticesEdgeCrossing data = casesWithMissingVertices[i];
                bool ok = true;
                for (int j = 0; j < 4; j++) {
                    if (math.any(vertices[data.indices[j]] <= 0)) {
                        ok = false;
                    }
                }

                if (ok) {
                    StitchUtils.AddQuadsOrTris(data.flip, data.indices, ref indexCounter, ref indices);
                } else {
                    debugData.Add(new float4(data.positions[0], 1f));
                }
            }

            lookup.Dispose();
        }
    }
}
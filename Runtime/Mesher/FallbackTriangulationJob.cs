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
        public NativeCounter.Concurrent indexCounter;
        public NativeArray<int> indices;

        [ReadOnly]
        public NativeList<StitchUtils.MissingVerticesEdgeCrossing> casesWithMissingVertices;

        public void Execute() {
            int fallbackVerticesBaseIndex = vertices.Length - StitchUtils.FALLBACK_MAX_VERTS;
            NativeHashMap<uint3, int> lookup = new NativeHashMap<uint3, int>(StitchUtils.FALLBACK_MAX_VERTS, Allocator.Temp);
            NativeList<(float3, int)> averages = new NativeList<(float3, int)>(StitchUtils.FALLBACK_MAX_VERTS, Allocator.Temp);

            // first pass, keep track of missing vertices and create lil average
            uint3x4 invalidPos = new uint3x4(uint.MaxValue);

            // loop through the quads/tris that are missing a vertex and keep track of that vertex
            // also modifies the indices so that we can properly created the index in the next pass 
            for (int i = 0; i < casesWithMissingVertices.Length; i++) {
                StitchUtils.MissingVerticesEdgeCrossing data = casesWithMissingVertices[i];

                if (math.cmax(data.indices) == int.MaxValue || math.all(data.indices == int.MinValue)) {
                    // sorry bro can't do nun
                    continue;
                }

                bool4 invalid = data.indices == int.MinValue;

                // calculate average position of VALID vertices
                float3 avgPos = 0;
                int count = 0;

                for (int b = 0; b < 4; b++) {
                    if (!invalid[b]) {
                        avgPos += vertices[data.indices[b]];
                        count++;
                    }
                }

                for (int b = 0; b < 4; b++) {
                    // if the vertex is invalid
                    uint3 pos = data.positions[b];
                    if (invalid[b]) {
                        // check if we already defined it before
                        if (!lookup.ContainsKey(pos)) {
                            // instantiate new vertex if not (with invalid pos for now)
                            lookup.Add(pos, averages.Length);
                            averages.Add((float3.zero, 0));
                        }

                        // use remapper
                        int fallbackLocalIndex = lookup[pos];
                        data.indices[b] = fallbackLocalIndex + fallbackVerticesBaseIndex;

                        // add to the average sum
                        (float3 avgPosInner, int countInner) = averages[fallbackLocalIndex];
                        avgPosInner += avgPos;
                        countInner += count;
                        averages[fallbackLocalIndex] = (avgPosInner, countInner);
                    }
                }

                // create quad/tri with updated indices
                StitchUtils.AddQuadsOrTris(data.flip, data.indices, ref indexCounter, ref indices);
            }

            // update vertex positions using averages
            for (int i = 0; i < averages.Length; i++) {
                (float3 sum, int count) = averages[i];
                vertices[i + fallbackVerticesBaseIndex] = sum / count;
            }

            lookup.Dispose();
            averages.Dispose();
        }
    }
}
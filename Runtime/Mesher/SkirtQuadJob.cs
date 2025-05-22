using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Diagnostics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct SkirtQuadJob : IJobParallelFor {
        public NativeList<float3>.ParallelWriter debugData;

        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [ReadOnly]
        public NativeArray<int> skirtVertexIndicesGenerated;

        [ReadOnly]
        public NativeArray<int> skirtVertexIndicesCopied;

        
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> skirtForcedPerFaceIndices;

        [WriteOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<int> skirtStitchedIndices;

        public NativeCounter.Concurrent skirtStitchedTriangleCounter;
        public NativeMultiCounter.Concurrent skirtForcedTriangleCounter;

        // Fetch vertex index for a specific position
        // If it goes out of the chunk bounds, assume it is a skirt vertex's position we're trying to fetch
        int FetchIndex(int3 position, int face) {
            int direction = face % 3;
            int2 flattened = SkirtUtils.FlattenToFaceRelative(position, direction);
            int other = position[direction];

            // checks if we are dealing with a skirt vertex or a copied vertex in a particular direction
            if (other < 0 || other > VoxelUtils.SIZE - 3) {
                // since the skirt generated vertices have 2 padding vertices (for edges), we need to add an offset 
                flattened += 1;
                flattened = math.clamp(flattened, 0, VoxelUtils.SKIRT_SIZE);

                // lookup in the appropriate 2D index table
                int lookup = VoxelUtils.PosToIndex2D((uint2)flattened, VoxelUtils.SKIRT_SIZE);
                int res = skirtVertexIndicesGenerated[lookup + VoxelUtils.SKIRT_FACE * face];
                return res;
            } else {
                flattened = math.clamp(flattened, 0, VoxelUtils.SIZE);

                // lookup in the appropriate 2D index table
                int lookup = VoxelUtils.PosToIndex2D((uint2)flattened, VoxelUtils.SIZE);
                int res = skirtVertexIndicesCopied[lookup + VoxelUtils.FACE * face];
                return res;
            }
        }

        // Check and edge and check if we must generate a quad in it's forward facing direction
        void CheckEdge(uint2 flattened, uint3 unflattened, int index, bool negative, bool force, int face) {
            uint3 forward = DirectionOffsetUtils.FORWARD_DIRECTION[index];

            bool flip = !negative;


            if (!force) {
                int baseIndex = VoxelUtils.PosToIndex(unflattened, VoxelUtils.SIZE);
                int endIndex = VoxelUtils.PosToIndex(unflattened + forward, VoxelUtils.SIZE);

                Voxel startVoxel = voxels[baseIndex];
                Voxel endVoxel = voxels[endIndex];

                if (startVoxel.density >= 0f == endVoxel.density >= 0f)
                    return;

                flip = (endVoxel.density > 0.0);
            }

            int3 offset = (int3)((int3)unflattened + (int3)forward - math.int3(1));

            // to help the fetcher a bit lol (I honestly don't know why I need this but I do... whatever)
            if (force) {
                if (negative) {
                    offset[index] -= 1;
                } else {
                    offset[index] += 1;
                }
            }

            // load the vertex indices inside this vector
            int4 v = int.MaxValue;
            for (int i = 0; i < 4; i++) {
                v[i] = FetchIndex(offset + (int3)DirectionOffsetUtils.PERPENDICULAR_OFFSETS[index * 4 + i], face);
            }

            if (TryCalculateQuadOrTris(flip, v, out Triangulate data)) {
                if (force) {
                    NativeArray<int> faceIndicesSubArray = skirtForcedPerFaceIndices.GetSubArray(face * VoxelUtils.SKIRT_FACE * 6, VoxelUtils.SKIRT_FACE * 6);
                    AddQuadsOrTris(data, skirtForcedTriangleCounter.BecomeSigma(face), faceIndicesSubArray);
                } else {
                    AddQuadsOrTris(data, skirtStitchedTriangleCounter, skirtStitchedIndices);
                }
            }
        }


        static readonly int3[] DEDUPE_TRIS_THING = new int3[] {
            new int3(0, 2, 3), // x/y, discard y
            new int3(0, 1, 3), // x/z, discard z
            new int3(0, 1, 2), // x/w, discard w
            new int3(0, 1, 3), // y/z, discard z
            new int3(0, 1, 2), // y/w, discard w
            new int3(0, 1, 2), // z/w, discard w
        };

        static readonly int3[] IGNORE_SPECIFIC_VALUE_TRI = new int3[] {
            new int3(1, 2, 3), // discard x
            new int3(0, 2, 3), // discard y
            new int3(0, 1, 3), // discard z
            new int3(0, 1, 2), // discard w
        };

        struct Triangulate {
            public int4 indices;
            public bool triangle;

        }

        static  void AddQuadsOrTris(Triangulate triangulate, NativeCounter.Concurrent counter, NativeArray<int> indices) {
            int4 v = triangulate.indices;

            if (triangulate.triangle) {
                int triIndex = counter.Add(1) * 3;
                indices[triIndex + 0] = v[0];
                indices[triIndex + 1] = v[1];
                indices[triIndex + 2] = v[2];
            } else {
                int triIndex = counter.Add(2) * 3;

                indices[triIndex + 0] = v[0];
                indices[triIndex + 1] = v[1];
                indices[triIndex + 2] = v[2];

                indices[triIndex + 3] = v[2];
                indices[triIndex + 4] = v[3];
                indices[triIndex + 5] = v[0];
            }
        }

        // Add quads/tris for stitched triangles (filling the gap between surface nets verts and copied verts)
        // We NEED to have triangle fallback to handle the literal "edge" case (since there are only 3 vertices there)
        static bool TryCalculateQuadOrTris(bool flip, int4 v, out Triangulate data) {
            data = default;

            // Ts gpt-ed kek
            int dupeType = 0;
            dupeType |= math.select(0, 1, v.x == v.y);
            dupeType |= math.select(0, 2, v.x == v.z);
            dupeType |= math.select(0, 4, v.x == v.w);
            dupeType |= math.select(0, 8, v.y == v.z && v.x != v.y);
            dupeType |= math.select(0, 16, v.y == v.w && v.x != v.y && v.z != v.y);
            dupeType |= math.select(0, 32, v.z == v.w && v.x != v.z && v.y != v.z);

            bool4 b4 = v == int.MaxValue;
            int bitmask = math.bitmask(b4);

            // Means that there are more than 2 duplicate verts, not possible?
            if (math.countbits(dupeType) > 1) {
                return false;
            }

            // Means that there are more than 2 invalid verts, not possible to create a tri nor a quad
            if (math.countbits(bitmask) > 1) {
                return false;
            }

            // If there's only a SINGLE invalid index, then consider it as an extra duplicate one (and create a triangle for the valid ones instead of a quad)
            if (math.countbits(bitmask) == 1) {
                int3 remapper = IGNORE_SPECIFIC_VALUE_TRI[math.tzcnt(bitmask)];
                int3 uniques = new int3(v[remapper[0]], v[remapper[1]], v[remapper[2]]);

                data.triangle = true;
                data.indices.x = uniques[flip ? 0 : 2];
                data.indices.y = uniques[1];
                data.indices.z = uniques[flip ? 2 : 0];

                return true;
            }

            if (dupeType == 0) {
                if (math.cmax(v) == int.MaxValue | math.cmin(v) < 0) {
                    return false;
                }

                data.triangle = false;
                data.indices.x = v[flip ? 0 : 2];
                data.indices.y = v[1];
                data.indices.z = v[flip ? 2 : 0];
                data.indices.w = v[3];
                return true;
            } else {
                int config = math.tzcnt(dupeType);
                int3 remapper = DEDUPE_TRIS_THING[config];
                int3 uniques = new int3(v[remapper[0]], v[remapper[1]], v[remapper[2]]);

                if (math.cmax(uniques) == int.MaxValue | math.cmin(v) < 0) {
                    return false;
                }

                data.triangle = true;
                data.indices.x = uniques[flip ? 0 : 2];
                data.indices.y = uniques[1];
                data.indices.z = uniques[flip ? 2 : 0];
                return true;
            }

            return false;
        }

        public void Execute(int index) {
            // we run the job for VoxelUtils.FACE since quad jobs always miss the last 2/1 voxels (due to missing indices)
            // fine for us though...
            int face = index / VoxelUtils.FACE;
            int direction = face % 3;
            bool negative = face < 3;
            int localIndex = index % VoxelUtils.FACE;

            // convert from 2D position to 3D using missing value
            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE - 2);
            uint2 flattened = VoxelUtils.IndexToPos2D(localIndex, VoxelUtils.SIZE);
            uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, direction, missing);

            if (math.any(flattened > VoxelUtils.SKIRT_SIZE - 2)) {
                return;
            }

            for (int i = 0; i < 3; i++) {
                bool force = direction == i;

                // this skips checking the edge at a boundary if the edge is in the direction that we're looking at
                // for example, if we are at the positive x boundary, you must NOT check the edge that goes in the x direction (would result in out of bound fetches)
                // makes sense, since you well never generate quads in that direction anyways (impossible to have an edge crossing in the 3rd dimension that is missing from a 2D plane spanned by the other 2 basis vectors)
                // (unless it's a forced quad, and in which case we don't care since we don't read voxel data anyways!!!)
                if (position[i] > VoxelUtils.SIZE - 3 && !force)
                    continue;

                CheckEdge(flattened, position, i, negative, force, face);
            }
        }
    }
}
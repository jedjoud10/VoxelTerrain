using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
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
        public NativeArray<int> skirtIndices;

        public Unsafe.NativeCounter.Concurrent skirtQuadCounter;

        // Fetch vertex index for a specific position
        // If it goes out of the chunk bounds, assume it is a skirt vertex's position we're trying to fetch
        int FetchIndex(int3 position, int face) {
            int direction = face % 3;
            int2 flattened = SkirtUtils.FlattenToFaceRelative(position, direction);
            int other = position[direction];

            // checks if we are dealing with a skirt vertex or a copied vertex in a particular direction
            if (other < 0 || other > VoxelUtils.SIZE-2) {
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
            uint3 forward = VoxelUtils.FORWARD_DIRECTION[index];

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
                v[i] = FetchIndex(offset + (int3)VoxelUtils.PERPENDICULAR_OFFSETS[index * 4 + i], face);
            }

            // there are some cases where this generates more tris than necessary, but that's better than not generating enough tris
            // for that reason I will stick to using uniform SN for between chunks of the same resolution
            SkirtUtils.TryAddQuadsOrTris(flip, v, ref skirtQuadCounter, ref skirtIndices);
        }

        public void Execute(int index) {
            // we run the job for VoxelUtils.FACE since quad jobs always miss the last 2/1 voxels (due to missing indices)
            // fine for us though...
            int face = index / VoxelUtils.FACE;
            int direction = face % 3;
            bool negative = face < 3;
            int localIndex = index % VoxelUtils.FACE;
            
            // convert from 2D position to 3D using missing value
            uint missing = negative ? 0 : ((uint)VoxelUtils.SIZE-1);
            uint2 flattened = VoxelUtils.IndexToPos2D(localIndex, VoxelUtils.SIZE);
            uint3 position = SkirtUtils.UnflattenFromFaceRelative(flattened, direction, missing);

            for (int i = 0; i < 3; i++) {
                bool force = direction == i;

                // this skips checking the edge at a boundary if the edge is in the direction that we're looking at
                // for example, if we are at the positive x boundary, you must NOT check the edge that goes in the x direction (would result in out of bound fetches)
                // makes sense, since you well never generate quads in that direction anyways (impossible to have an edge crossing in the 3rd dimension that is missing from a 2D plane spanned by the other 2 basis vectors)
                // (unless it's a forced quad, and in which case we don't care since we don't read voxel data anyways!!!)
                if (position[i] > VoxelUtils.SIZE - 2 && !force)
                    continue;
                
                CheckEdge(flattened, position, i, negative, force, face);      
            }
        }
    }
}
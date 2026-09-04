using Unity.Entities;

namespace jedjoud.VoxelTerrain.Meshing  {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainMeshingSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class TerrainLightingSystem : SystemBase {
        /*
        class LightingHandler {
            public Mesh mesh;
            public UnsafePtrList<half> densityDataPtrs;
            //public NativeArray<half> combinedDensities;
            public JobHandle jobHandle;
            public Mesh.MeshDataArray meshDataArray;
            public bool Free => jobHandle.IsCompleted && mesh == null;
            private NativeArray<float3> vertices;
            private NativeArray<float3> normals;

            public LightingHandler() {
                densityDataPtrs = new UnsafePtrList<half>(27, Allocator.Persistent);

                mesh = null;
                //combinedDensities = new NativeArray<half>(VoxelUtils.VOLUME * 27, Allocator.Persistent);
                jobHandle = default;
            }

            public unsafe void Begin(BitField32 neighbourMask, TerrainChunkMesh chunkMesh, half*[] densityPtrs) {
                densityDataPtrs.Clear();
                for (int i = 0; i < 27; i++) {
                    densityDataPtrs.Add(densityPtrs[i]);
                }

                Mesh.MeshData data = meshDataArray[0];
                NativeArray<float4> colours = data.GetVertexData<float4>(2);

                vertices = new NativeArray<float3>(chunkMesh.vertices.Length, Allocator.Persistent);
                vertices.CopyFrom(chunkMesh.vertices);

                normals = new NativeArray<float3>(chunkMesh.normals.Length, Allocator.Persistent);
                normals.CopyFrom(chunkMesh.normals);

                AoJob job = new AoJob() {
                    strength = 1f,
                    globalSpread = 2f,
                    globalOffset = 0.5f,
                    minDotNormal = 0.5f,
                    neighbourMask = neighbourMask,
                    vertices = vertices,
                    normals = normals,
                    uvs = colours,
                    densityDataPtrs = densityDataPtrs,
                };

                jobHandle = job.Schedule(colours.Length, BatchUtils.SMALLEST_VERTEX_BATCH);
            }

            public void Complete() {
                jobHandle.Complete();

                if (mesh != null) {
                    Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
                    vertices.Dispose();
                    normals.Dispose();
                }

                mesh = null;
                jobHandle = default;
            }

            public void Dispose() {
                jobHandle.Complete();
                //combinedDensities.Dispose();
                densityDataPtrs.Dispose();
            }
        }

        private EntitiesGraphicsSystem graphics;
        
        private List<LightingHandler> handlers;
        const int MAX_LIGHTING_HANDLES_PER_TICK = 1;

        protected override void OnCreate() {
            handlers = new List<LightingHandler>();

            for (int i = 0; i < MAX_LIGHTING_HANDLES_PER_TICK; i++) {
                handlers.Add(new LightingHandler());
            }
        }


        protected override void OnUpdate() {
            if (graphics == null) {
                graphics = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            }

            foreach (var handler in handlers) {
                if (handler.jobHandle.IsCompleted) {
                    handler.Complete();
                }
            }

            TerrainManager manager = SystemAPI.GetSingleton<TerrainManager>();

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkRequestLightingTag, TerrainChunk, TerrainChunkVoxels, TerrainChunkVoxelsReadyTag, TerrainChunkEndOfPipeTag>().WithPresent<MaterialMeshInfo>().Build();
            NativeArray<Entity> entitiesArray = query.ToEntityArray(Allocator.Temp);

            LightingHandler[] freeHandlers = handlers.AsEnumerable().Where(x => x.Free).ToArray();
            int numChunksToProcess = math.min(freeHandlers.Length, entitiesArray.Length);

            for (int i = 0; i < numChunksToProcess; i++) {
                LightingHandler handler = freeHandlers[i];
                Entity chunkEntity = entitiesArray[i];
                TerrainChunk chunk = SystemAPI.GetComponent<TerrainChunk>(chunkEntity);
                TerrainChunkMesh chunkMesh = SystemAPI.GetComponent<TerrainChunkMesh>(chunkEntity);
                TryCalculateLightingForChunkEntity(manager, handler, chunkEntity, chunk, chunkMesh);

            }
        }


        protected override void OnDestroy() {
            foreach (var handler in handlers) {
                handler.Dispose();
            }
        }
        */
        protected override void OnUpdate() {
        }
    }
}
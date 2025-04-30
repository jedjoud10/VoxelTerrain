using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;

namespace jedjoud.VoxelTerrain.Props {
    public class VoxelProps : VoxelBehaviour {
        public bool spawnProps;
        public List<PropType> props;
        private Queue<(Vector3Int, VoxelChunk)> queuedChunks;
        private HashSet<Vector3Int> pendingChunks;
        private Dictionary<int, OngoingPropReadback> frameIdTempData;

        // Packs some prop data alongside the chunk it was generated from
        // TODO: Figure out if we need to stick to this or use the segmentation stuff that we did prior to the revamp
        private class OngoingPropReadback {
            // Number of props to spawn
            public int count;

            // The prop data that was readback from the GPU
            public NativeArray<BlittableProp> data;

            // We need both of these values to be true, since we do an async readback for the count buffer as well
            public bool countSet;
            public bool dataSet;

            // Chunk stuff
            public VoxelChunk chunk;
        }

        public override void CallerStart() {
            pendingChunks = new HashSet<Vector3Int>();
            queuedChunks = new Queue<(Vector3Int, VoxelChunk)>();
            frameIdTempData = new Dictionary<int, OngoingPropReadback>();
        }

        /*
        public void GenerateProps(VoxelChunk chunk) {
            if (!spawnProps)
                return;

            Vector3Int position = chunk.chunkPosition;
            if (pendingChunks.Contains(position)) return;
            queuedChunks.Enqueue((position, chunk));
            pendingChunks.Add(position);
        }
        */

        // Get the latest chunk in the queue and generate voxel data for it
        public override void CallerTick() {
            if (queuedChunks.TryDequeue(out var temp)) {
                (Vector3Int position, VoxelChunk chunk) = temp;
                pendingChunks.Remove(position);

                Vector3 offset = ((Vector3)position * VoxelUtils.SIZE * terrain.voxelSizeFactor);
                float scale = terrain.voxelSizeFactor;
                terrain.executor.ExecuteShader(VoxelUtils.SIZE, terrain.compiler.propsDispatchIndex, offset, Vector3.one * scale, true, true);
                int frame = Time.frameCount;


                OngoingPropReadback readback = new OngoingPropReadback() { chunk = chunk };
                frameIdTempData.Add(frame, readback);

                // Read asynchronously from the props count buffer
                AsyncGPUReadback.Request(
                    terrain.executor.buffers["props_counter"],
                    delegate (AsyncGPUReadbackRequest asyncRequest) {
                        readback.count = asyncRequest.GetData<int>()[0];
                        readback.countSet = true;
                    }
                );

                // Read asynchronously from the props data buffer
                AsyncGPUReadback.Request(
                    terrain.executor.buffers["props"],
                    delegate (AsyncGPUReadbackRequest asyncRequest) {
                        NativeArray<BlittableProp> data = new NativeArray<BlittableProp>(VoxelUtils.VOLUME, Allocator.Persistent);
                        data.CopyFrom(asyncRequest.GetData<BlittableProp>());
                        readback.data = data;
                        readback.dataSet = true;
                    }
                );
            }

            Handle();
        }

        private void Handle() {
            int[] tempFrames = frameIdTempData.Keys.ToArray();

            // Checks all the ongoing prop readbacks and spawns the props for those that have both the prop data & prop count
            foreach (int frame in tempFrames) {
                var val = frameIdTempData[frame];
                if (val.dataSet && val.countSet) {
                    // Just in case...
                    if (val.count > 10000) {
                        Debug.LogWarning("YOU ARE SPAWNING MORE THAN 10k PROPS IN ONE SINGLE CHUNK!!!");
                        return;
                    }

                    for (int i = 0; i < val.count; i++) {
                        BlittableProp packed = val.data[i];
                        GpuProp unpacked = PropUtils.UnpackProp(packed);

                        PropType type = props[unpacked.type];
                        PropType.Variant variant = type.variants[unpacked.variant];

                        Vector3 position = unpacked.position;
                        GameObject go = Instantiate(variant.prefab, transform);
                        go.transform.position = position;
                        go.transform.localScale = Vector3.one * unpacked.scale;
                        go.transform.rotation = Quaternion.Euler(unpacked.rotation);
                    }



                    val.data.Dispose();
                    frameIdTempData.Remove(frame);
                }
            }
        }

        public override void CallerDispose() {
            AsyncGPUReadback.WaitAllRequests();
            Handle();

            foreach (var item in frameIdTempData) {
                if (item.Value.data.IsCreated) {
                    item.Value.data.Dispose();
                }
            }
        }
    }
}
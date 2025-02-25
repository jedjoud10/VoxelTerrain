using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using System;
using static jedjoud.VoxelTerrain.Generation.VoxelReadback;
using System.Linq;
using System.Net.NetworkInformation;

namespace jedjoud.VoxelTerrain.Props {
    public class TempTest {
        public int count;
        public NativeArray<BlittableProp> data;
        public bool countSet;
        public bool dataSet;
        public Vector3 offset;
        public float scale;
        public Vector3Int chunkOffset;
    }

    public class VoxelProps : VoxelBehaviour {
        public GameObject prop;
        private Queue<(Vector3Int, VoxelChunk)> queuedOctalUnits;
        private HashSet<Vector3Int> pendingOctalUnits;
        private Dictionary<int, TempTest> frameIdTempData;

        public override void CallerStart() {
            pendingOctalUnits = new HashSet<Vector3Int>();
            queuedOctalUnits = new Queue<(Vector3Int, VoxelChunk)>();
            frameIdTempData = new Dictionary<int, TempTest>();
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


        public void GenerateProps(VoxelChunk chunk) {
            Vector3Int octalPosition = chunk.chunkPosition;
            if (pendingOctalUnits.Contains(octalPosition)) return;
            queuedOctalUnits.Enqueue((octalPosition, chunk));
            pendingOctalUnits.Add(octalPosition);
        }

        // Get the latest chunk in the queue and generate voxel data for it
        public override void CallerTick() {
            if (queuedOctalUnits.TryDequeue(out var temp)) {
                (Vector3Int position, VoxelChunk chunk) = temp;
                pendingOctalUnits.Remove(position);

                Vector3 offset = ((Vector3)position * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor) / (VoxelUtils.VertexScaling);
                float scale =  VoxelUtils.VoxelSizeFactor;
                terrain.executor.ExecuteShader(VoxelUtils.Size, 1, offset, Vector3.one * scale, true, true);
                int frame = Time.frameCount;

                frameIdTempData.Add(frame, new TempTest() { chunkOffset = position, offset = offset, scale = scale });

                AsyncGPUReadback.Request(
                    terrain.executor.buffers["props_counter"],
                    delegate (AsyncGPUReadbackRequest asyncRequest) {
                        frameIdTempData[frame].count = asyncRequest.GetData<int>()[0];
                        frameIdTempData[frame].countSet = true;
                    }
                );

                AsyncGPUReadback.Request(
                    terrain.executor.buffers["props"],
                    delegate (AsyncGPUReadbackRequest asyncRequest) {
                        NativeArray<BlittableProp> data = new NativeArray<BlittableProp>(VoxelUtils.Volume, Allocator.Persistent);
                        data.CopyFrom(asyncRequest.GetData<BlittableProp>());
                        frameIdTempData[frame].data = data;
                        frameIdTempData[frame].dataSet = true;
                    }
                );
            }

            Handle();
        }

        private void Handle() {
            int[] tempFrames = frameIdTempData.Keys.ToArray();

            foreach (int frame in tempFrames) {
                var val = frameIdTempData[frame];
                if (val.dataSet && val.countSet) {
                    if (val.count > 10000) {
                        throw new Exception("YOU ARE SPAWNING MORE THAN 10k PROPS IN ONE SINGLE CHUNK!!!");
                    }

                    for (int i = 0; i < val.count; i++) {
                        BlittableProp packed = val.data[i];
                        Prop unpacked = PropUtils.UnpackProp(packed);

                        Vector3 position = unpacked.position;
                        position = (position / val.scale - val.offset);

                        //position *= VoxelUtils.VoxelSizeFactor;
                        //position -= Vector3.one * 1.5f * VoxelUtils.VoxelSizeFactor;

                        //position -= math.float3(1);
                        position *= VoxelUtils.VertexScaling;
                        position += (Vector3)val.chunkOffset * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor;
                        position *= VoxelUtils.VoxelSizeFactor;
                        position += -Vector3.one * VoxelUtils.VoxelSizeFactor;

                        //position *= VoxelUtils.VertexScaling;
                        //position += (Vector3)val.chunkOffset * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor;

                        GameObject go = Instantiate(prop, transform);
                        go.transform.position = position;
                        go.transform.localScale = Vector3.one * unpacked.scale;
                        go.transform.rotation = Quaternion.Euler(unpacked.rotation);
                    }



                    val.data.Dispose();
                    frameIdTempData.Remove(frame);
                }
            }
        }
    }
}
using jedjoud.VoxelTerrain.Props;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial class SegmentPropsRenderSystem : SystemBase {
        private TerrainPropsConfig config;
        private TerrainPropPermBuffers perm;
        private TerrainPropRenderingBuffers rendering;

        protected override void OnCreate() {
            RequireForUpdate<TerrainPropsConfig>();
            RequireForUpdate<TerrainPropPermBuffers>();
            RequireForUpdate<TerrainPropRenderingBuffers>();
        }

        protected override void OnUpdate() {
            config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            perm = SystemAPI.ManagedAPI.GetSingleton<TerrainPropPermBuffers>();
            rendering = SystemAPI.ManagedAPI.GetSingleton<TerrainPropRenderingBuffers>();

            int types = config.props.Count;
            Camera cam = Camera.main;
            if (cam == null)
                return;

            CommandBuffer cmds = new CommandBuffer();
            cmds.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            cmds.name = "Compute Cull Props Buffers Dispatch";

            perm.permPropsInUseBitsetBuffer.SetData(perm.permPropsInUseBitset.AsNativeArrayExt<uint>());
            rendering.visiblePropsCountersBuffer.SetData(new uint[types]);

            cmds.SetComputeBufferParam(config.cull, 0, "perm_buffer", perm.permBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "indirection_buffer", rendering.indirectionBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "perm_props_in_use_bitset_buffer", perm.permPropsInUseBitsetBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "visible_props_counters_buffer", rendering.visiblePropsCountersBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "perm_buffer_counts_buffer", perm.permBufferCountsBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "perm_buffer_offsets_buffer", perm.permBufferOffsetsBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "max_distances_buffer", rendering.maxDistancesBuffer);
            cmds.SetComputeIntParam(config.cull, "max_combined_perm_props", perm.maxCombinedPermProps);
            cmds.SetComputeIntParam(config.cull, "types", types);

            Vector3 cameraPosition = cam.transform.position;
            Vector3 camerForward = cam.transform.forward;
            cmds.SetComputeVectorParam(config.cull, "camera_position", cameraPosition);
            cmds.SetComputeVectorParam(config.cull, "camera_forward", camerForward);

            const int THREAD_GROUP_SIZE_X = 32;
            const int CULL_INNER_LOOP_SIZE = 32;
            int threadCountX = Mathf.CeilToInt((float)perm.maxCombinedPermProps / (THREAD_GROUP_SIZE_X * CULL_INNER_LOOP_SIZE));
            cmds.DispatchCompute(config.cull, 0, threadCountX, 1, 1);

            cmds.SetComputeBufferParam(config.apply, 0, "draw_args_buffer", rendering.drawArgsBuffer);
            cmds.SetComputeBufferParam(config.apply, 0, "visible_props_counters_buffer", rendering.visiblePropsCountersBuffer);
            cmds.DispatchCompute(config.apply, 0, types, 1, 1);

            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Default);

            // TODO: need to figure out how to put this in its own URP pass or at least after all the main opaque objects have rendered
            // it being in the "middle" causes some issues
            for (int i = 0; i < config.props.Count; i++) {
                if (config.props[i].renderInstances) {
                    RenderPropsOfType(config.props[i], i, perm, rendering);
                }
            }
        }

        public void RenderPropsOfType(PropType type, int i, TerrainPropPermBuffers perm, TerrainPropRenderingBuffers rendering) {
            Material material = type.material;

            RenderParams renderParams = new RenderParams(material);
            renderParams.shadowCastingMode = type.renderInstancesShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderParams.worldBounds = new Bounds {
                min = -Vector3.one * 100000,
                max = Vector3.one * 100000,
            };

            var mat = new MaterialPropertyBlock();
            renderParams.matProps = mat;
            mat.SetBuffer("_PermBuffer", perm.permBuffer);
            mat.SetBuffer("_PermMatricesBuffer", perm.permMatricesBuffer);
            mat.SetBuffer("_IndirectionBuffer", rendering.indirectionBuffer);
            mat.SetTexture("_DiffuseMapArray", rendering.typeBatchData[i].diffuse);
            mat.SetTexture("_NormalMapArray", rendering.typeBatchData[i].normal);
            mat.SetTexture("_MaskMapArray", rendering.typeBatchData[i].mask);
            mat.SetInt("_PermBufferOffset", perm.permBufferOffsets[i]);
            mat.SetInt("_PropType", i);

            Mesh mesh = type.instancedMesh;
            Graphics.RenderMeshIndirect(renderParams, mesh, rendering.drawArgsBuffer, 1, i);
        }

    }
}
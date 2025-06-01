using jedjoud.VoxelTerrain.Props;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial class SegmentPropsRenderSystem : SystemBase {
        private TerrainPropsConfig config;
        private TerrainPropPermBuffers perm;
        private TerrainPropRenderingBuffers rendering;
        private Material material;

        protected override void OnCreate() {
            RequireForUpdate<TerrainPropsConfig>();
            RequireForUpdate<TerrainPropPermBuffers>();
            RequireForUpdate<TerrainPropRenderingBuffers>();
        }

        protected override void OnUpdate() {
            config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            perm = SystemAPI.ManagedAPI.GetSingleton<TerrainPropPermBuffers>();
            rendering = SystemAPI.ManagedAPI.GetSingleton<TerrainPropRenderingBuffers>();

            if (material == null) {
                material = new Material(config.shader);
                material.renderQueue = 2500;
            }

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
            cmds.DispatchCompute(config.apply, 0, Mathf.CeilToInt(TerrainPropRenderingBuffers.MAX_COMMAND_COUNT_PER_TYPE / 32f), types, 1);

            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Default);

            /*
            for (int i = 0; i < config.props.Count; i++) {
                if (config.props[i].renderInstances) {
                    RenderPropsOfType(config.props[i], i);
                }
            }
            */
        }

        public void RenderPropsOfType(int i, RasterGraphContext ctx) {
            if (config == null)
                return;

            PropType type = config.props[i];

            if (!config.props[i].renderInstances)
                return;

            Material muhMat = type.overrideInstancedIndirectMaterial ? type.instancedIndirectMaterial : material;
            RenderParams renderParams = new RenderParams(muhMat);
            renderParams.shadowCastingMode = type.renderInstancesShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderParams.worldBounds = new Bounds {
                min = -Vector3.one * 100000,
                max = Vector3.one * 100000,
            };
            renderParams.rendererPriority = -300;

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

            for (int c = 0; c < TerrainPropRenderingBuffers.MAX_COMMAND_COUNT_PER_TYPE; c++) {
                mat.SetInt("_WtfOffset", c * TerrainPropRenderingBuffers.MAX_INSTANCES_PER_COMMAND);
                ctx.cmd.DrawMeshInstancedIndirect(mesh, 0, muhMat, 0, rendering.drawArgsBuffer, c * 5 * sizeof(uint), mat);
            }
            //Graphics.RenderMeshIndirect(renderParams, mesh, rendering.drawArgsBuffer, TerrainPropRenderingBuffers.MAX_COMMAND_COUNT_PER_TYPE, i * TerrainPropRenderingBuffers.MAX_COMMAND_COUNT_PER_TYPE);
        }
    }
}
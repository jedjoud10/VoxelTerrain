using System.Linq;
using jedjoud.VoxelTerrain.Props;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial class TerrainSegmentPropsRenderSystem : SystemBase {
        private TerrainPropsConfig config;
        private TerrainPropPermBuffers perm;
        private TerrainPropRenderingBuffers rendering;
        private Material instancedMaterial;
        private Material impostorMaterial;
        private bool initialized;
        private Mesh quad;

        protected override void OnCreate() {
            RequireForUpdate<TerrainPropsConfig>();
            RequireForUpdate<TerrainPropPermBuffers>();
            RequireForUpdate<TerrainPropRenderingBuffers>();
            RequireForUpdate<TerrainMainCamera>();
            initialized = false;
        }

        protected override void OnUpdate() {
            config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            perm = SystemAPI.ManagedAPI.GetSingleton<TerrainPropPermBuffers>();
            rendering = SystemAPI.ManagedAPI.GetSingleton<TerrainPropRenderingBuffers>();

            if (!initialized) {
                instancedMaterial = new Material(config.instancedShader);
                impostorMaterial = new Material(config.impostorShader);
                instancedMaterial.renderQueue = 2500;
                impostorMaterial.renderQueue = 2500;
                initialized = true;
                quad = PropQuadGenerator.GenerateMuhQuad();
            }

            int types = config.props.Count;
            Entity cameraEntity = SystemAPI.GetSingletonEntity<TerrainMainCamera>();
            TerrainMainCamera cameraComponent = SystemAPI.GetComponent<TerrainMainCamera>(cameraEntity);
            LocalToWorld cameraTransform = SystemAPI.GetComponent<LocalToWorld>(cameraEntity);


            CommandBuffer cmds = new CommandBuffer();
            cmds.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            if (perm.copyFence != null)
                cmds.WaitOnAsyncGraphicsFence(perm.copyFence.Value);
            
            cmds.name = "Compute Cull Props Buffers Dispatch";

            perm.permPropsInUseBitsetBuffer.SetData(perm.permPropsInUseBitset.AsNativeArrayExt<uint>());
            rendering.visibilityCountersBuffer.SetData(new uint[types * 2]);

            cmds.SetComputeBufferParam(config.cull, 0, "perm_buffer", perm.permBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "perm_props_in_use_bitset_buffer", perm.permPropsInUseBitsetBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "visibility_counters_buffer", rendering.visibilityCountersBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "perm_buffer_counts_buffer", perm.permBufferCountsBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "perm_buffer_offsets_buffer", perm.permBufferOffsetsBuffer);

            cmds.SetComputeBufferParam(config.cull, 0, "instanced_indirection_buffer", rendering.instancedIndirectionBuffer);
            cmds.SetComputeBufferParam(config.cull, 0, "impostor_indirection_buffer", rendering.impostorIndirectionBuffer);

            cmds.SetComputeIntParam(config.cull, "max_combined_perm_props", perm.maxCombinedPermProps);
            cmds.SetComputeIntParam(config.cull, "types", types);

            cmds.SetComputeVectorParam(config.cull, "camera_position", (Vector3)cameraTransform.Position);

            Plane[] temp = GeometryUtility.CalculateFrustumPlanes(cameraComponent.worldToProjection);
            Vector4[] frustums = temp.Select(plane => new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance)).ToArray();
            cmds.SetComputeVectorArrayParam(config.cull, "camera_frustum_planes", frustums);

            Vector4[] cullingSpheres = rendering.cullingSpheres;
            cmds.SetComputeVectorArrayParam(config.cull, "culling_spheres", cullingSpheres);

            // For some reason unity doesn't like using SetComputeFloatParams. Wtf????
            // has to be yet ANOTHER bug
            // https://discussions.unity.com/t/setcomputefloatparams-does-not-work-with-commandbuffer/1650792
            float[] maxDistances = config.props.Select(type => type.instanceMaxDistance).ToArray();
            cmds.SetGlobalFloatArray("max_distances", maxDistances);
            //cmds.SetComputeFloatParams(config.cull, "max_distances", maxDistances);

            float[] impostorDistancePercentage = config.props.Select(type => type.impostorDistancePercentage).ToArray();
            cmds.SetGlobalFloatArray("impostor_distance_percentage", impostorDistancePercentage);

            const int THREAD_GROUP_SIZE_X = 64;
            const int CULL_INNER_LOOP_SIZE = 32;
            int threadCountX = Mathf.CeilToInt((float)perm.maxCombinedPermProps / (THREAD_GROUP_SIZE_X * CULL_INNER_LOOP_SIZE));
            cmds.DispatchCompute(config.cull, 0, threadCountX, 1, 1);

            cmds.SetComputeBufferParam(config.apply, 0, "instanced_draw_args_buffer", rendering.instancedDrawArgsBuffer);
            cmds.SetComputeBufferParam(config.apply, 0, "impostor_draw_args_buffer", rendering.impostorDrawArgsBuffer);
            cmds.SetComputeBufferParam(config.apply, 0, "visibility_counters_buffer", rendering.visibilityCountersBuffer);
            cmds.DispatchCompute(config.apply, 0, types, 1, 1);

            GraphicsFence fence = Graphics.CreateAsyncGraphicsFence();
            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Default);


            Graphics.WaitOnAsyncGraphicsFence(fence);
            for (int i = 0; i < config.props.Count; i++) {
                if (config.props[i].renderInstances) {
                    RenderInstancedPropsOfType(config.props[i], i);
                }

                if (config.props[i].renderImpostors) {
                    RenderImpostorPropsOfType(cameraTransform, config.props[i], i);
                }
            }
        }

        public void RenderInstancedPropsOfType(PropType type, int i) {
            if (!rendering.typeInstanceTextureArrays[i].IsValid()) {
                Debug.LogWarning($"Missing instanced textures for prop '{type.name}' variant {i}");
                return;
            }

            RenderParams renderParams = new RenderParams(instancedMaterial);
            renderParams.shadowCastingMode = type.renderInstancesShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderParams.rendererPriority = -100;
            renderParams.worldBounds = new Bounds {
                min = -Vector3.one * 100000,
                max = Vector3.one * 100000,
            };

            var mat = new MaterialPropertyBlock();
            renderParams.matProps = mat;
            mat.SetBuffer("_PermBuffer", perm.permBuffer);
            mat.SetBuffer("_PermMatricesBuffer", perm.permMatricesBuffer);
            mat.SetBuffer("_InstancedIndirectionBuffer", rendering.instancedIndirectionBuffer);
            mat.SetTexture("_DiffuseMapArray", rendering.typeInstanceTextureArrays[i].diffuse);
            mat.SetTexture("_NormalMapArray", rendering.typeInstanceTextureArrays[i].normal);
            mat.SetTexture("_MaskMapArray", rendering.typeInstanceTextureArrays[i].mask);
            mat.SetInt("_PermBufferOffset", perm.permBufferOffsets[i]);
            mat.SetInt("_PropType", i);
            mat.SetInt("_MaxVariantCountForType", type.variants.Count);

            Mesh mesh = type.instancedMesh;

            if (mesh == null) {
                Debug.LogWarning($"Missing instanced mesh for prop '{type.name}'");
                return;
            }

            Graphics.RenderMeshIndirect(renderParams, mesh, rendering.instancedDrawArgsBuffer, 1, i);
        }

        public void RenderImpostorPropsOfType(LocalToWorld cameraTransform, PropType type, int i) {
            if (!rendering.typeImpostorsTextureArrays[i].IsValid()) {
                Debug.LogWarning($"Missing captured impostor textures for prop '{type.name}' variant {i}");
                return;
            }

            RenderParams renderParams = new RenderParams(impostorMaterial);
            renderParams.shadowCastingMode = ShadowCastingMode.Off;
            renderParams.rendererPriority = -100;
            renderParams.worldBounds = new Bounds {
                min = -Vector3.one * 100000,
                max = Vector3.one * 100000,
            };

            var mat = new MaterialPropertyBlock();
            renderParams.matProps = mat;
            mat.SetBuffer("_PermBuffer", perm.permBuffer);
            mat.SetBuffer("_ImpostorIndirectionBuffer", rendering.impostorIndirectionBuffer);

            mat.SetTexture("_DiffuseMapArray", rendering.typeImpostorsTextureArrays[i].diffuse);
            mat.SetTexture("_NormalMapArray", rendering.typeImpostorsTextureArrays[i].normal);
            mat.SetTexture("_MaskMapArray", rendering.typeImpostorsTextureArrays[i].mask);

            mat.SetInt("_PermBufferOffset", perm.permBufferOffsets[i]);
            mat.SetInt("_PropType", i);
            mat.SetInt("_MaxVariantCountForType", type.variants.Count);

            mat.SetVector("_CameraPosition", (Vector3)cameraTransform.Position);
            mat.SetVector("_CameraUp", (Vector3)cameraTransform.Up);

            mat.SetVector("_ImpostorOffset", type.impostorOffset);
            mat.SetFloat("_ImpostorScale", type.impostorScale);

            Graphics.RenderMeshIndirect(renderParams, quad, rendering.impostorDrawArgsBuffer, 1, i);
        }
    }
}
using jedjoud.VoxelTerrain.Props;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class VoxelExecutor : VoxelBehaviour {
        [Header("Seeding")]
        public int seed = 1234;
        public Vector3Int permutationSeed;
        public Vector3Int moduloSeed;
        private ComputeBuffer posScaleOctalBuffer;

        [HideInInspector]
        [NonSerialized]
        public Dictionary<string, ExecutorTexture> textures;
        [HideInInspector]
        [NonSerialized]
        public Dictionary<string, ExecutorBuffer> buffers;

        private VoxelCompiler compiler => GetComponent<VoxelCompiler>();

        // Cache the size so that we don't need to re-initialize the texture and buffers
        private int setSize;

        public override void CallerStart() {
            DisposeResources();
        }

        private void OnDisable() {
            DisposeResources();
        }

        public void DisposeResources() {
            if (posScaleOctalBuffer != null) {
                posScaleOctalBuffer.Dispose();
            }

            if (textures != null) {
                foreach (var (name, tex) in textures) {
                    tex.Dispose();
                }

                textures = null;
            }

            if (buffers != null) {
                foreach (var (name, buffer) in buffers) {
                    buffer.Dispose();
                }

                buffers = null;
            }
        }

        // Create intermediate textures (cached, gradient) to be used for voxel graph shader execution
        // Texture size will correspond to execution size property
        private void CreateResources(int size, bool readback) {
            DisposeResources();

            posScaleOctalBuffer = new ComputeBuffer(8, sizeof(float) * 4, ComputeBufferType.Structured);

            /*
            float4[] packed = new float4[8];
            for (int i = 0; i < 8; i++) {
                packed[i] = new Vector4(0f, 0f, 0f, 1f);
            }

            posScaleOctalBuffer.SetData(packed);
            */


            // Creates dictionary with the default voxel graph textures (density + custom data)
            textures = new Dictionary<string, ExecutorTexture> {
                //{ "voxels", new OutputExecutorTexture("voxels", new List<string>() { "CSVoxel" }, TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt), -1) },
            };

            // TODO: for some reason unity thinks there's a memory leak here due to the compute buffers??
            // I dispose of them just like the render textures idk why it's complaining
            buffers = new Dictionary<string, ExecutorBuffer> {
                //{ "voxels", new ExecutorBuffer("voxels", new List<string>() { "CSVoxel" }, new ComputeBuffer(65*65*65, Voxel.size, ComputeBufferType.Structured)) },
                //{ "pos_scale_octals", new ExecutorBuffer("pos_scale_octals", new List<string>() { "CSVoxel" }, new ComputeBuffer(8, sizeof(float) * 4, ComputeBufferType.Constant)) },
                //{ "props", new ExecutorBuffer("props", new List<string>() { "CSProps" }, new ComputeBuffer(VoxelUtils.VOLUME, BlittableProp.size, ComputeBufferType.Structured)) },
                //{ "props_counter", new ExecutorBufferCounter("props_counter", new List<string>() { "CSProps" }, 1) },
            };

            if (readback) {
                buffers.Add("voxels", new ExecutorBuffer("voxels", new List<string>() { "CSVoxel" }, new ComputeBuffer(130 * 130 * 130, Voxel.size, ComputeBufferType.Structured)));
            } else {
                textures.Add("voxels", new OutputExecutorTexture("voxels", new List<string>() { "CSVoxel" }, TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt), -1));
            }

            foreach (var (name, descriptor) in compiler.ctx.textures) {
                textures.Add(name, descriptor.Create(size));
            }

            // TODO: do the same custom texture stuff but with buffers instead!!
        }


        public void ExecuteShader(int newSize, int dispatchIndex, Vector3 offset, Vector3 scale, Vector4[] posScaleOctals = null, bool updateInjected = true) {
            if (compiler.ctx == null) {
                compiler.ParsedTranspilation();
            }

            if (newSize != setSize || textures == null || buffers == null) {
                setSize = newSize;
                CreateResources(newSize, posScaleOctals != null);
            }

            CommandBuffer commands = new CommandBuffer();
            commands.name = "Execute Terrain Generator Dispatches";
            commands.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            ComputeShader shader = compiler.shader;
            
            LocalKeyword keyword = shader.keywordSpace.FindKeyword("_ASYNC_READBACK_OCTAL");
            if (posScaleOctals != null && posScaleOctals.Length == 8) {
                commands.EnableKeyword(shader, keyword);
                commands.SetBufferData(posScaleOctalBuffer, posScaleOctals);
                commands.SetComputeBufferParam(shader, dispatchIndex, "pos_scale_octals", posScaleOctalBuffer);
                //commands.SetComputeVectorArrayParam(shader, "pos_scale_octals", posScaleOctals);
            } else {
                commands.DisableKeyword(shader, keyword);
            }

            commands.SetComputeIntParam(shader, "size", newSize);
            commands.SetComputeIntParams(shader, "permuationSeed", new int[] { permutationSeed.x, permutationSeed.y, permutationSeed.z });
            commands.SetComputeIntParams(shader, "moduloSeed", new int[] { moduloSeed.x, moduloSeed.y, moduloSeed.z });
            commands.SetComputeVectorParam(shader, "offset", offset);
            commands.SetComputeVectorParam(shader, "scale", scale);

            if (updateInjected) {
                compiler.ctx.injector.UpdateInjected(commands, shader, textures);
            }


            foreach (var (name, texture) in textures) {
                texture.PreDispatch(commands, shader);
            }

            foreach (var (name, buffer) in buffers) {
                buffer.PreDispatch(commands, shader);
            }

            foreach (var (name, texture) in textures) {
                texture.BindToComputeShader(commands, shader);
            }

            foreach (var (name, buffer) in buffers) {
                buffer.BindToComputeShader(commands, shader);
            }

            // Execute the kernels sequentially
            // TODO: Add back said kernels since we removed the whole cached node kek
            KernelDispatch kernel =  compiler.ctx.dispatches[dispatchIndex];
            int id = shader.FindKernel(kernel.name);
            int tempSize = newSize;
            tempSize = Mathf.Max(tempSize, 1);

            int minScaleBase3D = Mathf.CeilToInt((float)tempSize / 8.0f);
            commands.DispatchCompute(shader, id, minScaleBase3D, minScaleBase3D, minScaleBase3D);

            // TODO: Dictionary<string, string> kernelsToWriteTexture = new Dictionary<string, string>();
            foreach (var (name, item) in textures) {
                item.PostDispatchKernel(commands, shader, id);
            }

            // This works! Only in the builds, but async compute queue is being utilized!!!
            Graphics.ExecuteCommandBuffer(commands);
            //Graphics.ExecuteCommandBufferAsync(commands, UnityEngine.Rendering.ComputeQueueType.Default);
        }

        private void ComputeSecondarySeeds() {
            var random = new System.Random(seed);
            permutationSeed.x = random.Next(-1000, 1000);
            permutationSeed.y = random.Next(-1000, 1000);
            permutationSeed.z = random.Next(-1000, 1000);
            moduloSeed.x = random.Next(-1000, 1000);
            moduloSeed.y = random.Next(-1000, 1000);
            moduloSeed.z = random.Next(-1000, 1000);
        }

        public void RandomizeSeed() {
            seed = UnityEngine.Random.Range(-9999, 9999);
            ComputeSecondarySeeds();
            compiler.OnPropertiesChanged();
        }
    }
}
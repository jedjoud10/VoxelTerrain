using jedjoud.VoxelTerrain.Props;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class VoxelExecutor : VoxelBehaviour {
        [Header("Seeding")]
        public int seed = 1234;
        public Vector3Int permutationSeed;
        public Vector3Int moduloSeed;

        [HideInInspector]
        [NonSerialized]
        public Dictionary<string, ExecutorTexture> textures;
        [HideInInspector]
        [NonSerialized]
        public Dictionary<string, ExecutorBuffer> buffers;

        // Cache the size so that we don't need to re-initialize the texture and buffers
        private int setSize;

        public override void CallerStart() {
            DisposeResources();
        }

        private void OnDisable() {
            DisposeResources();
        }

        public void DisposeResources() {
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
        private void CreateIntermediateTextures(int size) {
            DisposeResources();

            // Creates dictionary with the default voxel graph textures (density + custom data)
            textures = new Dictionary<string, ExecutorTexture> {
                { "voxels", new OutputExecutorTexture("voxels", new List<string>() { "CSVoxel" }, TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt), -1) },
            };

            // TODO: for some reason unity thinks there's a memory leak here due to the compute buffers??
            // I dispose of them just like the render textures idk why it's complaining
            buffers = new Dictionary<string, ExecutorBuffer> {
                { "props", new ExecutorBuffer("props", new List<string>() { "CSProps" }, new ComputeBuffer(VoxelUtils.VOLUME, BlittableProp.size, ComputeBufferType.Structured)) },
                { "props_counter", new ExecutorBufferCounter("props_counter", new List<string>() { "CSProps" }, 1) }
            };

            foreach (var (name, descriptor) in graph.ctx.textures) {
                textures.Add(name, descriptor.Create(size));
            }

            // TODO: do the same custom texture stuff but with buffers instead!!
        }


        public void ExecuteShader(int newSize, int dispatchIndex, Vector3 offset, Vector3 scale, bool morton, bool updateInjected) {
            if (graph.ctx == null) {
                graph.ParsedTranspilation();
            }

            if (newSize != setSize || textures == null) {
                setSize = newSize;
                CreateIntermediateTextures(newSize);
            }

            ComputeShader shader = graph.shader;
            shader.SetInt("size", newSize);
            shader.SetInts("permuationSeed", new int[] { permutationSeed.x, permutationSeed.y, permutationSeed.z });
            shader.SetInts("moduloSeed", new int[] { moduloSeed.x, moduloSeed.y, moduloSeed.z });
            shader.SetVector("offset", offset);
            shader.SetVector("scale", scale);
            shader.SetBool("morton", morton);

            if (updateInjected) {
                graph.ctx.injector.UpdateInjected(shader, textures);
            }

            CommandBuffer commands = new CommandBuffer();
            commands.name = "Execute Terrain Generator Dispatches";
            commands.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

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
            KernelDispatch kernel =  graph.ctx.dispatches[dispatchIndex];
            int id = shader.FindKernel(kernel.name);
            int tempSize = newSize / (1 << kernel.sizeReductionPower);
            tempSize = Mathf.Max(tempSize, 1);

            int minScaleBase3D = Mathf.CeilToInt((float)tempSize / 8.0f);
            int minScaleBase2D = Mathf.CeilToInt((float)tempSize / 32.0f);

            if (kernel.threeDimensions) {
                commands.DispatchCompute(shader, id, minScaleBase3D, minScaleBase3D, minScaleBase3D);
            } else {
                commands.DispatchCompute(shader, id, minScaleBase2D, minScaleBase2D, 1);
            }

            // TODO: Dictionary<string, string> kernelsToWriteTexture = new Dictionary<string, string>();
            foreach (var (name, item) in textures) {
                item.PostDispatchKernel(commands, shader, id);
            }

            // TODO: figure out how 2 do this pls???
            Graphics.ExecuteCommandBufferAsync(commands, UnityEngine.Rendering.ComputeQueueType.Background);
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
            graph.OnPropertiesChanged();
        }
    }
}
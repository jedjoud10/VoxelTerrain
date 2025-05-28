using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public abstract class ExecutorParameters {
        public string dispatchName;
        public bool updateInjected;
        public ManagedTerrainCompiler compiler;
        public ManagedTerrainSeeder seeder;
    }

    public abstract class Executor<P> where P : ExecutorParameters {
        protected int size;
        protected Dictionary<string, ExecutorTexture> textures;
        protected Dictionary<string, ExecutorBuffer> buffers;

        public Dictionary<string, ExecutorTexture> Textures { get { return textures; } }
        public Dictionary<string, ExecutorBuffer> Buffers { get { return buffers; } }

        public Executor(int size) {
            if (size <= 0) {
                throw new ArgumentException("Size must be a positive non-zero number");
            }

            this.size = size;
        }

        protected abstract void CreateMainResources();
        protected abstract void ExecuteSetCommands(CommandBuffer commands, ComputeShader shader, P parameters, int dispatchIndex);

        private void CreateResources(ManagedTerrainCompiler compiler) {
            DisposeResources();

            // TODO: for some reason unity thinks there's a memory leak here due to the compute buffers??
            textures = new Dictionary<string, ExecutorTexture>();
            buffers = new Dictionary<string, ExecutorBuffer>();

            foreach (var (name, descriptor) in compiler.ctx.textures) {
                textures.Add(name, descriptor.Create(size));
            }

            CreateMainResources();
        }

        public GraphicsFence Execute(P parameters) {
            ManagedTerrainCompiler compiler = parameters.compiler;
            ManagedTerrainSeeder seeder = parameters.seeder;

            int dispatchIndex = compiler.DispatchIndices[parameters.dispatchName];
            bool updateInjected = parameters.updateInjected;

            if (compiler.ctx == null) {
                compiler.ParsedTranspilation();
            }

            if (textures == null || buffers == null) {
                CreateResources(compiler);
            }

            CommandBuffer commands = new CommandBuffer();
            commands.name = "Execute Terrain Generator Dispatches";
            commands.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            ComputeShader shader = compiler.shader;

            ExecuteSetCommands(commands, shader, parameters, dispatchIndex);


            commands.SetComputeIntParam(shader, "size", size);
            commands.SetComputeIntParams(shader, "permuationSeed", new int[] { seeder.permutationSeed.x, seeder.permutationSeed.y, seeder.permutationSeed.z });
            commands.SetComputeIntParams(shader, "moduloSeed", new int[] { seeder.moduloSeed.x, seeder.moduloSeed.y, seeder.moduloSeed.z });

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
            KernelDispatch kernel = compiler.ctx.dispatches[dispatchIndex];
            int id = shader.FindKernel(kernel.name);

            int tempSize = size;
            tempSize = Mathf.Max(size, 1);
            int minScaleBase3D = Mathf.CeilToInt((float)tempSize / 8.0f);
            commands.DispatchCompute(shader, id, minScaleBase3D, minScaleBase3D, minScaleBase3D);

            // TODO: Dictionary<string, string> kernelsToWriteTexture = new Dictionary<string, string>();
            foreach (var (name, item) in textures) {
                item.PostDispatchKernel(commands, shader, id);
            }

            // This works! Only in the builds, but async compute queue is being utilized!!!
            // If the target arch doesn't support async compute this will just revert to the normal queue
            GraphicsFence fence = commands.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
            Graphics.ExecuteCommandBufferAsync(commands, UnityEngine.Rendering.ComputeQueueType.Background);
            return fence;
        }

        public virtual void DisposeResources() {
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

        ~Executor() {
            DisposeResources();
        }
    }
}

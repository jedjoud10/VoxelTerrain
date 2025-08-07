using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public abstract class ExecutorParameters {
        public string kernelName;
        public string commandBufferName;
        public bool updateInjected;
        public ManagedTerrainCompiler compiler;
        public TerrainSeed seed;
    }

    public abstract class Executor<P> where P : ExecutorParameters {
        protected Dictionary<string, ExecutorTexture> textures;
        protected Dictionary<string, ExecutorBuffer> buffers;

        public Dictionary<string, ExecutorTexture> Textures { get { return textures; } }
        public Dictionary<string, ExecutorBuffer> Buffers { get { return buffers; } }

        protected virtual void CreateResources (ManagedTerrainCompiler compiler) {
            DisposeResources();

            // TODO: for some reason unity thinks there's a memory leak here due to the compute buffers??
            textures = new Dictionary<string, ExecutorTexture>();
            buffers = new Dictionary<string, ExecutorBuffer>();

            foreach (var (name, descriptor) in compiler.ctx.textures) {
                textures.Add(name, descriptor.Create());
            }
        }


        int3 permutationSeed = int3.zero;
        int3 moduloSeed = int3.zero;

        protected virtual void SetComputeParams(CommandBuffer commands, ComputeShader shader, P parameters, int kernelIndex) {
            commands.SetComputeIntParams(shader, "permutation_seed", new int[] { permutationSeed.x, permutationSeed.y, permutationSeed.z });
            commands.SetComputeIntParams(shader, "modulo_seed", new int[] { moduloSeed.x, moduloSeed.y, moduloSeed.z });
        }


        public GraphicsFence ExecuteWithInvocationCount(int3 invocations, P parameters, GraphicsFence? previous = null) {
            if (parameters == null)
                throw new ArgumentNullException("Missing execution parameters");

            permutationSeed = parameters.seed.permutationSeed;
            moduloSeed = parameters.seed.moduloSeed;

            ManagedTerrainCompiler compiler = parameters.compiler;

            if (compiler == null)
                throw new ArgumentNullException("Compiler not set or missing");

            if (compiler.ctx == null)
                throw new ArgumentNullException("Compiler context missing (need to add more ParsedTranspilation() guards oops...)");

            // dawg...
            ComputeShader shader = compiler.shader;
            KernelDispatch dispatch = compiler.ctx.dispatches.Find(x => x.name == parameters.kernelName);
            
            int id = shader.FindKernel(parameters.kernelName);
            bool updateInjected = parameters.updateInjected;

            if (compiler.ctx == null) {
                compiler.Parse();
            }

            if (textures == null || buffers == null) {
                CreateResources(compiler);

                // Initializing the values will force us to update them
                updateInjected = true;
            }

            CommandBuffer commands = new CommandBuffer();
            commands.name = parameters.commandBufferName;
            commands.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            if (previous != null) {
                commands.WaitOnAsyncGraphicsFence(previous.Value, SynchronisationStageFlags.ComputeProcessing);
            }


            SetComputeParams(commands, shader, parameters, id);

            if (updateInjected) {
                compiler.ctx.injector.UpdateInjected(commands, shader, textures);
            }

            foreach (var (name, texture) in textures) {
                texture.BindToComputeShader(commands, shader);
            }

            foreach (var (name, buffer) in buffers) {
                buffer.BindToComputeShader(commands, shader);
            }

            float3 numThreads = (float3)dispatch.numThreads;
            float3 tempSize = (float3)invocations / numThreads;
            int3 threadGroups = (int3)math.ceil(math.max(tempSize, 1));
            commands.DispatchCompute(shader, id, threadGroups.x, threadGroups.y, threadGroups.z);

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

    public abstract class VolumeExecutor<P>: Executor<P> where P : ExecutorParameters {
        protected int size;
        public VolumeExecutor(int size) {
            if (size <= 0) {
                throw new ArgumentException("Size must be a positive non-zero number");
            }

            this.size = size;
        }

        public GraphicsFence Execute(P parameters, GraphicsFence? previous = null) {
            return ExecuteWithInvocationCount(new int3(size), parameters, previous);
        }

        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, P parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, parameters, kernelIndex);
            commands.SetComputeIntParam(shader, "size", size);
        }
    }
}

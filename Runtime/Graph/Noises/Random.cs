using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public class RandomNode<I, O> : Variable<O> {
        public Variable<I> input;
        public bool signed;
        public uint seed;

        public override void HandleInternal(TreeContext ctx) {
            input.Handle(ctx);
            int inputDims = VariableType.Dimensionality<I>();
            int outputDims = VariableType.Dimensionality<O>();

            float4 compilerTimeOffset = ctx.GetRngVarOffset(seed);
            string compilerTimeOffsetCode = GraphUtils.SwizzleFromFloat4<I>(compilerTimeOffset);

            string code = $"hash{outputDims}{inputDims}({ctx[input]} + {compilerTimeOffsetCode})";

            if (signed) {
                code = $"(2.0 * ({code} - 0.5))";
            }

            ctx.DefineAndBindNode<O>(this, "rng", code);
        }
    }

    public class Random {
        public static Variable<O> Evaluate<I, O>(Variable<I> input, bool signed, uint seed = uint.MaxValue) {
            return (new RandomNode<I, O> {
                input = input,
                signed = signed,
                seed = seed
            });
        }

        public static Variable<bool> Uniform<I>(Variable<I> input, Variable<float> probability, uint seed = uint.MaxValue) {
            return Evaluate<I, float>(input, false, seed) > (1 - probability);
        }

        internal static Variable<O> Range<I, O>(Variable<I> input, Variable<O> lower, Variable<O> upper) {
            var ampl = upper - lower;
            var offset = lower;
            var temp = Evaluate<I, O>(input, false);
            return temp * ampl + offset;
        }
    }
}
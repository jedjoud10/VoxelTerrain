using System;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class FractalNode<T> : Variable<float> {
        public Variable<T> position;
        public Fractal<T>.Fold fold;
        public Fractal<T>.Inner inner;
        public Fractal<T>.Remap remap;
        public Variable<float> lacunarity;
        public Variable<float> persistence;
        public int octaves;

        public override void HandleInternal(TreeContext context) {
            lacunarity.Handle(context);
            position.Handle(context);
            persistence.Handle(context);

            int actualOctaves = Mathf.Max(octaves, 0);
            context.Hash(actualOctaves);

            Variable<float> sum = context.AssignTempVariable<float>($"{context[position]}_fbm", "0.0");
            Variable<float> fbm_scale = context.AssignTempVariable<float>($"{context[position]}_fbm_scale", "1.0");
            Variable<float> fbm_amplitude = context.AssignTempVariable<float>($"{context[position]}_fbm_amplitude", "1.0");

            context.AddLine("[unroll]");
            context.AddLine($"for(uint i = 0; i < {actualOctaves}; i++) {{");
            context.Indent++;

            int dimensionality = VariableType.Dimensionality<T>();
            string hashOffset = $"hash{dimensionality}1(float(i) * 6543.26912) * 2366.5437";

            Variable<T> fbmed = context.AssignTempVariable<T>($"{context[position]}_fmb_pos", $"{context[position]} * {context[fbm_scale]} + {hashOffset}");
            Variable<float> innerVar = inner(fbmed * fbm_scale.Broadcast<T>());
            Variable<float> remapped = remap(innerVar);
            Variable<float> foldedVar = fold(sum, remapped * fbm_amplitude);
            foldedVar.Handle(context);
            context.AddLine($"{context[sum]} = {context[foldedVar]};");

            context.AddLine($"{context[fbm_scale]} *= {context[lacunarity]};");
            context.AddLine($"{context[fbm_amplitude]} *= {context[persistence]};");

            context.Indent--;
            context.AddLine("}");

            context.DefineAndBindNode<float>(this, $"{context[position]}_fbm", context[sum]);
        }
    }

    class CreatePreFoldRemapFromModeNode<T> : Variable<float> {
        public FractalMode mode;
        public Variable<float> upperBound;
        public Variable<float> current;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(mode);
            upperBound.Handle(ctx);
            current.Handle(ctx);

            string huh = "";
            switch (mode) {
                case FractalMode.Ridged:
                    huh = $"2 * abs({ctx[current]}) - abs({ctx[upperBound]})";
                    break;
                case FractalMode.Billow:
                    huh = $"-(2 * abs({ctx[current]}) - abs({ctx[upperBound]}))";
                    break;
                case FractalMode.Sum:
                    huh = $"{ctx[current]}";
                    break;
            }

            ctx.DefineAndBindNode<float>(this, "huh", huh);
        }
    }

    public enum FractalMode {
        Ridged,
        Billow,
        Sum,
    }

    public class Fractal<T> {
        public delegate Variable<float> Inner(Variable<T> position);
        public delegate Variable<float> Fold(Variable<float> last, Variable<float> current);
        public delegate Variable<float> Remap(Variable<float> current);

        public Inner inner;
        public Fold fold;
        public Remap remap;

        public Variable<float> persistence;
        public Variable<float> lacunarity;
        public int octaves;

        public static Remap CreatePreFoldRemapFromNoise(AbstractNoise noise, FractalMode mode) {
            return (current) => new CreatePreFoldRemapFromModeNode<T>() { current = current, mode = mode, upperBound = noise.CreateAbstractYetToEval<T>().amplitude };
        }

        public Fractal(AbstractNoise noise, FractalMode mode, int octaves, Variable<float> lacunarity = null, Variable<float> persistence = null) {
            this.lacunarity = lacunarity;
            this.persistence = persistence;
            this.inner = (Variable<T> position) => { return noise.Evaluate(position); };
            this.remap = CreatePreFoldRemapFromNoise(noise, mode);
            this.octaves = octaves;
        }

        public Fractal(AbstractNoise noise, FractalMode mode, int octaves, Variable<float2> others = null) {
            this.lacunarity = others.Swizzle<float>("x");
            this.persistence = others.Swizzle<float>("y");
            this.inner = (Variable<T> position) => { return noise.Evaluate(position); };
            this.remap = CreatePreFoldRemapFromNoise(noise, mode);
            this.octaves = octaves;
        }

        public Fractal(Inner inner, int octaves, Variable<float> lacunarity = null, Variable<float> persistence = null, Fold fold = null, Remap remap = null) {
            this.lacunarity = lacunarity;
            this.persistence = persistence;
            this.inner = inner;
            this.fold = (last, current) => last + current;
            this.octaves = octaves;
            this.fold = fold;
            this.remap = remap;
        }

        public Variable<float> Evaluate(Variable<T> position) {
            if (inner == null) {
                throw new Exception("Inner function for fractal accumulator is null");
            }

            return new FractalNode<T> {
                inner = inner,
                fold = fold ?? ((current, last) => current + last),
                remap = remap ?? ((current) => current),
                lacunarity = lacunarity ?? 2.0f,
                persistence = persistence ?? 0.5f,
                octaves = octaves,
                position = position
            };
        }

        public static Variable<float> Evaluate(Variable<T> position, AbstractNoise noise, FractalMode mode, int octaves, Variable<float> lacunarity = null, Variable<float> persistence = null) {
            Fractal<T> test = new Fractal<T>(noise, mode, octaves, lacunarity, persistence);
            return test.Evaluate(position);
        }

        public static Variable<float> Evaluate(Variable<T> position, Inner inner, int octaves, Variable<float> lacunarity = null, Variable<float> persistence = null, Fold fold = null, Remap remap = null) {
            Fractal<T> test = new Fractal<T>(inner, octaves, lacunarity, persistence, fold, remap);
            return test.Evaluate(position);
        }
    }
}
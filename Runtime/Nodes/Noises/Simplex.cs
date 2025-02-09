using System;

namespace jedjoud.VoxelTerrain.Generation {
    public class SimplexNode<T> : AbstractNoiseNode<T> {
        public override object Clone() {
            return new SimplexNode<T> {
                amplitude = this.amplitude,
                scale = this.scale,
                position = this.position
            };
        }

        public override void HandleInternal(TreeContext context) {
            base.HandleInternal(context);
            string inner = $"({context[position]}) * {context[scale]}";
            string value = $"(snoise({inner})) * {context[amplitude]}";
            context.DefineAndBindNode<float>(this, $"{context[position]}_noised", value);
        }
    }

    public class Simplex : Noise {
        public Variable<float> amplitude;
        public Variable<float> scale;

        public Simplex() {
            amplitude = 1.0f;
            scale = 0.01f;
        }

        public Simplex(Variable<float> scale, Variable<float> amplitude) {
            this.amplitude = amplitude;
            this.scale = scale;
        }

        public override AbstractNoiseNode<I> CreateAbstractYetToEval<I>() {
            return new SimplexNode<I>() {
                amplitude = amplitude,
                scale = scale,
                position = null,
            };
        }

        public override Variable<float> Evaluate<T>(Variable<T> position) {
            var type = GraphUtils.TypeOf<T>();
            if (type != GraphUtils.StrictType.Float2 && type != GraphUtils.StrictType.Float3) {
                throw new Exception("Type not supported");
            }

            AbstractNoiseNode<T> a = CreateAbstractYetToEval<T>();
            a.position = position;
            return a;
        }
    }
}
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

    public class Simplex<T> : AbstractNoise<T> {
        public Variable<float> amplitude;
        public Variable<T> scale;

        public Simplex() {
            amplitude = 1.0f;
            scale = GraphUtils.One<T>() * (Variable<float>.Const(0.01f)).Broadcast<T>();
        }

        public Simplex(Variable<float> scale, Variable<float> amplitude) {
            this.amplitude = amplitude;
            this.scale = scale.Broadcast<T>();
        }

        public Simplex(Variable<T> scale, Variable<float> amplitude) {
            this.amplitude = amplitude;
            this.scale = scale;
        }

        public override AbstractNoiseNode<T> CreateAbstractYetToEval() {
            return new SimplexNode<T>() {
                amplitude = amplitude,
                scale = scale,
                position = null,
            };
        }

        public override Variable<float> Evaluate(Variable<T> position) {
            var type = VariableType.TypeOf<T>();
            if (type.strict != VariableType.StrictType.Float2 && type.strict != VariableType.StrictType.Float3) {
                throw new Exception("Type not supported");
            }

            AbstractNoiseNode<T> a = CreateAbstractYetToEval();
            a.position = position;
            return a;
        }
    }
}
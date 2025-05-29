using System;

namespace jedjoud.VoxelTerrain.Generation {
    public class VoronoiNode<T> : AbstractNoiseNode<T> {
        public override object Clone() {
            return new VoronoiNode<T> {
                amplitude = this.amplitude,
                scale = this.scale,
                position = this.position,
                type = this.type,
            };
        }

        public Voronoi<T>.Type type;


        public override void HandleInternal(TreeContext context) {
            base.HandleInternal(context);
            context.Hash(type);
            string suffix = "";
            string fn = "";

            switch (type) {
                case Voronoi<T>.Type.F1:
                    fn = "cellular";
                    suffix = ".x - 0.5";
                    break;
                case Voronoi<T>.Type.F2:
                    fn = "cellular";
                    suffix = ".y - 0.5";
                    break;
            }

            string inner = $"({context[position]}) * {context[scale]}";
            string value = $"({fn}({inner}){suffix}) * {context[amplitude]}";
            context.DefineAndBindNode<float>(this, $"{context[position]}_noised", value);
        }
    }
    public class Voronoi<T> : AbstractNoise<T> {
        public Variable<float> amplitude;
        public Variable<T> scale;
        public Type type;

        public enum Type {
            F1,
            F2,
        }

        public Voronoi() {
            this.amplitude = 1.0f;
            scale = GraphUtils.One<T>() * (Variable<float>.New(0.01f)).Broadcast<T>();
            this.type = Type.F1;
        }

        public Voronoi(Variable<float> scale, Variable<float> amplitude, Type type = Type.F1) {
            this.amplitude = amplitude;
            this.scale = scale.Broadcast<T>();
            this.type = type;
        }

        public Voronoi(Variable<T> scale, Variable<float> amplitude, Type type = Type.F1) {
            this.amplitude = amplitude;
            this.scale = scale;
            this.type = type;
        }

        public override Variable<float> Evaluate(Variable<T> position) {
            var type2 = VariableType.TypeOf<T>();
            if (type2.strict != VariableType.StrictType.Float2 && type2.strict != VariableType.StrictType.Float3) {
                throw new Exception("Type not supported");
            }

            AbstractNoiseNode<T> a = CreateAbstractYetToEval();
            a.position = position;
            return a;
        }

        public override AbstractNoiseNode<T> CreateAbstractYetToEval() {
            return new VoronoiNode<T>() {
                amplitude = amplitude,
                scale = scale,
                position = null,
                type = type,
            };
        }
    }
}
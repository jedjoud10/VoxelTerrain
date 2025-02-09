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

        public Voronoi.Type type;


        public override void HandleInternal(TreeContext context) {
            base.HandleInternal(context);
            context.Hash(type);
            string suffix = "";
            string fn = "";

            switch (type) {
                case Voronoi.Type.F1:
                    fn = "cellular";
                    suffix = ".x - 0.5";
                    break;
                case Voronoi.Type.F2:
                    fn = "cellular";
                    suffix = ".y - 0.5";
                    break;
            }

            string inner = $"({context[position]}) * {context[scale]}";
            string value = $"({fn}({inner}){suffix}) * {context[amplitude]}";
            context.DefineAndBindNode<float>(this, $"{context[position]}_noised", value);
        }
    }
    public class Voronoi : Noise {
        public Variable<float> amplitude;
        public Variable<float> scale;
        public Type type;

        public enum Type {
            F1,
            F2,
        }

        public Voronoi() {
            this.amplitude = 1.0f;
            this.scale = 0.01f;
            this.type = Type.F1;
        }

        public Voronoi(Variable<float> scale, Variable<float> amplitude) {
            this.amplitude = amplitude;
            this.scale = scale;
            this.type = Type.F1;
        }

        public Voronoi(float scale = 0.01f, float amplitude = 1.0f, Type type = Type.F1) {
            this.amplitude = amplitude;
            this.scale = scale;
            this.type = type;
        }

        public override Variable<float> Evaluate<T>(Variable<T> position) {
            var type2 = GraphUtils.TypeOf<T>();
            if (type2 != GraphUtils.StrictType.Float2 && type2 != GraphUtils.StrictType.Float3) {
                throw new Exception("Type not supported");
            }

            AbstractNoiseNode<T> a = CreateAbstractYetToEval<T>();
            a.position = position;
            return a;
        }

        public override AbstractNoiseNode<I> CreateAbstractYetToEval<I>() {
            return new VoronoiNode<I>() {
                amplitude = amplitude,
                scale = scale,
                position = null,
                type = type,
            };
        }
    }
}
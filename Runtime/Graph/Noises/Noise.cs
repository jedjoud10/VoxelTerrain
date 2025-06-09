using System;


namespace jedjoud.VoxelTerrain.Generation {
    public abstract class AbstractNoiseNode<I> : Variable<float>, ICloneable {
        public Variable<float> amplitude;
        public Variable<I> scale;
        public Variable<I> position;

        public abstract object Clone();

        public override void HandleInternal(TreeContext context) {
            amplitude.Handle(context);
            scale.Handle(context);
            position.Handle(context);
        }
    }

    public abstract class AbstractNoise<T> {
        public abstract AbstractNoiseNode<T> CreateAbstractYetToEval();
        public abstract Variable<float> Evaluate(Variable<T> position);
    }

    public static class Noise {
        public static Variable<float> Simplex<T>(Variable<T> input, Variable<T> scale, Variable<float> amplitude) {
            return new Simplex<T>(scale, amplitude).Evaluate(input);
        }

        public static Variable<float> Simplex<T>(Variable<T> input, Variable<float> scale, Variable<float> amplitude) {
            return new Simplex<T>(scale, amplitude).Evaluate(input);
        }

        public static Variable<float> VoronoiF1<T>(Variable<T> input, Variable<T> scale, Variable<float> amplitude) {
            return new Voronoi<T>(scale, amplitude, VoronoiType.F1).Evaluate(input);
        }

        public static Variable<float> VoronoiF2<T>(Variable<T> input, Variable<T> scale, Variable<float> amplitude) {
            return new Voronoi<T>(scale, amplitude, VoronoiType.F2).Evaluate(input);
        }

        public static Variable<float> VoronoiF1<T>(Variable<T> input, Variable<float> scale, Variable<float> amplitude) {
            return new Voronoi<T>(scale, amplitude, VoronoiType.F1).Evaluate(input);
        }

        public static Variable<float> VoronoiF2<T>(Variable<T> input, Variable<float> scale, Variable<float> amplitude) {
            return new Voronoi<T>(scale, amplitude, VoronoiType.F2).Evaluate(input);
        }

        public static Variable<O> Random<I, O>(Variable<I> input, bool signed = false) {
            return Generation.Random.Evaluate<I, O>(input, signed);
        }
    }
}
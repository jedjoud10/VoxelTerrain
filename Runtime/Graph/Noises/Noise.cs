using System;
using Unity.Mathematics;


namespace jedjoud.VoxelTerrain.Generation {
    public abstract class AbstractNoiseNode<I> : Variable<float>, ICloneable {
        public Variable<float> amplitude;
        public Variable<float3> scale;
        public Variable<I> position;

        public abstract object Clone();

        public override void HandleInternal(TreeContext context) {
            amplitude.Handle(context);
            scale.Handle(context);
            position.Handle(context);
        }
    }

    public abstract class AbstractNoise {
        public abstract AbstractNoiseNode<I> CreateAbstractYetToEval<I>();
        public abstract Variable<float> Evaluate<T>(Variable<T> position);
    }

    public static class Noise {
        public static Variable<float> Simplex<T>(Variable<T> input, float scale, float amplitude) {
            return new Simplex(scale, amplitude).Evaluate(input);
        }

        public static Variable<float> Simplex<T>(Variable<T> input, float3 scale, float amplitude) {
            return new Simplex(scale, amplitude).Evaluate(input);
        }

        public static Variable<float> VoronoiF1<T>(Variable<T> input, float scale, float amplitude) {
            return new Voronoi(scale, amplitude, Voronoi.Type.F1).Evaluate(input);
        }

        public static Variable<float> VoronoiF2<T>(Variable<T> input, float scale, float amplitude) {
            return new Voronoi(scale, amplitude, Voronoi.Type.F2).Evaluate(input);
        }

        public static Variable<float> VoronoiF1<T>(Variable<T> input, float3 scale, float amplitude) {
            return new Voronoi(scale, amplitude, Voronoi.Type.F1).Evaluate(input);
        }

        public static Variable<float> VoronoiF2<T>(Variable<T> input, float3 scale, float amplitude) {
            return new Voronoi(scale, amplitude, Voronoi.Type.F2).Evaluate(input);
        }

        public static Variable<O> Random<I, O>(Variable<I> input, bool signed = false) {
            return jedjoud.VoxelTerrain.Generation.Random.Evaluate<I, O>(input, signed);
        }
    }
}
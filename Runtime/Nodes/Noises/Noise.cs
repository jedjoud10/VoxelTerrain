using System;


namespace jedjoud.VoxelTerrain.Generation {
    public abstract class AbstractNoiseNode<I> : Variable<float>, ICloneable {
        public Variable<float> amplitude;
        public Variable<float> scale;
        public Variable<I> position;

        public abstract object Clone();

        public override void HandleInternal(TreeContext context) {
            amplitude.Handle(context);
            scale.Handle(context);
            position.Handle(context);
        }
    }

    public abstract class Noise {
        public abstract AbstractNoiseNode<I> CreateAbstractYetToEval<I>();
        public abstract Variable<float> Evaluate<T>(Variable<T> position);

        public static Variable<float> Simplex<T>(Variable<T> position, Variable<float> scale, Variable<float> amplitude) {
            return new Simplex { amplitude = amplitude, scale = scale }.Evaluate(position);
        }
    }
}
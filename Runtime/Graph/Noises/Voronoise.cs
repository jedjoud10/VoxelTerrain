using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public class VoronoiseNode : AbstractNoiseNode<float2> {
        public Variable<float> lerpValue;
        public Variable<float> randomness;

        public override object Clone() {
            return new VoronoiseNode {
                amplitude = this.amplitude,
                scale = this.scale,
                position = this.position,
                lerpValue = this.lerpValue,
                randomness = this.randomness
            };
        }

        public override void HandleInternal(TreeContext context) {
            lerpValue.Handle(context);
            randomness.Handle(context);
            base.HandleInternal(context);
            string inner = $"({context[position]}) * {context[scale]}";
            string value = $"(voronoise({inner}, {context[randomness]}, {context[lerpValue]})) * {context[amplitude]}";
            context.DefineAndBindNode<float>(this, $"{context[position]}_noised", value);
        }
    }
    public class Voronoise : AbstractNoise<float2> {
        public Variable<float> amplitude;
        public Variable<float> scale;
        public Variable<float> lerpValue;
        public Variable<float> randomness;

        public Voronoise() {
            this.amplitude = 1.0f;
            this.scale = 0.01f;
            this.lerpValue = 0.5f;
            this.randomness = 0.5f;
        }

        public Voronoise(Variable<float> scale, Variable<float> amplitude, Variable<float> lerpValue = null, Variable<float> randomness = null) {
            this.amplitude = amplitude;
            this.scale = scale;
            this.lerpValue = lerpValue ?? 0.5f;
            this.randomness = randomness ?? 0.5f;
        }

        public override AbstractNoiseNode<float2> CreateAbstractYetToEval() {
            return new VoronoiseNode() {
                amplitude = amplitude,
                scale = scale.Broadcast<float2>(),
                position = null,
                randomness = randomness,
                lerpValue = lerpValue,
            };
        }

        public override Variable<float> Evaluate(Variable<float2> position) {
            AbstractNoiseNode<float2> a = CreateAbstractYetToEval();
            a.position = position;
            return a;
        }
    }
}
using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public class WarperNode<T> : Variable<T> {
        public Warper<T>.Warping warping;
        public Variable<T> axialScale;
        public Variable<T> axialAmplitude;
        public Variable<T> position;

        public float3 offsets_x = new float3(123.85441f, 32.223543f, -359.48534f);
        public float3 offsets_y = new float3(65.4238f, -551.15353f, 159.5435f);
        public float3 offsets_z = new float3(-43.85454f, -3346.234f, 54.7653f);

        public override void HandleInternal(TreeContext context) {
            axialScale.Handle(context);
            axialAmplitude.Handle(context);
            position.Handle(context);
            float3[] arr = new float3[] { offsets_x, offsets_y, offsets_z };

            int dimensionality = VariableType.Dimensionality<T>();

            if (dimensionality != 2 && dimensionality != 3) {
                throw new System.Exception();
            }

            string[] swizzleAxii = new string[] { "x", "y", "z" };
            string swizzleTest = dimensionality == 2 ? "xy" : "xyz";

            Variable<float>[] created = new Variable<float>[dimensionality];

            for (int i = 0; i < dimensionality; i++) {
                string swizzle = swizzleAxii[i];
                Variable<T> offsetted = context.AssignTempVariable<T>($"{context[position]}_{swizzle}_offset", $"(({context[position]} + {arr[i]}.{swizzleTest}) * {context[axialScale]}.{swizzle})");
                
                var warped = warping(offsetted);
                warped.Handle(context);
                
                Variable<float> a2 = context.AssignTempVariable<float>($"{context[position]}_warped_{swizzle}", $"({context[position]}.{swizzle} + {context[warped]} * {context[axialAmplitude]}.{swizzle})");
                created[i] = a2;
            }

            ConstructNode<T> constructNode = new ConstructNode<T>() { inputs = created };
            constructNode.Handle(context);

            context.DefineAndBindNode<T>(this, $"{context[position]}_warped", context[constructNode]);
        }
    }

    public class Warper<T> {
        public delegate Variable<float> Warping(Variable<T> input);

        public Variable<T> axialScale;
        public Variable<T> axialAmplitude;
        public Warping warping;

        public Warper(Noise noise, Variable<T> axialScale = null, Variable<T> axialAmplitude = null) {
            this.warping = (Variable<T> input) => {
                var test = (AbstractNoiseNode<T>)noise.CreateAbstractYetToEval<T>().Clone();
                test.position = input;
                return test;
            };
            this.axialAmplitude = axialAmplitude;
            this.axialScale = axialScale;
        }

        public Warper(Warping warping, Variable<T> axialScale = null, Variable<T> axialAmplitude = null) {
            this.warping = warping;
            this.axialAmplitude = axialAmplitude;
            this.axialScale = axialScale;
        }

        public Variable<T> Warpinate(Variable<T> position) {
            if (warping == null) {
                throw new NullReferenceException("Warp function not defined");
            }

            return new WarperNode<T> {
                warping = warping,
                axialAmplitude = axialAmplitude != null ? axialAmplitude : GraphUtils.One<T>(),
                axialScale = axialScale != null ? axialScale : GraphUtils.One<T>(),
                position = position,
            };
        }
    }
}
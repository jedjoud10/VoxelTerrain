using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public static class VariableExtensions {
        public static Variable<float4> Gradient(this Variable<float> mixer, UnityEngine.Gradient gradient, int size = 128) {
            if (gradient == null) {
                throw new NullReferenceException("Unity Gradient is not set");
            }

            return new ColorGradientNode {
                gradient = gradient,
                mixer = mixer,
                size = size,
                inner = new GeneratedTextureHelper(),
            };
        }

        public static Variable<float> Curve(this Variable<float> mixer, UnityEngine.AnimationCurve curve, Variable<float> inputMin, Variable<float> inputMax, int size = 128, bool invert = false) {
            if (curve == null) {
                throw new NullReferenceException("Unity AnimationCurve is not set");
            }

            if (inputMin == null || inputMax == null) {
                throw new NullReferenceException("Curve inputMin and inputMax need to be set");
            }

            return new CurveNode {
                curve = curve,
                mixer = mixer,
                size = size,
                inputMin = inputMin,
                inputMax = inputMax,
                invert = invert,
                inner = new GeneratedTextureHelper(),
            };
        }

        public static Variable<T> Select<T>(this Variable<bool> self, Variable<T> falseVal, Variable<T> trueVal) {
            return new SelectorNode<T>() { falseVal = falseVal, trueVal = trueVal, selector = self };
        }

        public static Variable<O> Broadcast<O>(this Variable<float> self) {
            return new SwizzleNode<float, O> { a = self, swizzle = new string('x', VariableType.Dimensionality<O>()) };
        }

        public static Variable<O> Broadcast<O>(this Variable<int> self) {
            return new SwizzleNode<int, O> { a = self, swizzle = new string('x', VariableType.Dimensionality<O>()) };
        }

        public static Variable<float2> Normalize(this Variable<float2> self) {
            return new SimpleUnaryFunctionNode<float2, float2>() { a = self, func = "normalize" };
        }

        public static Variable<float3> Normalize(this Variable<float3> self) {
            return new SimpleUnaryFunctionNode<float3, float3>() { a = self, func = "normalize" };
        }

        public static Variable<float4> Normalize(this Variable<float4> self) {
            return new SimpleUnaryFunctionNode<float4, float4>() { a = self, func = "normalize" };
        }

        public static Variable<quaternion> Normalize(this Variable<quaternion> self) {
            return new SimpleUnaryFunctionNode<quaternion, quaternion>() { a = self, func = "normalize" };
        }

        public static Variable<float> Magnitude(this Variable<float2> self) {
            return new SimpleUnaryFunctionNode<float2, float>() { a = self, func = "length" };
        }

        public static Variable<float> Magnitude(this Variable<float3> self) {
            return new SimpleUnaryFunctionNode<float3, float>() { a = self, func = "length" };
        }

        public static Variable<float> Magnitude(this Variable<float4> self) {
            return new SimpleUnaryFunctionNode<float4, float>() { a = self, func = "length" };
        }

        public static Variable<float2> Scaled(this Variable<float2> self, Variable<float> other) {
            return new SimpleBinaryOperatorNode<float2, float, float2>() { a = self, b = other, op = "*" };
        }

        public static Variable<float3> Scaled(this Variable<float3> self, Variable<float> other) {
            return new SimpleBinaryOperatorNode<float3, float, float3>() { a = self, b = other, op = "*" };
        }

        public static Variable<float4> Scaled(this Variable<float4> self, Variable<float> other) {
            return new SimpleBinaryOperatorNode<float4, float, float4>() { a = self, b = other, op = "*" };
        }

        public static Variable<float> Dot(this Variable<float2> self, Variable<float2> other) {
            return new SimpleBinaryFunctionNode<float2, float2, float>() { a = self, b = other, func = "dot" };
        }

        public static Variable<float> Dot(this Variable<float3> self, Variable<float3> other) {
            return new SimpleBinaryFunctionNode<float3, float3, float>() { a = self, b = other, func = "dot" };
        }

        public static Variable<float> Dot(this Variable<float4> self, Variable<float4> other) {
            return new SimpleBinaryFunctionNode<float4, float4, float>() { a = self, b = other, func = "dot" };
        }

        public static Variable<quaternion> LookAt(this Variable<float3> self, Variable<float3> up = null, Variable<float> roll = null) {
            return new SimpleTertiaryFunctionNode<float3, float3, float, quaternion>() { a = self, b = up ?? math.up(), c = roll ?? 0f, func = "LookAt" };
        }

        public static Variable<T> Clamp<T>(Variable<T> t, Variable<T> min, Variable<T> max) {
            return new SimpleTertiaryFunctionNode<T, T, T, T>() { a = t, b = min, c = max, func = "clamp" };
        }

        public static Variable<T> Remap<T>(Variable<T> t, Variable<T> inputMin, Variable<T> inputMax, Variable<T> outputMin, Variable<T> outputMax) {
            return new RemapNode<T>() {
                inputMin = inputMin,
                inputMax = inputMax,
                outputMin = outputMin,
                outputMax = outputMax,
                mixer = t,
            };
        }

        public static Variable<T> Lerp<T>(Variable<T> a, Variable<T> b, Variable<T> t, bool saturate = false) {
            if (saturate) {
                t = t.Saturate();
            }

            return new SimpleTertiaryFunctionNode<T, T, T, T>() { a = a, b = b, c = t, func = "lerp" };
        }

        public static Variable<quaternion> Slerp(Variable<quaternion> a, Variable<quaternion> b, Variable<float> t, bool saturate = false) {
            if (saturate) {
                t = Clamp(t, 0f, 1f);
            }

            return new SimpleTertiaryFunctionNode<quaternion, quaternion, float, quaternion>() { a = a, b = b, c = t, func = "Slerp" };
        }

        public static Variable<float> Ease(this Variable<float> self, Easing mode, bool remapNormalize = false) {
            var tmp = self.Saturate();

            string func = Enum.GetName(typeof(Easing), mode);
            var variable = new SimpleUnaryFunctionNode<float, float>() { a = tmp, func = func };

            return remapNormalize ? variable * 2f - 1f : variable;
        }
    }
}
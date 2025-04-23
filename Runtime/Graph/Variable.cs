using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public abstract class Variable<T> : UntypedVariable {
        public static implicit operator Variable<T>(T value) {
            return new DefineNode<T> { value = VariableType.ToDefinableString(value), constant = true };
        }

        public static Variable<T> New(T value) {
            return (Variable<T>)(value);
        } 

        public static Variable<T> Default() {
            return (Variable<T>)default(T);
        }

        public static Variable<T> operator +(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T, T, T> { a = a, b = b, op = "+" };
        }

        public static Variable<T> operator -(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T, T, T> { a = a, b = b, op = "-" };
        }

        public static Variable<bool> operator >(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinOpNode<T, T, bool> { a = a, b = b, op = ">" };
        }

        public static Variable<bool> operator <(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinOpNode<T, T, bool> { a = a, b = b, op = "<" };
        }

        public static Variable<bool> operator <=(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinOpNode<T, T, bool> { a = a, b = b, op = "<=" };
        }

        public static Variable<bool> operator >=(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinOpNode<T, T, bool> { a = a, b = b, op = ">=" };
        }

        public static Variable<bool> operator &(Variable<T> a, Variable<T> b) {
            VerifyBoolBitwiseCheck();
            return new SimpleBinOpNode<T, T, bool> { a = a, b = b, op = "&&" };
        }

        public static Variable<bool> operator |(Variable<T> a, Variable<T> b) {
            VerifyBoolBitwiseCheck();
            return new SimpleBinOpNode<T, T, bool> { a = a, b = b, op = "||" };
        }

        public static Variable<T> operator -(Variable<T> a) {
            return new SimpleUnaFuncNode<T> { a = a, func = "-" };
        }

        public static Variable<T> operator *(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T, T, T> { a = a, b = b, op = "*" };
        }
        public static Variable<T> operator /(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T, T, T> { a = a, b = b, op = "/" };
        }

        public static implicit operator Variable<T>(Inject<T> value) {
            return new InjectedNode<T> { a = value };
        }

        public static implicit operator Variable<T>(CustomCode<T> value) {
            return value.Execute();
        }

        private static void VerifyEqCheck() {
            if (VariableType.Dimensionality<T>() != 1) {
                // TODO: Actually implement VectorExt
                throw new Exception("Equality checks can only be used on scalar types. Check VectorExt for more");
            }
        }

        private static void VerifyBoolBitwiseCheck() {
            if (VariableType.TypeOf<T>().strict != VariableType.StrictType.Bool) {
                // TODO: Actually implement VectorExt
                throw new Exception("Bitwise operations can only work on booleans!!");
            }
        }


        public Variable<U> Swizzle<U>(string swizzle, params Variable<float>[] others) {
            return new SwizzleNode<T, U> { a = this, swizzle = swizzle, others = others };
        }

        public Variable<U> Cast<U>() {
            return new CastNode<T, U> { a = this };
        }

        public Variable<T> Cached(string swizzle = "xyz") {
            throw new NotImplementedException();
            //return new CachedNode<T> { inner = this, swizzle = swizzle };
        }
        public Variable<T> Min(Variable<T> other) {
            return new SimpleBinFuncNode<T> { a = this, b = other, func = "min" };
        }

        public Variable<T> Max(Variable<T> other) {
            return new SimpleBinFuncNode<T> { a = this, b = other, func = "max" };
        }

        public Variable<T> Abs() {
            return new SimpleUnaFuncNode<T> { a = this, func = "abs" };
        }

        public Variable<T> SmoothAbs(Variable<T> smoothing) {
            return new SmoothAbs<T> { a = this, smoothing = smoothing };
        }

        public Variable<T> Lerp(Variable<T> a, Variable<T> b, bool clamp = false) {
            return new LerpNode<T> { a = a, b = b, t = this, clamp = clamp };
        }

        public Variable<T> Clamp(Variable<T> a, Variable<T> b) {
            return new ClampNode<T> { a = a, b = b, t = this };
        }

        public Variable<T> ClampZeroOne() {
            return new ClampNode<T> { a = GraphUtils.Zero<T>(), b = GraphUtils.One<T>(), t = this };
        }


        public Variable<T> With(params (string, UntypedVariable)[] properties) {
            return new SetPropertiesNode<T>() { owner = this, properties = properties };
        }

        internal Variable<O> Broadcast<O>() {
            if (VariableType.Dimensionality<T>() != 1) {
                throw new Exception("Cannot broadcast value from a vector; must be a scalar");
            }

            return new SwizzleNode<T, O> { a = this, swizzle = new string('x', VariableType.Dimensionality<O>()) };
        }
    }

    public static class VariableExtensions {
        public static Variable<float4> Gradient(this Variable<float> mixer, UnityEngine.Gradient gradient, int size = 128) {
            if (gradient == null) {
                throw new NullReferenceException("Unity Gradient is not set");
            }

            return new ColorGradientNode {
                gradient = gradient,
                mixer = mixer,
                size = size,
            };
        }

        public static Variable<float> Curve(this Variable<float> mixer, UnityEngine.AnimationCurve curve, Variable<float> inputMin, Variable<float> inputMax, int size = 128) {
            if (curve == null) {
                throw new NullReferenceException("Unity AnimationCurve is not set");
            }

            if (inputMin == null || inputMax == null) {
                throw new NullReferenceException("inputMin and inputMax need to be set");
            }

            return new CurveNode {
                curve = curve,
                mixer = mixer,
                size = size,
                inputMin = inputMin,
                inputMax = inputMax,
            };
        }

        public static Variable<T> Select<T>(this Variable<bool> self, Variable<T> falseVal, Variable<T> trueVal) {
            return new SelectorOpNode<T>() { falseVal = falseVal, trueVal = trueVal, selector = self };
        }

        public static Variable<O> Broadcast<O>(this Variable<float> self) {
            return new SwizzleNode<float, O> { a = self, swizzle = new string('x', VariableType.Dimensionality<O>()) };
        }

        public static Variable<O> Broadcast<O>(this Variable<int> self) {
            return new SwizzleNode<int, O> { a = self, swizzle = new string('x', VariableType.Dimensionality<O>()) };
        }

        public static Variable<float2> Normalize(this Variable<float2> self) {
            return new NormalizeNode<float2>() { a = self };
        }

        public static Variable<float3> Normalize(this Variable<float3> self) {
            return new NormalizeNode<float3>() { a = self };
        }

        public static Variable<float4> Normalize(this Variable<float4> self) {
            return new NormalizeNode<float4>() { a = self };
        }

        public static Variable<float> Magnitude(this Variable<float2> self) {
            return new LengthNode<float2>() { a = self };
        }

        public static Variable<float> Magnitude(this Variable<float3> self) {
            return new LengthNode<float3>() { a = self };
        }

        public static Variable<float> Magnitude(this Variable<float4> self) {
            return new LengthNode<float4>() { a = self };
        }

        public static Variable<float2> Scaled(this Variable<float2> self, Variable<float> other) {
            return new SimpleBinOpNode<float2, float, float2>() { a = self, b = other, op = "*" };
        }

        public static Variable<float3> Scaled(this Variable<float3> self, Variable<float> other) {
            return new SimpleBinOpNode<float3, float, float3>() { a = self, b = other, op = "*" };
        }

        public static Variable<float4> Scaled(this Variable<float4> self, Variable<float> other) {
            return new SimpleBinOpNode<float4, float, float4>() { a = self, b = other, op = "*" };
        }
    }
}
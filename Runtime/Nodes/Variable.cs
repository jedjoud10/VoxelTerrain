using System;

namespace jedjoud.VoxelTerrain.Generation {
    [Serializable]
    public abstract class Variable<T> : TreeNode {

        public static implicit operator Variable<T>(T value) {
            return new DefineNode<T> { value = GraphUtils.ToDefinableString(value), constant = true };
        }

        public static Variable<T> operator +(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T> { a = a, b = b, op = "+" };
        }

        public static Variable<T> operator -(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T> { a = a, b = b, op = "-" };
        }

        public static Variable<T> operator -(Variable<T> a) {
            return new SimpleUnaFuncNode<T> { a = a, func = "-" };
        }

        public static Variable<T> operator *(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T> { a = a, b = b, op = "*" };
        }
        public static Variable<T> operator /(Variable<T> a, Variable<T> b) {
            return new SimpleBinOpNode<T> { a = a, b = b, op = "/" };
        }

        public static implicit operator Variable<T>(Inject<T> value) {
            return new InjectedNode<T> { a = value };
        }

        public static implicit operator Variable<T>(CustomCode<T> value) {
            return value.DoStuff();
        }

        public Variable<U> Swizzle<U>(string swizzle) {
            return new SwizzleNode<T, U> { a = this, swizzle = swizzle };
        }

        public Variable<U> Cast<U>() {
            return new CastNode<T, U> { a = this };
        }

        /*
        public Variable<T> Cached(int sizeReductionPower, string swizzle = "xyz") {
            return new CachedNode<T> { inner = this, sizeReductionPower = sizeReductionPower, sampler = new CachedSampler(), swizzle = swizzle };
        }
        */
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

        public static Variable<T> Lerp(Variable<T> a, Variable<T> b, Variable<T> t, bool clamp = false) {
            return new LerpNode<T> { a = a, b = b, t = t, clamp = clamp };
        }

        public static Variable<T> Clamp(Variable<T> t, Variable<T> a, Variable<T> b) {
            return new ClampNode<T> { a = a, b = b, t = t };
        }

        public static Variable<T> ClampZeroOne(Variable<T> t) {
            return new ClampNode<T> { a = GraphUtils.Zero<T>(), b = GraphUtils.One<T>(), t = t };
        }

        internal Variable<O> Broadcast<O>() {
            if (GraphUtils.Dimensionality<T>() != 1) {
                throw new Exception("breh");
            }

            return new SwizzleNode<T, O> { a = this, swizzle = new string('x', GraphUtils.Dimensionality<O>()) };
        }
    }
}
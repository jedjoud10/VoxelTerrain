using System;

namespace jedjoud.VoxelTerrain.Generation {
    public abstract partial class Variable<T> : UntypedVariable {
        public static implicit operator Variable<T>(T value) {
            return new DefineNode<T> { value = VariableType.ToDefinableString(value), constant = true };
        }

        public static Variable<T> Const(T value) {
            return new DefineNode<T> { value = VariableType.ToDefinableString(value), constant = true };
        }

        public static Variable<T> NonConst(T value) {
            return new DefineNode<T> { value = VariableType.ToDefinableString(value), constant = false };
        }

        public static Variable<T> Default() {
            return (Variable<T>)default(T);
        }

        public static Variable<T> operator +(Variable<T> a, Variable<T> b) {
            return new SimpleBinaryOperatorNode<T, T, T> { a = a, b = b, op = "+" };
        }

        public static Variable<T> operator -(Variable<T> a, Variable<T> b) {
            return new SimpleBinaryOperatorNode<T, T, T> { a = a, b = b, op = "-" };
        }

        public static Variable<bool> operator >(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinaryOperatorNode<T, T, bool> { a = a, b = b, op = ">" };
        }

        public static Variable<bool> operator <(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinaryOperatorNode<T, T, bool> { a = a, b = b, op = "<" };
        }

        public static Variable<bool> operator <=(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinaryOperatorNode<T, T, bool> { a = a, b = b, op = "<=" };
        }

        public static Variable<bool> operator >=(Variable<T> a, Variable<T> b) {
            VerifyEqCheck();
            return new SimpleBinaryOperatorNode<T, T, bool> { a = a, b = b, op = ">=" };
        }

        public static Variable<bool> operator &(Variable<T> a, Variable<T> b) {
            VerifyBoolBitwiseCheck();
            return new SimpleBinaryOperatorNode<T, T, bool> { a = a, b = b, op = "&&" };
        }

        public static Variable<bool> operator |(Variable<T> a, Variable<T> b) {
            VerifyBoolBitwiseCheck();
            return new SimpleBinaryOperatorNode<T, T, bool> { a = a, b = b, op = "||" };
        }

        public static Variable<T> operator -(Variable<T> a) {
            return new SimpleUnaryFunctionNode<T, T> { a = a, func = "-" };
        }

        public static Variable<T> operator !(Variable<T> a) {
            return new SimpleUnaryFunctionNode<T, T> { a = a, func = "!" };
        }

        public static Variable<T> operator *(Variable<T> a, Variable<T> b) {
            return new SimpleBinaryOperatorNode<T, T, T> { a = a, b = b, op = "*" };
        }
        public static Variable<T> operator /(Variable<T> a, Variable<T> b) {
            return new SimpleBinaryOperatorNode<T, T, T> { a = a, b = b, op = "/" };
        }

        public static implicit operator Variable<T>(Inject<T> value) {
            return new InjectedNode<T> { a = value };
        }

        /*
        public static implicit operator Variable<T>(CustomCode<T> value) {
            return value.Execute();
        }
        */

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

        public Variable<U> Cast<U>() {
            return new CastNode<T, U> { a = this };
        }

        public Variable<T> Min(Variable<T> other) {
            return new SimpleBinaryFunctionNode<T, T, T> { a = this, b = other, func = "min" };
        }

        public Variable<T> Max(Variable<T> other) {
            return new SimpleBinaryFunctionNode<T, T, T> { a = this, b = other, func = "max" };
        }

        public Variable<T> Abs() {
            return new SimpleUnaryFunctionNode<T, T> { a = this, func = "abs" };
        }

        public Variable<T> SmoothAbs(Variable<T> smoothing) {
            return new SmoothAbs<T> { a = this, smoothing = smoothing };
        }

        public Variable<T> Saturate() {
            return VariableExtensions.Clamp(this, GraphUtils.Zero<T>(), GraphUtils.One<T>());
        }

        public Variable<T> OneMinus() {
            return GraphUtils.One<T>() - this;
        }

        public Variable<T> With(params (string, UntypedVariable)[] properties) {
            return new SetPropertiesNode<T>() { owner = this, properties = properties };
        }

        public Variable<O> Broadcast<O>() {
            if (VariableType.Dimensionality<T>() != 1) {
                throw new Exception("Cannot broadcast value from a vector; must be a scalar");
            }

            return new SwizzleNode<T, O> { a = this, swizzle = new string('x', VariableType.Dimensionality<O>()) };
        }
    }
}
using System;

namespace jedjoud.VoxelTerrain.Generation {
    public class DefineNode<T> : Variable<T> {
        public string value;
        public bool constant;

        public override void HandleInternal(TreeContext ctx) {
            ctx.DefineAndBindNode(this, VariableType.TypeOf<T>(), "c", value, constant);
        }
    }

    public class NoOp<T> : Variable<T> {
        public override void HandleInternal(TreeContext ctx) {
        }
    }

    public class Passthrough<T> : Variable<T> {
        public Variable<T> past;
        
        public override void HandleInternal(TreeContext ctx) {
            past.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, "passthrough", ctx[past]);
        }
    }

    public class SimpleBinaryOperatorNode<A, B, T> : Variable<T> {
        public Variable<A> a;
        public Variable<B> b;
        public string op;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(op);
            a.Handle(ctx);
            b.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_op_{ctx[b]}", $"{ctx[a]} {op} {ctx[b]}");
        }
    }

    public class SimpleBinaryFunctionNode<I1, I2, O> : Variable<O> {
        public Variable<I1> a;
        public Variable<I2> b;
        public string func;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(func);
            a.Handle(ctx);
            b.Handle(ctx);
            ctx.DefineAndBindNode<O>(this, $"{ctx[a]}_func_{ctx[b]}", $"{func}({ctx[a]},{ctx[b]})");
        }
    }

    public class SimpleTertiaryFunctionNode<I1, I2, I3, O> : Variable<O> {
        public Variable<I1> a;
        public Variable<I2> b;
        public Variable<I3> c;
        public string func;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(func);
            a.Handle(ctx);
            b.Handle(ctx);
            c.Handle(ctx);
            ctx.DefineAndBindNode<O>(this, $"{ctx[a]}_func_{ctx[b]}", $"{func}({ctx[a]},{ctx[b]},{ctx[c]})");
        }
    }

    public class SimpleUnaryFunctionNode<I, O> : Variable<O> {
        public Variable<I> a;
        public string func;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(func);
            a.Handle(ctx);
            ctx.DefineAndBindNode<O>(this, $"{ctx[a]}_func", $"({func}({ctx[a]}))");
        }
    }

    public class SmoothAbs<T> : Variable<T> {
        public Variable<T> a;
        public Variable<float> smoothing;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            smoothing.Handle(ctx);

            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_smooth_abs", $"sqrt(pow({ctx[a]},2.0) + max({ctx[smoothing]},0))");
        }
    }

    public class CastNode<I, O> : Variable<O> {
        public Variable<I> a;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            ctx.DefineAndBindNode<O>(this, $"{ctx[a]}_casted", $"{ctx[a]}");
        }
    }

    public class SetPropertiesNode<T> : Variable<T> {
        public Variable<T> owner;
        public (string, UntypedVariable)[] properties;

        public override void HandleInternal(TreeContext ctx) {
            owner.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, $"{ctx[owner]}_with_properties", ctx[owner]);

            foreach (var property in properties) {
                property.Item2.Handle(ctx);
                ctx.AssignRaw($"{ctx[this]}.{property.Item1}", ctx[property.Item2]);
            }
        }
    }

    public class SwizzleNode<I, O> : Variable<O> {
        public Variable<I> a;
        public string swizzle;
        public Variable<float>[] others;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(swizzle);

            int input = VariableType.Dimensionality<O>();
            int output = swizzle.Length;
            a.Handle(ctx);

            if (others != null) {
                foreach (var item in others) {
                    item.Handle(ctx);
                }
            }

            if (input <= output) {
                ctx.DefineAndBindNode<O>(this, $"{ctx[a]}_swizzled", $"{ctx[a]}.{swizzle}");
            } else {
                string swizzled = $"{ctx[a]}.{swizzle}";
                string other = "";
                for (int i = 0; i < input - output; i++) {
                    if (others != null) {
                        other += $", {ctx[others[i]]}";
                    } else {
                        other += ", 0.0";
                    }
                }

                switch (VariableType.TypeOf<O>().strict) {
                    case VariableType.StrictType.Float2:
                        ctx.DefineAndBindNode<O>(this, $"f2_ctor_swizzle", $"float2({swizzled}{other})");
                        break;
                    case VariableType.StrictType.Float3:
                        ctx.DefineAndBindNode<O>(this, $"f3_ctor_swizzle", $"float3({swizzled}{other})");
                        break;
                    case VariableType.StrictType.Float4:
                        ctx.DefineAndBindNode<O>(this, $"f4_ctor_swizzle", $"float4({swizzled}{other})");
                        break;
                    default:
                        throw new Exception();
                }
            }
        }
    }

    public class ConstructNode<O> : Variable<O> {
        public Variable<float>[] inputs;

        public override void HandleInternal(TreeContext ctx) {
            string C(int index) {
                if (index < inputs.Length) {
                    return ctx[inputs[index]];
                } else {
                    return "0.0";
                }
            }

            foreach (var input in inputs) {
                input.Handle(ctx);
            }

            switch (VariableType.TypeOf<O>().strict) {
                case VariableType.StrictType.Float2:
                    ctx.DefineAndBindNode<O>(this, $"f2_ctor", $"float2({C(0)},{C(1)})");
                    break;
                case VariableType.StrictType.Float3:
                    ctx.DefineAndBindNode<O>(this, $"f3_ctor", $"float3({C(0)},{C(1)},{C(2)})");
                    break;
                case VariableType.StrictType.Float4:
                    ctx.DefineAndBindNode<O>(this, $"f4_ctor", $"float4({C(0)},{C(1)},{C(2)},{C(3)})");
                    break;
                default:
                    throw new Exception();
            }
        }
    }

    public class SelectorNode<T> : Variable<T> {
        public Variable<T> falseVal;
        public Variable<T> trueVal;
        public Variable<bool> selector;

        public override void HandleInternal(TreeContext ctx) {
            falseVal.Handle(ctx);
            trueVal.Handle(ctx);
            selector.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, $"{ctx[trueVal]}_select_{ctx[falseVal]}", $"({ctx[selector]} ? {ctx[trueVal]} : {ctx[falseVal]})");
        }
    }
}
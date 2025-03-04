using NUnit.Framework;
using System;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class DefineNode<T> : Variable<T> {
        public string value;
        public bool constant;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(value);
            ctx.DefineAndBindNode(this, VariableType.TypeOf<T>(), "c", value, constant);
        }
    }

    public class NoOp<T> : Variable<T> {
        public override void HandleInternal(TreeContext context) {
        }
    }

    public class SimpleBinOpNode<A, B, T> : Variable<T> {
        public Variable<A> a;
        public Variable<B> b;
        public string op;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            b.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_op_{ctx[b]}", $"{ctx[a]} {op} {ctx[b]}");
        }
    }

    public class SimpleBinFuncNode<T> : Variable<T> {
        public Variable<T> a;
        public Variable<T> b;
        public string func;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            b.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_func_{ctx[b]}", $"{func}({ctx[a]},{ctx[b]})");
        }
    }

    public class SimpleUnaFuncNode<T> : Variable<T> {
        public Variable<T> a;
        public string func;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_func", $"({func}({ctx[a]}))");
        }
    }

    public class SmoothAbs<T> : Variable<T> {
        public Variable<T> a;
        public Variable<T> smoothing;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            smoothing.Handle(ctx);

            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_smooth_abs", $"sqrt(pow({ctx[a]},2.0) + {ctx[smoothing]})");
        }
    }

    public class LerpNode<T> : Variable<T> {
        public Variable<T> a;
        public Variable<T> b;
        public Variable<T> t;
        public bool clamp;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            b.Handle(ctx);
            t.Handle(ctx);
            ctx.Hash(clamp);

            string mixer = clamp ? $"clamp({ctx[t]}, 0.0, 1.0)" : ctx[t];

            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_lerp_{ctx[b]}", $"lerp({ctx[a]},{ctx[b]},{mixer})");
        }
    }

    public class ClampNode<T> : Variable<T> {
        public Variable<T> a;
        public Variable<T> b;
        public Variable<T> t;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            b.Handle(ctx);
            t.Handle(ctx);

            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_clamp_{ctx[b]}", $"clamp({ctx[t]},{ctx[a]},{ctx[b]})");
        }
    }

    public class CastNode<I, O> : Variable<O> {
        public Variable<I> a;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            ctx.DefineAndBindNode<O>(this, $"{ctx[a]}_casted", $"{ctx[a]}");
        }
    }

    public class NormalizeNode<T> : Variable<T> {
        public Variable<T> a;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            ctx.DefineAndBindNode<T>(this, $"{ctx[a]}_normalized", $"normalize({ctx[a]})");
        }
    }

    public class LengthNode<T> : Variable<float> {
        public Variable<T> a;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            ctx.DefineAndBindNode<float>(this, $"{ctx[a]}_length", $"length({ctx[a]})");
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

    public class SelectorOpNode<T> : Variable<T> {
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
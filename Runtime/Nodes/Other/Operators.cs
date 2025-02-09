using System;

namespace jedjoud.VoxelTerrain.Generation {
    public static class MathOps {
        public static Variable<float> Length<T>(Variable<T> position) {
            return new LengthNode<T>() { a = position };
        }

        public static Variable<T> Normalize<T>(Variable<T> position) {
            return new NormalizeNode<T>() { a = position };
        }
    }

    public class DefineNode<T> : Variable<T> {
        public string value;
        public bool constant;

        public override void HandleInternal(TreeContext ctx) {
            ctx.Hash(value);
            ctx.DefineAndBindNode(this, GraphUtils.TypeOf<T>(), "c", value, constant);
        }
    }

    public class NoOp<T> : Variable<T> {
        public override void HandleInternal(TreeContext context) {
        }
    }

    public class SimpleBinOpNode<T> : Variable<T> {
        public Variable<T> a;
        public Variable<T> b;
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

    public class SwizzleNode<I, O> : Variable<O> {
        public Variable<I> a;
        public string swizzle;

        public override void HandleInternal(TreeContext ctx) {
            int input = GraphUtils.Dimensionality<O>();
            int output = swizzle.Length;
            a.Handle(ctx);

            if (input <= output) {
                ctx.DefineAndBindNode<O>(this, $"{ctx[a]}_swizzled", $"{ctx[a]}.{swizzle}");
            } else {
                string swizzled = $"{ctx[a]}.{swizzle}";
                string other = "";
                for (int i = 0; i < input - output; i++) {
                    other += ", 0.0";
                }

                switch (GraphUtils.TypeOf<O>()) {
                    case GraphUtils.StrictType.Float2:
                        ctx.DefineAndBindNode<O>(this, $"f2_ctor_swizzle", $"float2({swizzled}{other})");
                        break;
                    case GraphUtils.StrictType.Float3:
                        ctx.DefineAndBindNode<O>(this, $"f3_ctor_swizzle", $"float3({swizzled}{other})");
                        break;
                    case GraphUtils.StrictType.Float4:
                        ctx.DefineAndBindNode<O>(this, $"f4_ctor_swizzle", $"float4({swizzled}{other})");
                        break;
                    default:
                        throw new Exception();
                        break;
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

            switch (GraphUtils.TypeOf<O>()) {
                case GraphUtils.StrictType.Float2:
                    ctx.DefineAndBindNode<O>(this, $"f2_ctor", $"float2({C(0)},{C(1)})");
                    break;
                case GraphUtils.StrictType.Float3:
                    ctx.DefineAndBindNode<O>(this, $"f3_ctor", $"float3({C(0)},{C(1)},{C(2)})");
                    break;
                case GraphUtils.StrictType.Float4:
                    ctx.DefineAndBindNode<O>(this, $"f4_ctor", $"float4({C(0)},{C(1)},{C(2)},{C(3)})");
                    break;
                default:
                    throw new Exception();
                    break;
            }
        }
    }
}
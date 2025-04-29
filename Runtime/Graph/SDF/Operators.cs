
namespace jedjoud.VoxelTerrain.Generation {
    using static Sdf;
    public static class Sdf {
        public static Variable<float> Union(Variable<float> first, Variable<float> second, Variable<float> smooth = null) {
            return new OpSdfOp { first = first, second = second, smooth = smooth, op = "Union", negate = false };
        }

        public static Variable<float> Intersection(Variable<float> first, Variable<float> second, Variable<float> smooth = null) {
            return new OpSdfOp { first = first, second = second, smooth = smooth, op = "Intersection", negate = false };
        }

        public static Variable<float> Subtraction(Variable<float> first, Variable<float> second, Variable<float> smooth = null) {
            return new OpSdfOp { first = first, second = second, smooth = smooth, op = "Subtraction", negate = true};
        }

        public static Variable<float> Distance<T>(Variable<T> a, Variable<T> b, DistanceMetric mode = DistanceMetric.Euclidean) {
            return new DistanceOp<T>() { a = a, b = b, mode = mode };
        }

        public enum DistanceMetric {
            Euclidean,
            Manhattan,
            Chebyshev,
        }
    }

    public class DistanceOp<T> : Variable<float> {
        public Variable<T> a;
        public Variable<T> b;
        public DistanceMetric mode;

        public override void HandleInternal(TreeContext ctx) {
            a.Handle(ctx);
            b.Handle(ctx);
            ctx.Hash(mode);

            string func = "";

            switch (mode) {
                case DistanceMetric.Euclidean:
                    func = $"distance({ctx[a]}, {ctx[b]})";
                    break;
                case DistanceMetric.Manhattan:
                    func = $"dot(abs({ctx[a]} - {ctx[b]}), 1.0) / 1.414";
                    break;
                case DistanceMetric.Chebyshev:
                    Variable<T> temp = ctx.AssignTempVariable<T>("distance_maxx_bruh", $"abs({ctx[a]} - {ctx[b]})");

                    string[] swizzler = new string[] { "x", "y", "z" };

                    Variable<float> temp2 = temp.Swizzle<float>("x");
                    for (var i = 1; i < VariableType.Dimensionality<T>(); i++) {
                        temp2.Handle(ctx);
                        temp2 = ctx.AssignTempVariable<float>("folded_vec", $"max({ctx[temp2]}, {ctx[temp]}.{swizzler[i]})");
                    }


                    func = ctx[temp2];
                    break;
                default:
                    throw new System.Exception();
            }

            ctx.DefineAndBindNode<float>(this, $"distance_maxx", func);
        }
    }

    public class OpSdfOp : Variable<float> {
        public Variable<float> first;
        public Variable<float> second;
        public Variable<float> smooth = null;
        public string op;
        public bool negate;

        public override void HandleInternal(TreeContext ctx) {
            first.Handle(ctx);
            second.Handle(ctx);
            smooth?.Handle(ctx);

            string negatePrefix = negate ? "-" : "";
            if (smooth != null) {
                smooth.Handle(ctx);
                ctx.DefineAndBindNode<float>(this, $"sdf_smooth_{op.ToLower()}", $"opSmooth{op}({negatePrefix}{ctx[first]}, {ctx[second]}, {ctx[smooth]})");
            } else {
                ctx.DefineAndBindNode<float>(this, $"sdf_{op.ToLower()}", $"op{op}({negatePrefix}{ctx[first]}, {ctx[second]})");
            }
        }
    }
}
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public class CellularNode<T> : Variable<float> {
        public Variable<T> inner;
        public float tilingModSize;
        public Cellular<T>.Distance distance;
        public Cellular<T>.ShouldSpawn shouldSpawn;

        public Variable<float> offset;
        public Variable<float> factor;

        public override void HandleInternal(TreeContext context) {
            inner.Handle(context);
            bool tiling = tilingModSize > 0;
            context.Hash(tilingModSize);

            string scopeName = context.GenId($"CellularScope");
            string outputName = $"{scopeName}_sdf_output";
            
            int dimensions = VariableType.Dimensionality<T>();

            if (dimensions != 2 && dimensions != 3) {
                throw new Exception("Cellular Node input position must be either 2D or 3D");
            }

            Variable<float> custom = CustomCode.WithCode<float>((TreeContext ctx) => {
                offset.Handle(ctx);
                factor.Handle(ctx);
                string typeString = VariableType.TypeOf<T>().ToStringType();

                int maxLoopSize = 1;
                string loopInit = dimensions == 2 ? $@"
for (int y = -{maxLoopSize}; y <= {maxLoopSize}; y++)
for (int x = -{maxLoopSize}; x <= {maxLoopSize}; x++) {{
" : $@"
for(int z = -{maxLoopSize}; z <= {maxLoopSize}; z++)
for(int y = -{maxLoopSize}; y <= {maxLoopSize}; y++)
for(int x = -{maxLoopSize}; x <= {maxLoopSize}; x++) {{
";

                string tiler = tiling ? $"{typeString} tiled = fmod(cell, {tilingModSize});" : $"{typeString} tiled = cell;";

                string outputFirst = $@"
{ctx[inner]} += modulo_seed;
{typeString} posCell = floor({ctx[inner]});
{typeString} posFrac = frac({ctx[inner]});

float output = 100.0;

{loopInit}
    {typeString} cell = {typeString}({GraphUtils.VectorConstructor<T>()}) + posCell;
    {tiler}
    {typeString} randomOffset = hash{dimensions}{dimensions}(tiled);
";
                ctx.AddLine(outputFirst);

                ctx.Indent++;
                Variable<T> tiled = ctx.AssignTempVariable<T>("test__", "tiled");
                Variable<bool> shouldSpawnVar = shouldSpawn(tiled);
                shouldSpawnVar.Handle(ctx);
                ctx.Indent--;

                string outputSecond = $@"
    if ({ctx[shouldSpawnVar]}) {{
        {typeString} checkingPos = cell + randomOffset;
";
                ctx.AddLine(outputSecond);

                ctx.Indent += 2;
                Variable<T> checkingPos = ctx.AssignTempVariable<T>("test__2", "checkingPos");
                Variable<float> distanceVar = distance(checkingPos, inner);
                distanceVar.Handle(ctx);
                ctx.Indent -= 2;

                string outputThird = $@"
        output = min(output, {ctx[distanceVar]});
    }}
}}
";
                ctx.AddLine(outputThird);
                return $"min(output, 1.0) * {ctx[factor]} + {ctx[offset]}";
            });

            ScopeArgument input = ScopeArgument.AsInput<T>(context[inner], inner);
            ScopeArgument output = ScopeArgument.AsOutput<float>(outputName, custom);

            
            TreeScope scope = new TreeScope() {
                name = scopeName,
                arguments = new ScopeArgument[] { input, output },
            };
            TreeScope oldScope = context.currentScope;

            // ENTER NEW SCOPE!!!
            context.scopes.Insert(0, scope);
            context.currentScope = scope;
            context.Add(input.node, input.name);
            custom.Handle(context);

            // EXIT SCOPE!!!
            context.currentScope = oldScope;

            /*
            context.currentScope = context.scopes.Count - 1;
            context.scopeDepth++;
            UnityEngine.Debug.LogWarning($"S{context.currentScope}, D{context.scopeDepth}");




            
            // EXIT SCOPE!!!
            context.scopeDepth--;
            context.currentScope = oldScopeIndex;
            UnityEngine.Debug.LogWarning($"S{context.currentScope}, D{context.scopeDepth}");
            */



            context.currentScope.lines.Add(scope.InitArgVars());
            context.currentScope.lines.Add(scope.CallWithArgs());
            context.DefineAndBindNode<float>(this, "cellular_tiler", outputName);
            //context.dedupe.Add(dedupeOutputName);
            //context.DefineAndBindNode<float>(this, "cellular_tiler", "0");
        }
    }

    public class Cellular<T> {
        public float tilingModSize;

        public delegate Variable<float> Distance(Variable<T> a, Variable<T> b);
        public delegate Variable<bool> ShouldSpawn(Variable<T> point);

        public Distance distance;

        // Actually spawns the "entity" if greater than 0
        public ShouldSpawn shouldSpawn;


        public Variable<float> offset;
        public Variable<float> factor;

        public Cellular(Distance distance = null, ShouldSpawn shouldSpawn = null, float tilingModSize = -1, Variable<float> offset = null, Variable<float> factor = null) {
            this.distance = distance;
            this.tilingModSize = tilingModSize;
            this.shouldSpawn = shouldSpawn;
            this.offset = offset;
            this.factor = factor;
        }

        public static Cellular<T> Shape(SdfShape shape, Variable<float> probability, Variable<float> offset = null, Variable<float> factor = null) {
            return new Cellular<T>((Variable<T> a, Variable<T> b) => {
                return shape.Evaluate(a.Cast<float3>() - b.Cast<float3>());
            }, shouldSpawn: (Variable<T> a) => {
                return probability > Random.Evaluate<T, float>(a, false);
            }, offset: offset, factor: factor);
        }

        public static Cellular<T> Simple(Sdf.DistanceMetric metric, Variable<float> probability, Variable<float> offset = null, Variable<float> factor = null) {
            return new Cellular<T>((Variable<T> a, Variable<T> b) => {
                return Sdf.Distance(a, b, metric);
            }, shouldSpawn: (Variable<T> a) => {
                return probability > Random.Evaluate<T, float>(a, false);
            }, offset: offset, factor: factor);
        }

        public Variable<float> Tile(Variable<T> position) {
            Variable<float> cached = new CellularNode<T>() {
                tilingModSize = tilingModSize,
                distance = distance ?? ((a, b) => Sdf.Distance(a, b)),
                shouldSpawn = shouldSpawn ?? ((pos) => true),
                inner = position,
                offset = offset ?? 0f,
                factor = factor ?? 1f,
            };
            return cached;
        }
    }
}
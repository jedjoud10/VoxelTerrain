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

        private int dedupeScopeIndex = -1;
        private string dedupeOutputName = "";

        public override void HandleInternal(TreeContext context) {
            inner.Handle(context);
            bool tiling = tilingModSize > 0;
            context.Hash(tilingModSize);

            ScopeArgument input = ScopeArgument.AsInput<T>(context[inner], inner);
            if (dedupeOutputName != "" && dedupeScopeIndex != -1 && context.dedupe.Contains(dedupeOutputName)) {
                TreeScope alreadyDefinedScope = context.scopes[dedupeScopeIndex];

                // We need to overwrite the input since this is a different scope (probably)
                // Ok fuck it yk what?
                // TODO: Implement a general scope/function system so we don't have to rewrite these shenanigans for every node that uses a custom scope
                //alreadyDefinedScope.namesToNodes.TryAdd(input.node, context[input.node]);

                var overwrite = new (int, ScopeArgument)[] { (0, input) };
                context.scopes[context.currentScope].lines.Add(alreadyDefinedScope.InitArgVars(overwrite));
                //alreadyDefinedScope.arguments[0] = input;
                context.scopes[context.currentScope].lines.Add(alreadyDefinedScope.CallWithArgs(overwrite));
                context.DefineAndBindNode<float>(this, "cellular_tiler", dedupeOutputName);
                return;
            }

            string scopeName = context.GenId($"PeriodicityScope");
            string outputName = $"{scopeName}_sdf_output";
            dedupeOutputName = outputName;

            int dimensions = VariableType.Dimensionality<T>();

            if (dimensions != 2 && dimensions != 3) {
                throw new Exception("uhhhh");
            }

            Variable<float> custom = CustomCode.WithCode<float>((UntypedVariable self, TreeContext ctx) => {
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
{ctx[inner]} += moduloSeed;
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
                Variable<float> shouldSpawnVar = shouldSpawn(tiled);
                shouldSpawnVar.Handle(ctx);
                ctx.Indent--;

                string outputSecond = $@"
    if ({ctx[shouldSpawnVar]} > 0.0) {{
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

            ScopeArgument output = ScopeArgument.AsOutput<float>(outputName, custom);

            int index = context.scopes.Count;
            int oldScopeIndex = context.currentScope;

            TreeScope scopium = new TreeScope(context.scopeDepth + 1) {
                name = scopeName,
                arguments = new ScopeArgument[] { input, output, },
                nodesToNames = new Dictionary<UntypedVariable, string> { { input.node, context[input.node] } },
            };

            context.scopes.Add(scopium);

            // ENTER NEW SCOPE!!!
            context.currentScope = index;
            context.scopeDepth++;

            // Add the start node (position node) to the new scope
            //context.scopes[index].namesToNodes.TryAdd(input.node, "position");

            // Call the recursive handle function within the indented scope
            custom.Handle(context);

            // EXIT SCOPE!!!
            context.scopeDepth--;
            context.currentScope = oldScopeIndex;

            context.scopes[context.currentScope].lines.Add(scopium.InitArgVars());
            context.scopes[context.currentScope].lines.Add(scopium.CallWithArgs());
            
            dedupeScopeIndex = index;
            context.DefineAndBindNode<float>(this, "cellular_tiler", outputName);
            context.dedupe.Add(dedupeOutputName);
        }
    }

    public class Cellular<T> {
        public float tilingModSize;

        public delegate Variable<float> Distance(Variable<T> a, Variable<T> b);
        public delegate Variable<float> ShouldSpawn(Variable<T> point);

        public Distance distance;

        // Actually spawns the "entity" if greater than 0
        public ShouldSpawn shouldSpawn;


        public Variable<float> offset;
        public Variable<float> factor;

        public Cellular(Distance distance = null, ShouldSpawn shouldSpawn = null, float tilingModSize = -1) {
            this.distance = distance;
            this.tilingModSize = tilingModSize;
            this.shouldSpawn = shouldSpawn;
            this.offset = 0.0f;
            this.factor = 1.0f;
        }

        public static Cellular<T> Shape(SdfShape shape, Variable<float> probability) {
            return new Cellular<T>((Variable<T> a, Variable<T> b) => {
                return shape.Evaluate(a.Cast<float3>() - b.Cast<float3>());
            }, shouldSpawn: (Variable<T> a) => {
                return probability * 2.0f - Random.Evaluate<T, float>(a, false);
            });
        }

        public static Cellular<T> Simple(Sdf.DistanceMetric metric, Variable<float> probability) {
            return new Cellular<T>((Variable<T> a, Variable<T> b) => {
                return Sdf.Distance(a, b, metric);
            }, shouldSpawn: (Variable<T> a) => {
                return probability * 2.0f - Random.Evaluate<T, float>(a, false);
            });
        }

        public Variable<float> Tile(Variable<T> position) {
            return new CellularNode<T>() {
                tilingModSize = tilingModSize,
                distance = distance ?? ((a, b) => Sdf.Distance(a, b)),
                shouldSpawn = shouldSpawn ?? ((pos) => 1.0f),
                inner = position,
                offset = offset,
                factor = factor,
            };
        }
    }
}
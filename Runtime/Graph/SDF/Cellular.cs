using System;
using System.Collections.Generic;

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

            string scopeName = context.GenId($"PeriodicityScope");
            string outputName = $"{scopeName}_sdf_output";

            int dimensions = VariableType.Dimensionality<T>();

            if (dimensions != 2 && dimensions != 3) {
                throw new Exception("uhhhh");
            }

            Variable<float> tahini = new CustomCode<float>((UntypedVariable self, TreeContext ctx) => {
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

                ctx.DefineAndBindNode<float>(self, "__", $"min(output, 1.0) * {ctx[factor]} + {ctx[offset]}");
            });

            ScopeArgument input = new ScopeArgument(context[inner], VariableType.TypeOf<T>(), inner, false);
            ScopeArgument output = new ScopeArgument(outputName, VariableType.TypeOf<float>(), tahini, true);

            int index = context.scopes.Count;
            int oldScopeIndex = context.currentScope;

            TreeScope scopium = new TreeScope(context.scopeDepth + 1) {
                name = scopeName,
                arguments = new ScopeArgument[] { input, output, },
                namesToNodes = new Dictionary<UntypedVariable, string> { { input.node, context[input.node] } },
            };

            context.scopes.Add(scopium);

            // ENTER NEW SCOPE!!!
            context.currentScope = index;
            context.scopeDepth++;

            // Add the start node (position node) to the new scope
            //context.scopes[index].namesToNodes.TryAdd(input.node, "position");

            // Call the recursive handle function within the indented scope
            tahini.Handle(context);

            // EXIT SCOPE!!!
            context.scopeDepth--;
            context.currentScope = oldScopeIndex;

            context.scopes[context.currentScope].lines.Add(scopium.InitializeVariables());
            context.scopes[context.currentScope].lines.Add(scopium.GenerateScopedArguments());
            context.DefineAndBindNode<float>(this, "cellular_tiler", outputName);
        }
    }

    public class Cellular<T> {
        public float tilingModSize;

        public delegate Variable<float> Distance(Variable<T> a, Variable<T> b);
        public delegate Variable<float> ShouldSpawn(Variable<T> point);

        public Distance distance;

        // Value should be between -1 and 1
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
                distance = distance != null ? distance : (a, b) => Sdf.Distance(a, b),
                shouldSpawn = shouldSpawn != null ? shouldSpawn : (pos) => 1.0f,
                inner = position,
                offset = offset,
                factor = factor,
            };
        }
    }
}
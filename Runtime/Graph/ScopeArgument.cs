namespace jedjoud.VoxelTerrain.Generation {
    public class ScopeArgument {
        public VariableType type;
        public UntypedVariable node;
        public string name;
        public bool output;

        public static ScopeArgument AsInput<T>(string name, Variable<T> backing = null) {
            ScopeArgument arg = new ScopeArgument();
            arg.type = VariableType.TypeOf<T>();
            arg.name = name;
            arg.node = backing ?? new NoOp<T>();
            arg.output = false;
            return arg;
        }

        public static ScopeArgument AsOutput<T>(string name, Variable<T> backing) {
            ScopeArgument arg = new ScopeArgument();
            arg.type = VariableType.TypeOf<T>();
            arg.name = name;
            arg.node = backing;
            arg.output = true;
            return arg;
        }
    }
}
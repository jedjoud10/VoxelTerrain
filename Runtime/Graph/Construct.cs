namespace jedjoud.VoxelTerrain.Generation {
    public abstract partial class Variable<T> : UntypedVariable {
        public static Variable<T> New(Variable<float> x, Variable<float> y) { return new ConstructNode<T>() { inputs = new Variable<float>[] { x, y } }; }
        public static Variable<T> New(Variable<float> x, Variable<float> y, Variable<float> z) { return new ConstructNode<T>() { inputs = new Variable<float>[] { x, y, z } }; }
        public static Variable<T> New(Variable<float> x, Variable<float> y, Variable<float> z, Variable<float> w) { return new ConstructNode<T>() { inputs = new Variable<float>[] { x, y, z, w } }; }
    }
}
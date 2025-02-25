using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class TreeContext {
        public Dictionary<string, TextureDescriptor> textures;
        public List<string> computeKernels;

        // TODO: Move dispatches to be stored inside the scopes themselves
        public List<KernelDispatch> dispatches;
        public Dictionary<string, int> varNamesToId;
        public PropertyInjector injector;
        public List<string> properties;
        public Hashinator hashinator;
        public int counter;
        public bool debugNames;
        public List<TreeScope> scopes;
        public int currentScope = 0;
        public int scopeDepth = 0;

        public ScopeArgument position;
        public ScopeArgument id;

        public string this[UntypedVariable node] {
            get => scopes[currentScope].namesToNodes[node];
        }

        public int Indent {
            get => scopes[currentScope].indent;
            set => scopes[currentScope].indent = value;
        }

        public List<string> Properties { get { return properties; } }

        public TreeContext(bool debugNames) {
            this.properties = new List<string>();
            this.injector = new PropertyInjector();
            this.varNamesToId = new Dictionary<string, int>();
            this.debugNames = debugNames;
            this.scopes = new List<TreeScope> {
                new TreeScope(0)
            };

            this.hashinator = new Hashinator();

            this.currentScope = 0;
            this.scopeDepth = 0;
            this.counter = 0;
            this.computeKernels = new List<string>();
            this.dispatches = new List<KernelDispatch>();
            this.textures = new Dictionary<string, TextureDescriptor>();
        }

        public void Inject<T>(InjectedNode<T> node, string name, Func<object> func) {
            if (!Contains(node)) {
                string newName = GenId(name);
                injector.injected.Add((compute, textures) => {
                    GraphUtils.SetComputeShaderObj(compute, newName, func(), VariableType.TypeOf<T>());
                });
                properties.Add(VariableType.TypeOf<T>().ToStringType() + " " + newName + ";");
                Add(node, newName);
            }
        }

        public void Inject2(Action<ComputeShader, Dictionary<string, ExecutorTexture>> func) {
            injector.injected.Add(func);
        }

        public void Hash(object val) {
            hashinator.Hash(val);
        }

        public void Add(UntypedVariable node, string name) {
            scopes[currentScope].namesToNodes.Add(node, name);
        }

        public bool Contains(UntypedVariable node) {
            return scopes[currentScope].namesToNodes.ContainsKey(node);
        }

        public void AddLine(string line) {
            string[] aaa = line.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);

            foreach (var item in aaa) {
                scopes[currentScope].AddLine(item);
            }
        }

        public string GenId(string name) {
            int id = 0;

            if (varNamesToId.ContainsKey(name)) {
                id = ++varNamesToId[name];
            } else {
                varNamesToId.Add(name, 0);
            }

            if (debugNames) {
                return name + "_" + id.ToString();
            } else {
                return "_" + ++counter;
            }
        }

        // Binds a name to a no op variable that you can use in your code.
        // Useful when the variable is defined outside of the scope for example
        // Does not generate a custom name for it
        public Variable<T> AliasExternalInput<T>(string name) {
            var a = new NoOp<T> { };
            this.Add(a, name);
            return a;
        }

        // Assign a new variable using its inner value and a name given for it
        // Internally returns a NoOp
        public Variable<T> AssignTempVariable<T>(string name, string value) {
            var a = new NoOp<T> { };
            DefineAndBindNode<T>(a, name, value);
            return a;
        }

        // Literally just declares an assignment from a property/variable to a value. Pretty low-level
        // Does not check for duplicates
        public void AssignRaw(string name, string value) {
            AddLine($"{name} = {value};");
        }

        // Assigns an already defined node to some value in the code
        public void DefineAndBindNode(UntypedVariable node, VariableType type, string name, string value, bool constant = false, bool rngName = true, bool assignOnly = false) {
            if (!Contains(node)) {
                string newName = rngName ? GenId(name) : name;
                string suffix = constant ? "const " : "";

                if (assignOnly) {
                    AssignRaw(newName, value);
                } else {
                    AddLine($"{suffix + type.ToStringType()} {newName} = {value};");
                }
                Add(node, newName);
            }
        }

        public void ApplyInPlaceUnaryOp(UntypedVariable node, string name, string op, string value) {
            if (!Contains(node)) {
                AddLine($"{name} {op}= {value};");
                Add(node, name);
            }
        }



        public void DefineAndBindNode<T>(UntypedVariable node, string name, string value, bool constant = false, bool rngName = true, bool assignOnly = false) {
            DefineAndBindNode(node, VariableType.TypeOf<T>(), name, value, constant, rngName, assignOnly);
        }
    }
}
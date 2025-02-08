using System;
using System.Collections.Generic;
using UnityEngine;

public class TreeContext {
    public Dictionary<string, TextureDescriptor> textures;
    public List<string> computeKernels;
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

    public string this[TreeNode node] {
        get => scopes[currentScope].namesToNodes[node];
    }

    public int Indent {
        get => scopes[currentScope].indent;
        set => scopes[currentScope].indent = value;
    }

    public List<string> Properties { get { return properties; } }

    public ScopeArgument position;

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
                GraphUtils.SetComputeShaderObj(compute, newName, func(), GraphUtils.TypeOf<T>());
            });
            properties.Add(GraphUtils.TypeOf<T>().ToStringType() + " " + newName + ";");
            Add(node, newName);
        }
    }

    public void Inject2(Action<ComputeShader, Dictionary<string, ExecutorTexture>> func) {
        injector.injected.Add(func);
    }

    public void Hash(object val) {
        hashinator.Hash(val);
    }

    public void Add(TreeNode node, string name) {
        scopes[currentScope].namesToNodes.Add(node, name);
    }

    public bool Contains(TreeNode node) {
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

    // Assigns an already defined node to some value in the code
    public void DefineAndBindNode(TreeNode node, GraphUtils.StrictType type, string name, string value, bool constant = false, bool rngName = true, bool assignOnly = false) {
        if (!Contains(node)) {
            string newName = rngName ? GenId(name) : name;
            string suffix = constant ? "const " : "";
            
            if (assignOnly) {
                AddLine(newName + " = " + value + ";");
            } else {
                AddLine(suffix + type.ToStringType() + " " + newName + " = " + value + ";");
            }
            Add(node, newName);
        }
    }

    public void ApplyInPlaceUnaryOp(TreeNode node, string name, string op, string value) {
        if (!Contains(node)) {
            AddLine(name + $" {op}= " + value + ";");
            Add(node, name);
        }
    }

    public void DefineAndBindNode<T>(TreeNode node, string name, string value, bool constant = false, bool rngName = true, bool assignOnly = false) {
        DefineAndBindNode(node, GraphUtils.TypeOf<T>(), name, value, constant, rngName, assignOnly);
    }

    public void Parse(TreeNode[] head) {
        foreach (TreeNode node in head) {
            node.Handle(this);
        }
    }
}
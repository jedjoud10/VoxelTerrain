using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class AtomicOrderingNode : UntypedVariable {
        public override void HandleInternal(TreeContext context) {
        }
    }

    public class AtomicOrderer {
        public enum Axis {
            X, Y, Z
        }

        public enum Mode {
            Minimum, Maximum
        }
        
        public Variable<float> variable;
        public Axis axis;
        public Mode mode;
        public Mode initValue;

        public AtomicOrderer() {   
        }
    }
}
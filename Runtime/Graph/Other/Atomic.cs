using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class AtomicOrderingNode : UntypedVariable {
        public override void HandleInternal(TreeContext context) {
        }
    }

    // given a variable, calculate the min/max value of the variable across all thread groups by splitting the execution into two kernels
    // after the first kernel execution, run an atomic max/min op to keep track of the value during the first compute,
    // then read it in the second kernel
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
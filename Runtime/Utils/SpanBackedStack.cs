using System;
using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public ref struct SpanBackedStack {
        private Span<int> backing;
        private int length;

        public int Length => length;

        public int this[int index] {
            get { return backing[index]; }
        }

        public static SpanBackedStack New(Span<int> backing) {
            SpanBackedStack queue = new SpanBackedStack();
            queue.backing = backing;
            queue.length = 0;
            return queue;
        }

        public void Enqueue(int value) {
            if (length == backing.Length) {
                throw new InvalidOperationException("Backing Span is full. Cannot enqueue");
            }
            
            backing[length] = value;
            length++;
        }

        public bool TryDequeue(out int val) {
            if (length == 0) {
                val = -1;
                return false;
            }

            length--;
            val = backing[length];
            return true;
        }
    }
}
using System;

namespace jedjoud.VoxelTerrain {
    public ref struct SpanBackedStack<T> where T: unmanaged {
        private Span<T> backing;
        private int length;

        public int Length => length;

        public T this[int index] {
            get { return backing[index]; }
        }

        public static SpanBackedStack<T> New(Span<T> backing) {
            SpanBackedStack<T> queue = new SpanBackedStack<T>();
            queue.backing = backing;
            queue.length = 0;
            return queue;
        }

        public void Enqueue(T value) {
            if (length == backing.Length) {
                throw new InvalidOperationException("Backing Span is full. Cannot enqueue");
            }
            
            backing[length] = value;
            length++;
        }

        public bool TryDequeue(out T val) {
            if (length == 0) {
                val = default;
                return false;
            }

            length--;
            val = backing[length];
            return true;
        }

        public void Clear() {
            length = 0;
        }
    }
}
using System;
using System.Collections.Generic;

namespace jedjoud.VoxelTerrain {
    public class QueueDedupped<T> {
        private HashSet<T> set;
        private Queue<T> queue;

        public int Count => queue.Count;
        public QueueDedupped() {
            set = new HashSet<T>();
            queue = new Queue<T>();
        }

        public void Enqueue(T item) {
            if (!set.Contains(item)) {
                queue.Enqueue(item);
            }
        }

        public bool TryDequeue(out T item) {
            if (queue.TryDequeue(out item)) {
                set.Remove(item);
                return true;
            }

            return false;
        }

        public bool IsEmpty() {
            return queue.Count == 0;
        }

        public T[] Take(int maxBatchCount) {
            int count = Math.Min(queue.Count, maxBatchCount);
            T[] array = new T[count];

            for (int i = 0; i < count; i++) {
                array[i] = queue.Dequeue();
                set.Remove(array[i]);
            }

            return array;
        }
    }
}
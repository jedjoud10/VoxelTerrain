using System;
using System.Collections.Generic;
using System.Linq;

namespace jedjoud.VoxelTerrain {
    public class QueueDedupped<T> {
        private HashSet<T> set;
        private Queue<T> queue;

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
            T[] array = queue.AsEnumerable().Take(maxBatchCount).ToArray();
            foreach (T item in array) {
                set.Remove(item);
            }
            return array;
        }
    }
}
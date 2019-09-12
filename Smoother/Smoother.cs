using System.Collections.Generic;

namespace Smoother {
    public abstract class Smoother<T> {

        public T Value { get; private set; }
        public T ImmediateValue { get; private set; }

        protected Smoother(int capacity) {
            _capacity = capacity;
            _values = new Queue<T>(capacity);

            Reset();
        }

        private Queue<T> _values;
        private int _capacity;

        public void AddValue(T value) {
            ImmediateValue = value;

            while (_values.Count >= _capacity) {
                _values.Dequeue();
            }

            _values.Enqueue(value);

            Value = GetAverage(_values.ToArray());
        }

        public void Reset() {
            _values.Clear();

            Value = default (T);
            ImmediateValue = default (T);
        }

        protected abstract T GetAverage(T[] values);

    }
}

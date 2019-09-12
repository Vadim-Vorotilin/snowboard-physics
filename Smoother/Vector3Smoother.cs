using UnityEngine;

namespace Smoother {

    public class Vector3Smoother : Smoother<Vector3> {

        public Vector3Smoother(int capacity) : base(capacity) { }

        protected override Vector3 GetAverage(Vector3[] values) {
            Vector3 sum = Vector3.zero;

            for (int i = 0; i < values.Length; i++) {
                sum += values[i];
            }

            return sum / values.Length;
        }

    }

}
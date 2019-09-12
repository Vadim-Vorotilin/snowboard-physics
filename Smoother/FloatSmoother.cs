namespace Smoother {

    public class FloatSmoother : Smoother<float> {

        public FloatSmoother(int capacity) : base(capacity) { }

        protected override float GetAverage(float[] values) {
            float sum = 0;

            for (int i = 0; i < values.Length; i++) {
                sum += values[i];
            }

            return sum / values.Length;
        }

    }

}
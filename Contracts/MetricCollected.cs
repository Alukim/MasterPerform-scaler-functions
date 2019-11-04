namespace MasterPerform.Contracts
{
    public class MetricCollected
    {
        public MetricCollected(double metric)
            => this.Metric = metric;

        public double Metric { get; set; }
    }
}
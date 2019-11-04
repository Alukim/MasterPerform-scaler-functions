using System.Collections.Generic;

namespace MasterPerform.Models
{
    public class MetricData
    {
        public string PartitionKey { get; } = "Metrics";
        public string RowKey { get; } = "001";
        public string ETag { get; } = "*";
        public List<double> Data { get; set; }
        public double MessageCount { get; set; }
    }
}
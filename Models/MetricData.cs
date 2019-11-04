using System;
using System.Collections.Generic;
using MasterPerform.Utilities;

namespace MasterPerform.Models
{
    public class MetricData
    {
        public string PartitionKey { get; } = "Metrics";
        public string RowKey { get; } = "001";
        public string ETag { get; } = "*";
        public string Content { get; set; }
    }

    public class Content
    {
        public List<double> Data { get; set; }
        public double MessageCount { get; set; }

        public void AddData(double data)
        {
            if (data < 0)
                throw new ArgumentException(nameof(data));

            Data.ShiftLeft();
            Data[Data.Count - 1] = data;
        }
    }
}
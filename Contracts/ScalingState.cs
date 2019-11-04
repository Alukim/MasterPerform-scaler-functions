using System;
using System.Collections.Generic;

namespace MasterPerform.Utilities
{
    public class ScalingState
    {
        public ScalingState(List<double> metricData, double costForPeriod)
        {
            this.MetricData = metricData ?? throw new ArgumentNullException(nameof(metricData));
            this.CurrentPartOfPeriod = 1;
            this.Wait = false;
            this.RestCost = RestCost;
            this.CostForPeriod = costForPeriod;
        }

        public List<double> MetricData { get; set; }

        public int CurrentPartOfPeriod { get; set; }

        public bool Wait { get; set; }

        public double RestCost { get; set; }

        private double CostForPeriod { get; set; }


        public void  NextPart(double costForCurrentPart)
        {
            CurrentPartOfPeriod++;
            RestCost -= costForCurrentPart;

            if (CurrentPartOfPeriod >= MasterPerformMetricsCollector.Period)
            {
                CurrentPartOfPeriod = 1;
                RestCost = CostForPeriod;
            }
        }

        public void AddMetric(double metric)
        {
            if (metric < 0)
                throw new ArgumentException(nameof(metric));

            MetricData.ShiftLeft();
            MetricData[MetricData.Count - 1] = metric;
        }

        public void WaitForEvent()
            => Wait = true;
    }
}
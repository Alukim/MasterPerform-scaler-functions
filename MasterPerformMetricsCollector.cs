using Extreme.Mathematics;
using Extreme.Statistics.TimeSeriesAnalysis;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using MasterPerform.Utilities;
using MasterPerform.Contracts;
using MasterPerform.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterPerform
{
    public static class MasterPerformMetricsCollector
    {
        private static bool IsStarted = false;
        private static int AutoregresiveParam { get; set; }
        private static int IntegrationParam { get; set; }
        private static int MoveAverrageParam { get; set; }
        private static string InstanceId { get; set; }
        private static int Q { get; set; }

        public static int Period { get; set; }
        public static int CostOfOneMachine { get; set; }

        [FunctionName("MetricCollector")]
        public static async Task Run(
            [TimerTrigger("0 * * * * *")] TimerInfo myTimer,
            [OrchestrationClient] DurableOrchestrationClient client,
            [Table("MyTable", "MyPartition", "{queueTrigger}")] MetricData data,
            IAsyncCollector<MetricData> table,
            ILogger log)
        {
            log.LogMetricsCollector($"Executed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var t = await client.GetStatusAsync(InstanceId);

            if (IsStarted)
            {
                log.LogMetricsCollector("Get active message.");
                var currentActiveMessageCount = AzureHelpers.GetCurrentMetrics();

                log.LogMetricsCollector($"Current message count: {currentActiveMessageCount}");

                log.LogMetricsCollector($"Calculate current part metrics.");

                var currentDayActiveMessageCount = currentActiveMessageCount - data.MessageCount;

                data.MessageCount = currentActiveMessageCount;
                await table.AddAsync(data);

                var @event = new MetricCollected(currentDayActiveMessageCount);

                log.LogMetricsCollector($"Raise event to scaling function.");
                await client.RaiseEventAsync(InstanceId, nameof(MetricCollected), @event);

                log.LogMetricsCollector("Done.");
            }
            else
            {
                AutoregresiveParam = EnvironmentHelpers.GetIntegerEnvironmentParameter(nameof(MasterPerformMetricsCollector.AutoregresiveParam));
                IntegrationParam = EnvironmentHelpers.GetIntegerEnvironmentParameter(nameof(MasterPerformMetricsCollector.IntegrationParam));
                MoveAverrageParam = EnvironmentHelpers.GetIntegerEnvironmentParameter(nameof(MasterPerformMetricsCollector.MoveAverrageParam));
                Period = EnvironmentHelpers.GetIntegerEnvironmentParameter(nameof(MasterPerformMetricsCollector.Period));
                Q = EnvironmentHelpers.GetIntegerEnvironmentParameter(nameof(MasterPerformMetricsCollector.Q));
                var costForPeriod = EnvironmentHelpers.GetIntegerEnvironmentParameter("CostForPeriod");
                CostOfOneMachine = EnvironmentHelpers.GetIntegerEnvironmentParameter(nameof(MasterPerformMetricsCollector.CostOfOneMachine));

                log.LogMetricsCollector($"Start first forecasting.");
                IsStarted = true;
                InstanceId = await client.StartNewAsync(nameof(ScalingFunction), new ScalingState(data.Data, costForPeriod));
            }
        }

        [FunctionName("ScalingFunction")]
        public static async Task<ScalingState> ScalingFunction(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            log.LogScalingFunction("Get new input.");

            var scalingState = context.GetInput<ScalingState>();

            log.LogScalingFunction($"Scaling for {scalingState.CurrentPartOfPeriod} part of period.");

            log.LogScalingFunction("Waiting for event from Metrics Collection");

            if (scalingState.Wait)
            {
                var @event = await context.WaitForExternalEvent<MetricCollected>(nameof(MetricCollected));

                log.LogScalingFunction($"Update current metrics data.");

                scalingState.AddMetric(@event.Metric);
            }

            log.LogScalingFunction($"Prepare ARIMA model. Autoregresive: {AutoregresiveParam}, Integration: {IntegrationParam}, Move Averrage: {MoveAverrageParam}");

            var vectorMetricsData = Vector.Create(scalingState.MetricData.ToArray());

            var model = new ArimaModel(vectorMetricsData, AutoregresiveParam, IntegrationParam, MoveAverrageParam);
            model.EstimateMean = true;

            log.LogScalingFunction("Fit ARIMA model.");

            model.Fit();

            log.LogScalingFunction("Forecast capacity for rest period.");
            var period = MasterPerformMetricsCollector.Period - scalingState.CurrentPartOfPeriod - 1;
            var forecastedData = model.Forecast(period);

            log.LogScalingFunction("Calculate capacity");
            var capacityPerDay = forecastedData.Select(z => CapacityHelpers.CalculateCapacity(z)).ToList();
            var cost = capacityPerDay.Select(z => CapacityHelpers.CalculateCostOfCapacity(z)).Sum();

            var restCost = scalingState.RestCost - cost;

            var division = (int)restCost / period;
            if(division >= 1)
            {
                var additionalMachine = division > 2 ? division / MasterPerformMetricsCollector.Q : 1;
                capacityPerDay = capacityPerDay.Select(z => z + additionalMachine).ToList();
                restCost -= (additionalMachine * capacityPerDay.Count);
            }

            log.LogScalingFunction("Get capacity for part of period.");

            var capacity = capacityPerDay.First();

            log.LogScalingFunction($"Capacity to set: {capacity}");
            var action = new ScaleAction(capacity);
            await context.CallActivityAsync(nameof(Scaler), action);

            log.LogScalingFunction("Start new Scaling Function with new scaling state.");
            scalingState.NextPart(CapacityHelpers.CalculateCostOfCapacity(capacity));
            context.ContinueAsNew(scalingState);
            return scalingState;
        }

        [FunctionName("Scaler")]
        public static void Scaler(
            [ActivityTrigger] DurableActivityContext context,
            ILogger log)
        {
            var action = context.GetInput<ScaleAction>();

            var capacity = action.CapacityPlanCount < 2 ? 1 : action.CapacityPlanCount;

            log.LogScaler($"New capacity: {capacity}");

            log.LogScaler($"Executed at: {DateTime.Now}");

            var resourceName = Environment.GetEnvironmentVariable("appServicePlanName");

            log.LogScaler($"Executed for {resourceName} App Service");

            var azure = AzureHelpers.GetAzureConnection();

            log.LogScaler("Successfully authenticated to azure");

            var plan = azure.AppServices
                .AppServicePlans
                .List()
                .First(p => string.Equals(p.Name.ToLower(), resourceName.ToLower()));

            if (plan.Capacity == capacity)
            {
                log.LogScaler($"App service plan capacity: {plan.Capacity} is equal to new capacity: {capacity}. Ending function.");
            }
            else
            {
                log.LogScaler($"Switching {resourceName} from {plan.Capacity} to {capacity}");

                plan.Update()
                    .WithCapacity(capacity)
                    .Apply();

                log.LogScaler($"App Service Capacity: {resourceName} updated.");
            }
        }
    }
}
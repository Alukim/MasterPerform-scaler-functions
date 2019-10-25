using Extreme.Mathematics;
using Extreme.Statistics;
using Extreme.Statistics.TimeSeriesAnalysis;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MasterPerform
{
    public static class MasterPerformMetricsCollector
    {
        private static bool IsStarted = false;
        private static int AutoregresiveParam = 360;
        private static int IntegrationParam = 1;
        private static int MoveAverrageParam = 360;
        private const double DayInSeconds = 600;

        [FunctionName("MetricCollector")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]HttpRequestMessage req,
            //[TimerTrigger("0 */1 * * * *")] TimerInfo myTimer,
            [OrchestrationClient] DurableOrchestrationClient client,
            ILogger log)
        {
            log.LogMetricsCollector($"Executed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if(IsStarted)
            {
                log.LogMetricsCollector("Get active message.");
                var currentActiveMessageCount = Helpers.GetCurrentMetrics();

                log.LogMetricsCollector($"Current message count: {currentActiveMessageCount}");

                log.LogMetricsCollector("Get yesterday active messaga count.");

                var yesterdayActiveMessageCount = 10;

                log.LogMetricsCollector($"Calculate current day metrics.");

                var currentDayActiveMessageCount = currentActiveMessageCount - yesterdayActiveMessageCount;
                var metric = (double)currentDayActiveMessageCount /  DayInSeconds;

                log.LogMetricsCollector($"Metric for day - Requests per second: {metric}");

                var @event = new MetricCollected(metric);

                log.LogMetricsCollector($"Raise event to scaling function.");
                await client.RaiseEventAsync(instanceId, nameof(MetricCollected), @event);

                log.LogMetricsCollector("Done.");
            }
            else
            {
                log.LogMetricsCollector($"Prepare metric for last year.");
                IsStarted = true;
                await client.StartNewAsync(nameof(ScalingFunction), ScalingState.StartedNewPeriod(new double[360]));
            }
        }

        [FunctionName("ScalingFunction")]
        public static async Task<ScalingState> ScalingFunction(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            log.LogScalingFunction("Get new input.");

            var scalingState = context.GetInput<ScalingState>();

            log.LogScalingFunction($"Scaling for {scalingState.CurrentScalingDay} day on {scalingState.CurrentScalingMonth} month.");

            log.LogScalingFunction("Waiting for event from Metrics Collection");

            var @event = await context.WaitForExternalEvent<MetricCollected>(nameof(MetricCollected));

            log.LogScalingFunction($"Update current metrics data.");

            scalingState.AddCurrentDayMetrics(@event.Metric);

            if(scalingState.ForecastData)
            {
                log.LogScalingFunction($"Prepare ARIMA model. Autoregresive: {AutoregresiveParam}, Integration: {IntegrationParam}, Move Averrage: {MoveAverrageParam}");

                var vectorMetricsData = Vector.Create(scalingState.MetricData);
                
                var model = new ArimaModel(vectorMetricsData, AutoregresiveParam, IntegrationParam, MoveAverrageParam);

                log.LogScalingFunction("Fit ARIMA model.");

                model.Fit();

                log.LogScalingFunction("Forecast capacity for month.");
                var forecastedData = model.Forecast(30);

                log.LogScalingFunction("Calculate capacity");
                var capacityPerDay = forecastedData.Select(z => Helpers.CalculateCapacity(z)).ToArray();

                log.LogScalingFunction("Set capacity per day scaling state.");
                scalingState.SetCapacityPerDay(capacityPerDay);
            }

            log.LogScalingFunction("Get capacity for day.");
            var capacity = scalingState.GetCapacityForDay();

            log.LogScalingFunction($"Capacity to set: {capacity}");
            var action = new ScaleAction(capacity);
            await context.CallActivityAsync(nameof(Scaler), action);

            log.LogScalingFunction("Start new Scaling Function with new scaling state.");
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
            
            var resourceName = Helpers.GetEnv("appServicePlanName");

            log.LogScaler($"Executed for {resourceName} App Service");

            var azure = Helpers.GetAzureConnection();

            log.LogScaler("Successfully authenticated to azure");

            var plan = azure.AppServices
                .AppServicePlans
                .List()
                .First(p => string.Equals(p.Name.ToLower(), resourceName.ToLower()));

            if(plan.Capacity == capacity)
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

        public class ScalingState
        {
            public ScalingState(int currentScalingDay, int currentScalingMonth, double[] metricData, int[] capacityPerDay, bool newHistoricalData) 
            {
                this.CurrentScalingDay = currentScalingDay;
                this.CurrentScalingMonth = currentScalingMonth;
                this.MetricData = metricData;
                this.CapacityPerDay = capacityPerDay;
                this.NewHistoricalData = newHistoricalData;
            }
            
            public int CurrentScalingDay { get; set; }

            public int CurrentScalingMonth { get; set; }

            public double[] MetricData { get; set; }

            public int[] CapacityPerDay { get; set; }

            public bool NewHistoricalData { get; set; }

            public bool ForecastData { get; set; }

            public void NextDay()
            {
                if(CurrentScalingDay < 30)
                    ++CurrentScalingDay;
                else
                {
                    CurrentScalingDay = 1;

                    if(CurrentScalingMonth < 12)
                        ++CurrentScalingMonth;
                    else
                    {
                        CurrentScalingMonth = 1;
                    }

                    for(var i = 0; i < 359; ++i)
                        MetricData[i] = MetricData[i+1];

                    MetricData[359] = 0;
                }

                if(CurrentScalingDay == 30)
                    ForecastData = true;
            }

            public void AddCurrentDayMetrics(double metric)
                => this.MetricData[359] = metric;

            public void DataForcasted()
                => this.ForecastData = false;

            public void HistoricalDataAccepted()
                => this.NewHistoricalData = false;

            public void SetCapacityPerDay(int[] capacityPerDay)
                => this.CapacityPerDay = capacityPerDay;

            public int GetCapacityForDay()
                => this.CapacityPerDay[CurrentScalingDay];

            public static ScalingState StartedNewPeriod(double[] metricData) 
            {
                return new ScalingState(
                    currentScalingDay: 1,
                    currentScalingMonth: 1,
                    metricData: metricData,
                    capacityPerDay: Enumerable.Repeat(0, 30).ToArray(),
                    newHistoricalData: true); 
            }
        }

        public class ScaleAction
        {
            public ScaleAction(int capacityPlanCount)
             => this.CapacityPlanCount = capacityPlanCount;

            public int CapacityPlanCount { get; set; }
        }

        public class MetricCollected
        {
            public MetricCollected(double metric)
                => this.Metric = metric;

            public double Metric { get; set; }
        }

        public static class Helpers
        {
            private static double FourMachineThreshold = 19.6;
            private static double ThreeMachineThreshold = 17.6;
            private static double TwoMachineThreshold = 12.6;

            public static int CalculateCapacity(double generatedData)
            {
                if(generatedData >= FourMachineThreshold)
                    return 4;
                
                if(generatedData >= ThreeMachineThreshold)
                    return 3;

                if(generatedData >= TwoMachineThreshold)
                    return 2;

                return 1;
            }

            public static string GetEnv(string env)
                => Environment.GetEnvironmentVariable(env);

            public static IAzure GetAzureConnection()
            {
                var clientId = GetEnv("clientId");
                var clientSecret = GetEnv("clientSecret");
                var tenantId = GetEnv("tenantId");

                var credentials = SdkContext.AzureCredentialsFactory
                    .FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);

                return Azure.Configure()
                    .Authenticate(credentials)
                    .WithDefaultSubscription();
            }

            public static double GetCurrentMetrics()
            {
                var connectionString = Helpers.GetEnv("serviceBusConnectionString");
                var serviceBusQueueName = Helpers.GetEnv("serviceBusQueueName");
                var serviceBusResourceId = Helpers.GetEnv("serviceBusResourceId");

                var azure = Helpers.GetAzureConnection();
                var sbn = azure.ServiceBusNamespaces.GetById(serviceBusResourceId);
                var metricQueue = sbn.Queues.List().First(z => z.Name == serviceBusQueueName);
                return metricQueue.ActiveMessageCount;
            }
        }
    }

    public static class LoggerExtensions
    {
        public static void LogScaler(this ILogger logger, string message)
            => logger.LogInformation($"MasterPerform - Scaler: {message}");

        public static void LogScalingFunction(this ILogger logger, string message)
            => logger.LogInformation($"MasterPerform - ScalingFunction: {message}");

        public static void LogMetricsCollector(this ILogger logger, string message)
            => logger.LogInformation($"MasterPerform - Metrics Collector: {message}");
    }
}
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Fluent;

namespace MasterPerform
{
    public static class MasterPerformMetricsCollector
    {
        // [FunctionName("MasterPerformMetricsCollector")]
        // public static async Task Run(
        //     [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
        //     [OrchestrationClient] DurableOrchestrationClient context,
        //     ILogger log)
        // {
        //     var outputs = new List<string>();

        //     // Replace "hello" with the name of your Durable Activity Function.
        //     outputs.Add(await context.StartNewAsync(nameof(SayHello), "Tokyo"));
        //     outputs.Add(await context.StartNewAsync(nameof(SayHello), "Seattle"));
        //     outputs.Add(await context.StartNewAsync(nameof(SayHello), "London"));

        //     // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
        //     //return outputs;
        // }

        // [FunctionName("SayHello")]
        // public static string SayHello([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        // {
        //     var name = context.GetInput<string>();
        //     log.LogInformation($"Saying hello to {name}.");
        //     return $"Hello {name}!";
        // }

        [FunctionName("Scaler")]
        public static async Task Scaler([HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage req, ILogger log)
        //public static void Scaler([QueueTrigger("Actions")] ScaleAction action, TraceWriter log)
        {
            log.LogInformation($"Scaler executed at: {DateTime.Now}");
            
            var resourceName = Environment.GetEnvironmentVariable("appServicePlanName");

            log.LogInformation($"Scaler executed for {resourceName} App Service");

            var clientId = Environment.GetEnvironmentVariable("clientId");
            var clientSecret = Environment.GetEnvironmentVariable("clientSecret");
            var tenantId = Environment.GetEnvironmentVariable("tenantId");

            var credentials = SdkContext.AzureCredentialsFactory
                .FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);

            var action = await req.Content.ReadAsAsync<ScaleAction>();

            var capacity = action.CapacityPlanCount < 2 ? 1 : action.CapacityPlanCount;

            log.LogInformation($"New capacity: {capacity}");

            var azure = Azure.Configure()
                .Authenticate(credentials)
                .WithDefaultSubscription();

            log.LogInformation("Successfully authenticated to azure");

            var plan = azure.AppServices
                .AppServicePlans
                .List()
                .First(p => string.Equals(p.Name.ToLower(), resourceName.ToLower()));

            if(plan.Capacity == capacity)
            {
                log.LogInformation($"Scaler: App service plan capacity: {plan.Capacity} is equal to new capacity: {capacity}. Ending function.");
            }
            else
            {
                log.LogInformation($"Scaler: Switching {resourceName} from {plan.Capacity} to {capacity}");

                plan.Update()
                    .WithCapacity(capacity)
                    .Apply();
                
                log.LogInformation($"Scaler: App Service Capacity: {resourceName} updated.");
            }
        }

        public class ScaleAction
        {
            public ScaleAction(int capacityPlanCount)
             => this.CapacityPlanCount = capacityPlanCount;

            public int CapacityPlanCount { get; set; }
        }
    }
}
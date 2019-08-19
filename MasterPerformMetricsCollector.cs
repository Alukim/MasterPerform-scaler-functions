using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Fluent;

namespace MasterPerform
{
    public static class MasterPerformMetricsCollector
    {
        [FunctionName("MasterPerformMetricsCollector")]
        public static async Task<List<string>> RunOrchestrator(
            [TimerTrigger("0 */10 * * * *")] TimerInfo myTimer,
            [OrchestrationTrigger] DurableOrchestrationContext context,
            TraceWritter log)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("MasterPerformMetricsCollector_Hello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("MasterPerformMetricsCollector_Hello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("MasterPerformMetricsCollector_Hello", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("MasterPerformMetricsCollector_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("MasterPerformMetricsCollector_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("MasterPerformMetricsCollector", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
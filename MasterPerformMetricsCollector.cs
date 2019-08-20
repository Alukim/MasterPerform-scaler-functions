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
        [FunctionName("MasterPerformMetricsCollector")]
        public static async Task Run(
            [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
            [OrchestrationClient] DurableOrchestrationClient context,
            ILogger log)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.StartNewAsync(nameof(SayHello), "Tokyo"));
            outputs.Add(await context.StartNewAsync(nameof(SayHello), "Seattle"));
            outputs.Add(await context.StartNewAsync(nameof(SayHello), "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            //return outputs;
        }

        [FunctionName("SayHello")]
        public static string SayHello([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            var name = context.GetInput<string>();
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        // [FunctionName("MasterPerformMetricsCollector_HttpStart")]
        // public static async Task<HttpResponseMessage> HttpStart(
        //     [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
        //     [OrchestrationClient]DurableOrchestrationClient starter,
        //     ILogger log)
        // {
        //     // Function input comes from the request content.
        //     string instanceId = await starter.StartNewAsync("MasterPerformMetricsCollector", null);

        //     log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

        //     return starter.CreateCheckStatusResponse(req, instanceId);
        // }
    }
}
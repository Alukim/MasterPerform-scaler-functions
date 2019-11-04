using System;
using System.Linq;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace MasterPerform.Utilities
{
    public static class AzureHelpers
    {
         public static IAzure GetAzureConnection()
        {
            var clientId = Environment.GetEnvironmentVariable("clientId");
            var clientSecret = Environment.GetEnvironmentVariable("clientSecret");
            var tenantId = Environment.GetEnvironmentVariable("tenantId");

            var credentials = SdkContext.AzureCredentialsFactory
                .FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);

            return Azure.Configure()
                .Authenticate(credentials)
                .WithDefaultSubscription();
        }

        public static double GetCurrentMetrics()
        {
            var connectionString = Environment.GetEnvironmentVariable("serviceBusConnectionString");
            var serviceBusQueueName = Environment.GetEnvironmentVariable("serviceBusQueueName");
            var serviceBusResourceId = Environment.GetEnvironmentVariable("serviceBusResourceId");

            var azure = AzureHelpers.GetAzureConnection();
            var sbn = azure.ServiceBusNamespaces.GetById(serviceBusResourceId);
            var metricQueue = sbn.Queues.List().First(z => z.Name == serviceBusQueueName);
            return metricQueue.ActiveMessageCount;
        }
    }
}
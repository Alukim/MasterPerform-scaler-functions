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
            var clientId = Environment.GetEnvironmentVariable("ClientId");
            var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
            var tenantId = Environment.GetEnvironmentVariable("TenantId");

            var credentials = SdkContext.AzureCredentialsFactory
                .FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);

            return Azure.Configure()
                .Authenticate(credentials)
                .WithDefaultSubscription();
        }

        public static double GetCurrentMetrics()
        {
            var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            var serviceBusQueueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
            var serviceBusResourceId = Environment.GetEnvironmentVariable("ServiceBusResourceId");

            var azure = AzureHelpers.GetAzureConnection();
            var sbn = azure.ServiceBusNamespaces.GetById(serviceBusResourceId);
            var metricQueue = sbn.Queues.List().First(z => z.Name == serviceBusQueueName);
            return metricQueue.ActiveMessageCount;
        }
    }
}
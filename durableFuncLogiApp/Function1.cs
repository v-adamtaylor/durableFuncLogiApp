using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace durableFuncLogiApp
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<HttpResponseMessage> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            var outputs = new List<string>();

            try
            {
                DateTime dueTime = context.CurrentUtcDateTime.AddSeconds(10);
                await context.CreateTimer(dueTime, CancellationToken.None);

                // Replace "hello" with the name of your Durable Activity Function.
                outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Tokyo"));
                outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Seattle"));
                outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "London"));


                // Setting custom status to "Completed"
                context.SetCustomStatus("Custom->Complete");

                // If everything is successful, return a 200 OK status.
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(outputs), Encoding.UTF8, "application/json")
                };
            }
            catch (FunctionFailedException ex)
            {
                log.LogError($"Function failed with exception: {ex.Message}");

                // Setting custom status to "Failed"
                context.SetCustomStatus("Custom->Fail");

                // If a function fails, return a 500 Internal Server Error status.
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"Function execution failed: {ex.Message}", Encoding.UTF8, "application/json")
                };
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred: {ex.Message}");

                // Setting custom status to "Error"
                context.SetCustomStatus("Custom->Error");

                // For other types of exceptions, you might choose to return a 400 Bad Request or another appropriate status.
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent($"An error occurred: {ex.Message}", Encoding.UTF8, "application/json")
                };
            }
        }

        [FunctionName("Function1_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");

            // Introducing random failure
            var rand = new Random();
            if (rand.Next(0, 5) == 0) // 1/6 Chance of Failure
            {
                throw new Exception("Random failure occurred");
            }

            return $"Hello {name}!";
        }

        [FunctionName("Function1_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("Function1", null);

            log.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
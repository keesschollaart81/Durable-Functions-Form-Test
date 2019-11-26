using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Linq;
using System.Collections.Generic;

namespace BackendCSharp
{
    public class FormFunctions
    {
        [FunctionName(nameof(HttpTrigger))]
        public async Task<IActionResult> HttpTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient durableOrchestrationClient,
            [Blob("filestorage/test", FileAccess.Write)] CloudBlobContainer cloudBlobContainer,
            ILogger log)
        {
            log.LogInformation("Receiving http request");
            await cloudBlobContainer.CreateIfNotExistsAsync();

            var instanceId = Guid.NewGuid().ToString();
            var name = req?.Form["Name"];
            var file = req?.Form?.Files?.FirstOrDefault();
            if (file != null)
            {

                var blobReference = cloudBlobContainer.GetBlockBlobReference(instanceId);
                using (var fileStream = file.OpenReadStream())
                {
                    await blobReference.UploadFromStreamAsync(fileStream);
                }
            }
            var orchestrationId = await durableOrchestrationClient.StartNewAsync(nameof(Orchestration), instanceId, new OrchestrationParams(name, DateTime.Now, file != null));

            return durableOrchestrationClient.CreateCheckStatusResponse(req, orchestrationId);
        }

        [FunctionName(nameof(Orchestration))]
        public async Task Orchestration(
          [OrchestrationTrigger] IDurableOrchestrationContext context,
          ILogger logger,
          ExecutionContext executionContext)
        {
            var started = context.CurrentUtcDateTime;
            var orchestrationParams = context.GetInput<OrchestrationParams>();

            logger.LogMetric("OrchestratorStartLatency", (started - orchestrationParams.Triggered).TotalMilliseconds);

            if (orchestrationParams.IncludesFile)
            {
                context.SetCustomStatus("Processing file");
                var fileProcessingResult = await context.CallActivityAsync<ProcessFileResult>(nameof(ProcessFile), context.InstanceId);

                context.SetCustomStatus("Processing lines");
                var parallelTasks = new List<Task>();
                foreach (var id in fileProcessingResult.Ids)
                {
                    var task = context.CallActivityAsync(nameof(ProcessRow), id);
                    parallelTasks.Add(task);
                }

                await Task.WhenAll(parallelTasks);
            }
            context.SetCustomStatus("Done");

            logger.LogMetric("OrchestratorDuration", (DateTime.UtcNow - started).TotalMilliseconds);
        }

        [FunctionName(nameof(ProcessFile))]
        public async Task<ProcessFileResult> ProcessFile(
           [ActivityTrigger] string orchestrationId,
           [Blob("filestorage/{orchestrationId}", FileAccess.Read)] CloudBlockBlob cloudBlockBlob,
           ILogger logger)
        {
            var fileContents = await cloudBlockBlob.DownloadTextAsync();

            var result = new ProcessFileResult();

            foreach (var line in fileContents.Split(Environment.NewLine))
            {
                var parts = line.Split(',');
                result.Ids.Add(parts[0]);
            }

            return result;
        }

        [FunctionName(nameof(ProcessRow))]
        public async Task ProcessRow(
           [ActivityTrigger] string rowId,
           ILogger logger)
        {
            // fake IO
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    public class ProcessFileResult
    {
        public List<string> Ids = new List<string>();
    }

    public class OrchestrationParams
    {
        public OrchestrationParams(string name, DateTime triggered, bool includesFile)
        {
            Name = name;
            Triggered = triggered;
            IncludesFile = includesFile;
        }
        public string Name { get; set; }
        public bool IncludesFile { get; set; }
        public DateTime Triggered { get; set; }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Net.Http;
using System.Net;
using System.Text;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

namespace BackendCSharp
{
    public class OtherFunctions
    { 

        [FunctionName(nameof(HelloWorldHttpTrigger))]
        public IActionResult HelloWorldHttpTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            ILogger log)
        {
            return new OkResult();
        } 

        [FunctionName(nameof(Form))]
        public static HttpResponseMessage Form(
            [HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req,
            ExecutionContext context)
        {
            var path = Path.Combine(context.FunctionAppDirectory, "Form.html");
            var content = File.ReadAllText(path);

            var result = new HttpResponseMessage(HttpStatusCode.OK);
            result.Content = new StringContent(content, Encoding.UTF8, "text/html");

            return result;
        }

    } 
}

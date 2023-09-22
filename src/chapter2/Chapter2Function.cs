using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Memory;

namespace My.MyChatFunction
{
    public class Chapter2Function
    {
        private readonly ILogger _logger;
        private readonly IKernel _kernel;
        private readonly IChatCompletion _chat;
        private readonly ChatHistory _chatHistory;

        public Chapter2Function(ILoggerFactory loggerFactory, IKernel kernel, ChatHistory chatHistory, IChatCompletion chat)
        {
            _logger = loggerFactory.CreateLogger<Chapter2Function>();
            _kernel = kernel;
            _chat = chat;
            _chatHistory = chatHistory;
        }

        [Function("MyChatFunction")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            _chatHistory!.AddMessage(AuthorRole.User, await req.ReadAsStringAsync() ?? string.Empty);
            //string message = await SearchMemoriesAsync(_kernel, await req.ReadAsStringAsync() ?? string.Empty);
            //_chatHistory!.AddMessage(AuthorRole.User, message);

            string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());

            _chatHistory.AddMessage(AuthorRole.Assistant, reply);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString(reply);
            return response;
        }

        private async Task<string> SearchMemoriesAsync(IKernel kernel, string query)
        {
            StringBuilder result = new StringBuilder();
            result.Append("The below is relevant information.\n[START INFO]");

            const string memoryCollectionName = "ms10k";

            IAsyncEnumerable<MemoryQueryResult> queryResults =
                kernel.Memory.SearchAsync(memoryCollectionName, query, limit: 3, minRelevanceScore: 0.77);

            // For each memory found, get previous and next memories.
            await foreach (MemoryQueryResult r in queryResults)
            {
                int id = int.Parse(r.Metadata.Id);
                MemoryQueryResult? rb2 = await kernel.Memory.GetAsync(memoryCollectionName, (id - 2).ToString());
                MemoryQueryResult? rb = await kernel.Memory.GetAsync(memoryCollectionName, (id - 1).ToString());
                MemoryQueryResult? ra = await kernel.Memory.GetAsync(memoryCollectionName, (id + 1).ToString());
                MemoryQueryResult? ra2 = await kernel.Memory.GetAsync(memoryCollectionName, (id + 2).ToString());

                if (rb2 != null) result.Append("\n " + rb2.Metadata.Id + ": " + rb2.Metadata.Description + "\n");
                if (rb != null) result.Append("\n " + rb.Metadata.Description + "\n");
                if (r != null) result.Append("\n " + r.Metadata.Description + "\n");
                if (ra != null) result.Append("\n " + ra.Metadata.Description + "\n");
                if (ra2 != null) result.Append("\n " + ra2.Metadata.Id + ": " + ra2.Metadata.Description + "\n");
            }

            result.Append("\n[END INFO]");
            result.Append($"\n{query}");

            Console.WriteLine(result);
            return result.ToString();
        }
    }
}

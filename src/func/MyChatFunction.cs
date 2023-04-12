using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace My.ChatFunction
{
    public class MyChatFunction
    {
        private readonly ILogger _logger;
        private readonly IChatCompletion _chat;
        private readonly ChatHistory _chatHistory;

        public MyChatFunction(ILoggerFactory loggerFactory, ChatHistory chatHistory, IChatCompletion chat)
        {
            _logger = loggerFactory.CreateLogger<MyChatFunction>();
            _chat = chat;
            _chatHistory = chatHistory;
        }

        [Function("MyChatFunction")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            _chatHistory!.AddMessage("user", await req.ReadAsStringAsync() ?? string.Empty);

            string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());

            _chatHistory.AddMessage("assistant", reply);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString(reply);
            return response;
        }
    }
}

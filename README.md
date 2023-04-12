> **WORK IN-PROGRESS**
# Tutorial: ChatGPT + Enterprise data with Semantic Kernel, Azure OpenAI and Azure Cognitive Search

This progressive tutorial is for building your own AI chat application informed with your enterprise data. In chapter one, we start with building a simple [ChatGPT](https://platform.openai.com/docs/models/gpt-3-5)-like application. Chapter two imports files (PDFs) for reference by the [Semantic Kernel](https://aka.ms/skrepo) orchestrator when chatting. Chapter three extends the context of the chat by implementing the [Retrieval Augmented Generation](https://arxiv.org/abs/2005.11401) pattern with [Azure Cognitive Search](https://learn.microsoft.com/en-us/azure/search/) for data indexing and retrieval.

In all, this tutorial creates a minimal implementation for using [Semantic Kernel](https://aka.ms/skrepo) as a foundation for enabling enterprise data ingestion, long-term memory, plug-ins, and more.

# Chapter 1: Chat

## Configure your environment
Before you get started, make sure you have the following requirements in place:
- [Visual Studio Code](http://aka.ms/vscode) with extensions:
  - [C# Extension](https://aka.ms/csharp/vscode)
  - [Azure Functions Extension](https://aka.ms/azfn/vscode)
- [.NET 7.0 SDK](https://aka.ms/net70) for building and deploying .NET 7 projects.
- [Azure Function Core Tools 4.x](https://aka.ms/azfn/coretools) for managing Azure Functions

## Create an Azure Function project.
1. In Visual Studio Code, click on the Azure extension (or press `SHIFT+ALT+A`).
1. Mouse-over `WORKSPACE` and select `Create Function` (i.e., +⚡) to create a new local Azure function project.
1. Select `Browse`, choose/create the folder where you want to create your Azure Function code (e.g., `myrepo/src/myfunc`) then use the selections below when creating the project:

   | Selection       | Value                       |
   | ---------       | -----                       |
   | Language        | `C#`                        |
   | Runtime         | `.NET 7 Isolated`           |
   | Template        | `Http trigger`              |
   | Function name   | `MyChatFunction`            |
   | Namespace       | `My.MyChatFunction`         |
   | Access rightgs  | `Function`                  |

## Add Semantic Kernel to your Azure Function
In this section will create a minimal implementation for using Semantic Kernel as a foundation for enabling scenarios such as enterprise data ingestion, long-term memory, plug-ins, and more. 

1. Open a terminal window, change to the directory with your project file (e.g., `myrepo/src/myfunc`), 
   and run the `dotnet` command below to add the Semantic Kernel NuGet package to your project.
   ```bash
   dotnet add package Microsoft.SemanticKernel --prerelease
   ```

1. Back in your Azure Function project in Visual Studio Code, open the `Program.cs` file and replace with the content below. 
    > This updates the `HostBuilder` to read configuration variables from the environment.
    ```csharp
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.AI.ChatCompletion;

    var hostBuilder = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults();

    hostBuilder.ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    });

    IHost host = hostBuilder.Build();
    host.Run();
    ```

1. Add the Semantic Kernel by adding a `ConfigureServices` call below the existing `ConfigureAppConfiguration` and populating it with an instance of the kernel.
    > The kernel below is configured to use an OpenAI ChatGPT model (e.g., gpt-3.5-turbo) for chat completions.

    > TODO: Add instructions for getting an [OpenAI API key](https://platform.openai.com/account/api-keys) and setting the `OPENAI_APIKEY` environment variable.
    ```csharp
    hostBuilder.ConfigureServices(services =>
    {
        services.AddSingleton<IKernel>(sp =>
        {
            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string apiKey = configuration["OPENAI_APIKEY"];

            IKernel kernel = new KernelBuilder()
                .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
                .Configure(config => config.AddOpenAIChatCompletionService(
                    serviceId: "chat",
                    modelId: "gpt-3.5-turbo",
                    apiKey: apiKey))
                .Build();

            return kernel;
        });
    });
    ```

1. Enable a chat service and in-memory storage for the chat history by adding two more singletons inside the `ConfigureServices` call.
    ```csharp
    services.AddSingleton<IChatCompletion>(sp =>
        sp.GetRequiredService<IKernel>().GetService<IChatCompletion>());

    const string instructions = "You are a helpful friendly assistant.";
    services.AddSingleton<ChatHistory>(sp =>
        sp.GetRequiredService<IChatCompletion>().CreateNewChat(instructions));
    ```

1. Open your function code file (e.g., `MyChatFunction.cs`) and add the chat completion using statement to the top.
    ```csharp
    using Microsoft.SemanticKernel.AI.ChatCompletion;
    ```
    Replace the private members and constructor to include our chat history and chat completion services.
    ```csharp
    private readonly ILogger _logger;
    private readonly IChatCompletion _chat;
    private readonly ChatHistory _chatHistory;

    public MyChatFunction(ILoggerFactory loggerFactory, ChatHistory chatHistory, IChatCompletion chat)
    {
        _logger = loggerFactory.CreateLogger<MyChatFunction>();
        _chat = chat;
        _chatHistory = chatHistory;
    }

    ```

1. Update the `Run` method to read the user's chat message, add it to the chat history, use our chat service to call ChatGPT and generate a reply message, add the AI's reply to our chat history, and finally, send the reply back to the caller.
    ```csharp
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
    ```

1. The complete code files (with additional comments).
    <details>
    <summary>Program.cs</summary>

    ```csharp
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.AI.ChatCompletion;

    var hostBuilder = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults();

    hostBuilder.ConfigureAppConfiguration((context, config) =>
    {
        config.AddEnvironmentVariables();
    });

    hostBuilder.ConfigureServices(services =>
    {
        services.AddSingleton<IKernel>(sp =>
        {
            // Retrieve the OpenAI API key from the configuration/environment.
            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string apiKey = configuration["OPENAI_APIKEY"];

            // Construct a semantic kernel and connect the OpenAI chat completion APIs.
            IKernel kernel = new KernelBuilder()
                .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
                .Configure(config => config.AddOpenAIChatCompletionService(
                    serviceId: "chat",
                    modelId: "gpt-3.5-turbo",
                    apiKey: apiKey))
                .Build();

            return kernel;
        });

        // Provide a chat completion service client to our function.
        services.AddSingleton<IChatCompletion>(sp =>
            sp.GetRequiredService<IKernel>().GetService<IChatCompletion>());

        // Provide a persistant in-memory chat history store with the 
        // initial ChatGPT system message.
        const string instructions = "You are a helpful friendly assistant.";
        services.AddSingleton<ChatHistory>(sp =>
            sp.GetRequiredService<IChatCompletion>().CreateNewChat(instructions));
    });

    IHost host = hostBuilder.Build();
    host.Run();
    ```
    </details>

    <details>
    <summary>MyChatFunction.cs</summary>

    ```csharp
    using System.Net;
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Azure.Functions.Worker.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.SemanticKernel.AI.ChatCompletion;

    namespace My.MyChatFunction
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

                // Add the user's chat message to the history.
                _chatHistory!.AddMessage("user", await req.ReadAsStringAsync() ?? string.Empty);

                // Send the history to the AI and receive a reply.
                string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());

                // Add the AI's reply to the chat history.
                _chatHistory.AddMessage("assistant", reply);

                // Send the AI's response back to the caller.
                HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
                response.WriteString(reply);
                return response;
            }
        }
    }
    ```
    </details>

## Run the function locally
1. Run your Azure Function locally by opening a terminal, changing directory to your Azure Function project (e.g., `myrepo/src/myfunc`), and starting the function by running
    ```bash
    func start
    ```
    > Make note of the URL displayed (e.g., `http://localhost:7071/api/MyChatFunction`).

1. Start the test console application
   Open a second terminal and change directory to the `chatconsole` project folder (e.g., `myrepo/src/chatconsole`) and run the application using the Azure Function URL.
   ```bash
   dotnet run http://localhost:7071/api/MyChatFunction
   ```

# Chapter 2: Memory Stores and Your Data
> TODO describe

## Configure your environment
Before you get started, make sure you have the following additional requirements in place:
- [Docker Desktop](https://www.docker.com/products/docker-desktop) for hosting the [Qdrant](https://github.com/qdrant/qdrant) vector search engine.



## Qdrant
> TODO explain vector DBs
1. Open a terminal window, change to the directory with your project file (e.g., `myrepo/src/myfunc`), 
   and run the `dotnet` commands below to add Semantic Kernel Qdrant Memory and BlingFire tokenizer to your project.
    ```bash
    dotnet add package Microsoft.SemanticKernel.Connectors.Memory.Qdrant --prerelease
    dotnet add package BlingFireNuget
    ```
1. {add memory store}
    ```csharp
    using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
    using BlingFire;
    ```
    ```csharp
    QdrantMemoryStore memoryStore = new QdrantMemoryStore(
        host: "http://localhost",
        port: 6333,
        vectorSize: 1536,
        logger: sp.GetRequiredService<ILogger<QdrantMemoryStore>>());
    ```
1. {add to SK and add embedding generation, needs explanation}
    > TODO explain embeddings
    ```csharp
     IKernel kernel = new KernelBuilder()
        .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
        .Configure(config => config.AddOpenAIChatCompletionService(
            serviceId: "chat",
            modelId: "gpt-3.5-turbo",
            apiKey: apiKey))
        .Configure(c => c.AddOpenAIEmbeddingGenerationService(
            serviceId: "embedding",
            modelId: "text-embedding-ada-002",
            apiKey))
        .WithMemoryStorage(memoryStore)
        .Build();
    ```

1. {import data into vector db before the call to host.run}
    > This code reads a text file, splits it into sentences, and saves each sentence in a memory collection.

    > We have included Microsoft's 2022 10-K financial report to use as example data. You can find it in this repo at `data/ms10k.txt`.
    ```csharp
    string filePath = "{path to text file}"; // Example: c:/src/repo/data/ms10k.txt
    const string memoryCollectionName = "{collectioName}"; // Example: ms10k
    
    IKernel kernel = host.Services.GetRequiredService<IKernel>();
    
    IEnumerable<string> sentences = 
        BlingFireUtils.GetSentences(File.ReadAllText(filePath));

    int i = 0;
    foreach (var sentence in sentences)
    {
        if (i % 100 == 0) Console.WriteLines($"{i}/{sentences.Count()}");
        kernel.Memory.SaveInformationAsync(
            collection: memoryCollectionName,
            text: sentence,
            id: (i++).ToString(),
            description: sentence)
            .GetAwaiter().GetResult();
    }
    ```

## Deploy and start Qdrant VectorDB locally
1. Open a terminal and pull down the container image for Qdrant.
    ```bash
    docker pull qdrant/qdrant
    ```
1. Start running the Qdrant container on port 6333
    ```bash
    docker run -p 6333:6333 qdrant/qdrant
    ```

# Chapter 3: Azure Cognitive Search and Retrieval Augmented Generation
>TODO

# Appendix
## Deploy Azure Function to Azure
> **WORK IN-PROGRESS**
1. If you don't already have an Azure account go to https://azure.microsoft.com, click on `Try Azure for free`, and select `Start Free` to start creating a free Azure account with your Microsoft or GitHub account. After signing in, you will be prompted to enter some information.

    > This tutorial uses **Azure Functions** ([pricing](https://azure.microsoft.com/en-us/pricing/details/functions/)) and **Azure Cognitive Search** ([pricing](https://azure.microsoft.com/pricing/details/search/)) that may incur a monthly cost. Visit [here](https://azure.microsoft.com/free/cognitive-search/) to get some free Azure credits to get you started.

1. In Visual Studio Code, click on the Azure extension (or press `SHIFT+ALT+A`)
1. Mouse-over `RESOURCES` again and select `Create Resource` (i.e., +), select `Create Function App in Azure...`, select your Azure Subscription.
1. Enter a name for your deployed function, for example `fn-mychatfunction`.
1. Set the runtime stack to `.NET 7 Isolated` and choose a location in which to deploy your Azure Function.
    > If you don't have a preference, choose the recommended region.
1. Wait until the `Create Function App` completes, which should only take a minute or so.

1. Mouse-over `WORKSPACE` and select `Deploy` (i.e., ☁️) then `Deploy to Function App`. 
1. Select the same Azure Subscription in which you created the Azure Function in Azure, then select the Azure Function you created above (e.g., `fn-mychatfunction`).
    > It may take a minute or two to complete the deployment.




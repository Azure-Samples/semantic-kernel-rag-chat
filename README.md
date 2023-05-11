# Tutorial: ChatGPT + Enterprise data with Semantic Kernel, Azure OpenAI and Azure Cognitive Search
This progressive tutorial is for building your own AI chat application informed with your enterprise data. In chapter one, we start with building a simple [ChatGPT](https://platform.openai.com/docs/models/gpt-3-5)-like application. Chapter two imports files (PDFs) into a “Memories Store” for reference by the [Semantic Kernel](https://aka.ms/skrepo) (SK) orchestrator when chatting. Having the data from these PDFs allows SK to build better Prompts so the AI can offer better answers to questions – this is a key part of the Retrieval Augmented Generation pattern. Chapter three extends the context of the chat by using [Azure Cognitive Search](https://learn.microsoft.com/en-us/azure/search/) for data indexing and retrieval.

In all, this tutorial creates a minimal implementation for using [Semantic Kernel](https://aka.ms/skrepo) as a foundation for enabling enterprise data ingestion, long-term memory, plug-ins, and more.

Special thanks to Adam Hurwitz's for his [SemanticQuestion10K](https://github.com/adhurwit/SemanticQuestion10K) sample, which was used in Chapter 2.

- [x] [Chapter 1: ChatGPT](#chapter-1-chatgpt) - **complete**
- [x] [Chapter 2: Memories of Enterprise Data](#chapter-2-memories-of-enterprise-data) - **complete**
- [ ] [Chapter 3: Azure Cognitive Search and Retrieval Augmented Generation](#chapter-3-azure-cognitive-search-and-retrieval-augmented-generation) - **in progress**


# Chapter 1: ChatGPT
In this section will create a minimal chat implementation for a chat application that uses Semantic Kernel Semantic Kernel as a foundation for enterprise data ingestion, long-term memory, plug-ins, and more. We will write a C# Azure Function in detail from scratch that wraps all AI calls using SK and we will use a prebuilt C# console app as our UI for the chat app.

## Configure your environment
Before you get started, make sure you have the following requirements in place:
- [Visual Studio Code](http://aka.ms/vscode) with extensions:
  - [C# Extension](https://aka.ms/csharp/vscode)
  - [Azure Functions Extension](https://aka.ms/azfn/vscode)
- [.NET 7.0 SDK](https://aka.ms/net70) for building and deploying .NET 7 projects.
- [Azure Function Core Tools 4.x](https://aka.ms/azfn/coretools) for managing Azure Functions
- [OpenAI API key](https://platform.openai.com/account/api-keys) for using the OpenAI API (or click [here](https://platform.openai.com/signup) to signup).

> TODO: Add instructions for setting the `OPENAI_APIKEY` environment variable.

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
   | Access rights   | `Function`                  |

## Add Semantic Kernel to your Azure Function
1. Open a terminal window, change to the directory with your project file (e.g., `myrepo/src/myfunc`), 
   and run the `dotnet` command below to add the Semantic Kernel NuGet package to your project.
   ```bash
   dotnet add package Microsoft.SemanticKernel --prerelease
   ```

1. Back in your Azure Function project in Visual Studio Code, open the `Program.cs` file and replace everything in the file with the content below. 
    > This updates the `HostBuilder` to read configuration variables from the environment and sets up a reference to the SK runtime.
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

    hostBuilder.Build().Run();
    ```

1. Add the Semantic Kernel by adding a `ConfigureServices` call below the existing `ConfigureAppConfiguration` and populating it with an instance of the kernel.
    > The kernel below is configured to use an OpenAI ChatGPT model (e.g., gpt-3.5-turbo) for chat completions.

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
    Replace the private members and constructor to include the chat history and chat completion services – these will be used to give the AI a history of the conversation (since the AI is stateless) and to make calls to the AI.
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
        _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, await req.ReadAsStringAsync() ?? string.Empty);
        string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());
        _chatHistory.AddMessage(ChatHistory.AuthorRoles.Assistant, reply);
        
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

    hostBuilder.Build().Run();
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
                // Add the user's chat message to the history.
                _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, await req.ReadAsStringAsync() ?? string.Empty);

                // Send the chat history to the AI and receive a reply.
                string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());

                // Add the AI's reply to the chat history for next time.
                _chatHistory.AddMessage(ChatHistory.AuthorRoles.Assistant, reply);

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
1. Type a message and press enter to verify that we are able to chat with the AI!
    ```
    Input: Hello, how are you?
    AI: Hello! As an AI language model, I don't have feelings, but I'm functioning properly and ready to 
    assist you. How can I help you today?
    ```
   
 1. Now let's try to ask about something that is not in the current AI model, such as "What was Microsoft's total revenue for 2022?"
    ```
    Input: What was Microsoft's cloud revenue for 2022?
    AI: I'm sorry, but I cannot provide information about Microsoft's cloud revenue for 2022 as it is not yet 
    available. Microsoft's fiscal year 2022 ends on June 30, 2022, and the company typically releases its 
    financial results a few weeks after the end of the fiscal year. However, Microsoft's cloud revenue for 
    fiscal year 2021 was $59.5 billion, an increase of 34% from the previous year.
    ```
    As you can see the AI is a bit out of date with its answers.
    
    In Chapter 1 we created an Azure function hosting semantic kernel that makes it easy to send API calls we want to make to the AI.  This gives us a shared, production ready endpoint that we could use from any given solution we want to build.

    Next we'll add a 'knowledge base' to the chat to help answer questions such as those above more accurately.
   
# Chapter 2: Memories of Enterprise Data
Semantic Kernel's memory stores are used to integrate data from your knowledge base into AI interactions.
Any data can be added to a knowledge base and you have full control of that data and who it is shared with.
SK uses [embeddings](https://platform.openai.com/docs/guides/embeddings) to encode data and store it in a 
vector database. Using a vector database also allows us to use vector search engines to quickly find the most 
relevant data for a given query that we then share with the AI. In this chapter, we'll add a memory store to 
our chat function, import the Microsoft revenue data, and use it to answer the question from Chapter 1.

## Configure your environment
Before you get started, make sure you have the following additional requirements in place:
- [Docker Desktop](https://www.docker.com/products/docker-desktop) for hosting the [Qdrant](https://github.com/qdrant/qdrant) vector search engine.
   > Note that a different vector store, such as Pinecone or Weviate, could be leveraged.

## Add a memory store in our Azure Function
1. Open a terminal window, change to the directory with your project file (e.g., `myrepo/src/myfunc`), 
   and run the `dotnet` commands below to add Semantic Kernel Qdrant Memory Store to your project.
    ```bash
    dotnet add package Microsoft.SemanticKernel.Connectors.Memory.Qdrant --prerelease
    ```

1. Open your Program code file (e.g., `Program.cs`) and add the Qdrant memory store using statement to the top.
    ```csharp
    using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
    ```

    Replace the builder code we wrote in Chapter #1, where we instantiate the Semantic Kernel, to include a Qdrant memory store and an OpenAI embedding generation service.
    ```csharp
    QdrantMemoryStore memoryStore = new QdrantMemoryStore(
        host: "http://localhost",
        port: 6333,
        vectorSize: 1536,
        logger: sp.GetRequiredService<ILogger<QdrantMemoryStore>>());

     IKernel kernel = new KernelBuilder()
        .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
        .Configure(config => config.AddOpenAIChatCompletionService(
            serviceId: "chat",
            modelId: "gpt-3.5-turbo",
            apiKey: apiKey))
        .Configure(c => c.AddOpenAITextEmbeddingGenerationService(
            serviceId: "embedding",
            modelId: "text-embedding-ada-002",
            apiKey))
        .WithMemoryStorage(memoryStore)
        .Build();
    ```

1. Open `MyChatFunction.cs` and replace where we add the user's message to the chat history
   (`_chatHistory!.AddMessage(ChatHistory.AuthorRoles.User,...`) with a call that will search for related memories and include them
   in the user's message to the AI.
	```csharp
    // _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, await req.ReadAsStringAsync() ?? string.Empty);
    string message = await SearchMemoriesAsync(_kernel, await req.ReadAsStringAsync() ?? string.Empty);
    _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, message);
	```

1. And finally we'll add the `SearchMemoriesAsync` method to this class.
    > The strategy of this memory search is to find memories that are similar to the user's input and then
    > include those memories in the user's message to the AI. This is done by first searching for memories
    > that are similar to the user's input and including the previous and subsequent memories. 
    > These memories provide the AI with context for the user's input.
    ```csharp
    private async Task<string> SearchMemoriesAsync(IKernel kernel, string query)
    {
        StringBuilder result = new StringBuilder();
        result.Append("The below is relevant information.\n[START INFO]");
        
        // Search for memories that are similar to the user's input.
        const string memoryCollectionName = "ms10k";
        IAsyncEnumerable<MemoryQueryResult> queryResults = 
            kernel.Memory.SearchAsync(memoryCollectionName, query, limit: 3, minRelevanceScore: 0.77);

        // For each memory found, try to get previous and next memories.
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

        return result.ToString();
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
    using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;

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

            // Create a memory store that will be used to store memories.
            QdrantMemoryStore memoryStore = new QdrantMemoryStore(
               host: "http://localhost",
               port: 6333,
               vectorSize: 1536,
               logger: sp.GetRequiredService<ILogger<QdrantMemoryStore>>());

            // Create the kerne with chat completion, embedding generation, and memory storage.
            IKernel kernel = new KernelBuilder()
                .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
                .Configure(config => config.AddOpenAIChatCompletionService(
                    serviceId: "chat",
                    modelId: "gpt-3.5-turbo",
                    apiKey: apiKey))
                .Configure(c => c.AddOpenAITextEmbeddingGenerationService(
                    serviceId: "embedding",
                    modelId: "text-embedding-ada-002",
                    apiKey: apiKey))
                .WithMemoryStorage(memoryStore)
                .Build();

            return kernel;
        });

        // Register the chat completion service.
        services.AddSingleton<IChatCompletion>(sp =>
        sp.GetRequiredService<IKernel>().GetService<IChatCompletion>());

        // Create a new chat history.
        const string instructions = "You are a helpful friendly assistant.";
        services.AddSingleton<ChatHistory>(sp =>
            sp.GetRequiredService<IChatCompletion>().CreateNewChat(instructions));
    });

    hostBuilder.Build().Run();
    ```
    </details>

    <details>
    <summary>MyChatFunction.cs</summary>

    ```csharp
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
        public class MyChatFunction
        {
            private readonly ILogger _logger;
            private readonly IKernel _kernel;
            private readonly IChatCompletion _chat;
            private readonly ChatHistory _chatHistory;

            public MyChatFunction(ILoggerFactory loggerFactory, IKernel kernel, ChatHistory chatHistory, IChatCompletion chat)
            {
                _logger = loggerFactory.CreateLogger<MyChatFunction>();
                _kernel = kernel;
                _chat = chat;
                _chatHistory = chatHistory;
            }

            [Function("MyChatFunction")]
            public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
            {
                _logger.LogInformation("C# HTTP trigger function processed a request.");

                //_chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, await req.ReadAsStringAsync() ?? string.Empty);
                string message = await SearchMemoriesAsync(_kernel, await req.ReadAsStringAsync() ?? string.Empty);
                _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, message);

                string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());

                _chatHistory.AddMessage(ChatHistory.AuthorRoles.Assistant, reply);

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

                return result.ToString();
            }
        }
    }

    ```
    </details>
   
    Before running our new code, we'll need to launch and populate the vector database.
    
## Deploy Qdrant VectorDB and Populate Data
In this section we deploy the Qdrant vector database locally and populate it with example data (i.e., Microsoft's 2022 10-K financial report). This will take approximately 15 minutes to import and will use OpenAI’s embedding generation service to create embeddings for the 10-K.

1. Open a terminal and use Docker to pull down the container image for Qdrant.
    ```bash
    docker pull qdrant/qdrant
    ```

1. Change directory to this repo and create a `./data/qdrant` directory for Qdrant to use as persistent storage. 
   Then start the Qdrant container on port `6333` using the `./data/qdrant` folder as the persistent storage location.
    ```bash
    cd /src/semantic-kernel-rag-chat
    mkdir ./data/qdrant
    docker run --name mychat -p 6333:6333 -v "$(pwd)/data/qdrant:/qdrant/storage" qdrant/qdrant
    ```
    > To stop the container, in another terminal window run `docker container stop mychat; docker container rm mychat;`.

1. Open a second terminal, change directory to this repo, and run the `importmemories` tool to populate the vector database with your data.
    > Make sure the `--collection` argument matches the `collectionName` variable in the `SearchMemoriesAsync` method above.
    
    > **Note:** This may take several minutes to several hours depending on the size of your data. This repo contains 
      Microsoft's 2022 10-K financial report data as an example which should normally take about 15 minutes to import.
        
	```bash
    cd /src/semantic-kernel-rag-chat
    cd src/importmemories
    dotnet run -- --memory-store-type qdrant --memory-store-url http://localhost:6333 --collection ms10k --text-file ../../data/ms10k.txt
	```
    > When importing your own data, try to import all files at the same time using multiple `--text-file` arguments. 
    > This example leverages incremental indexes which are best constructed when all data is present. 
    
    > If you want to reset the memory store, delete and recreate the directory in step 2, or create a new directory to use.
        
## Run the function locally
1. With Qdrant running and populated, run your Azure Function locally by opening a terminal, changing directory to your Azure Function project (e.g., `myrepo/src/myfunc`), and starting the function by running
    ```bash
    func start
    ```
    > Make a note of the URL displayed (e.g., `http://localhost:7071/api/MyChatFunction`).

1. Start the test console application
   Open a second terminal and change directory to the `chatconsole` project folder (e.g., `myrepo/src/chatconsole`) and run the application using the Azure Function URL.
   ```bash
   dotnet run http://localhost:7071/api/MyChatFunction
   ```
1. Type a message and press enter to verify that we are able to chat with the AI!
    ```
    Input: Hello, how are you?
    AI: Hello! As an AI language model, I don't have feelings, but I'm functioning properly and ready to 
    assist you. How can I help you today?
    ```
   
 1. Now let's try ask the same question from before about Microsoft's 2022 revenue
    ```
    Input: What was Microsoft's cloud revenue for 2022?
    AI: Microsoft's cloud revenue for 2022 was $91.2 billion.
    ```
    > The AI now has the ability to search through the Microsoft 10-K financial report and find the answer to our question.
    > Let's try another...
    ```
    Input: Did linkedin's revenue grow in 2022?
    AI: Yes, LinkedIn's revenue grew in 2022. It increased by $3.5 billion or 34% driven by a strong job 
    market in the Talent Solutions business and advertising demand in the Marketing Solutions business.
    ```
    

# Chapter 3: Azure Cognitive Search and Retrieval Augmented Generation
[Azure Cognitive Search](https://learn.microsoft.com/en-us/azure/search/search-what-is-azure-search) is a powerful cloud search service that enables developers to build rich search experiences across their own private and heterogenous data sources. With [semantic search](https://learn.microsoft.com/en-us/azure/search/semantic-search-overview#what-is-semantic-search), Azure Cognitive Search can produce more semantically relevant results for text-based queries.

This is an alternative to the vector-based approach we took in chapter 2. With semantic search, we no longer need to generate embeddings like we did in the previous chapter. Instead, a [semantic re-ranking process](https://learn.microsoft.com/en-us/azure/search/semantic-ranking) is applied to the initial set of search results, using the context and meaning of words to elevate the results that are most relevant.

In this chapter, we will modify our chat function to use Azure Cognitive Search with semantic search as the backing memory store. We will once again demonstrate how we can use this memory to generate more meaningful results in our chat application.

## Configure your environment
Before you get started, make sure you have the following additional requirements:
- An [admin key](https://learn.microsoft.com/en-us/azure/search/search-security-api-keys?tabs=portal-use%2Cportal-find%2Cportal-query#find-existing-keys) for your Azure Cognitive Search service, with semantic search enabled.
  - For instructions on setting up an Azure Cognitive Search service instance, click [here](https://learn.microsoft.com/en-us/azure/search/search-create-service-portal).
  - For instructions on enabling semantic search, click [here](https://learn.microsoft.com/en-us/azure/search/semantic-search-overview#enable-semantic-search).

> TODO: Add instructions for setting the `AZURE_COGNITIVE_SEARCH_APIKEY` and `AZURE_COGNITIVE_SEARCH_ENDPOINT` environment variables.

## Update the memory store in our Azure Function
1. Open a terminal window, change to the directory with your project file (e.g., `myrepo/src/myfunc`), 
   and run the `dotnet` commands below to add the Semantic Kernel Azure Cognitive Search connector to your project.
    ```bash
    dotnet add package Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch --prerelease
    ```

1. Open your Program code file (e.g., `Program.cs`) and add the Azure Cognitive Search connector using statement to the top.
    ```csharp
    using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
    ```

    Replace the Qdrant memory store that we added in Chapter #2 with the Azure Cognitive Search memory connector.

    ```csharp
    AzureCognitiveSearchMemory memory = new AzureCognitiveSearchMemory(
        configuration["AZURE_COGNITIVE_SEARCH_ENDPOINT"],
        configuration["AZURE_COGNITIVE_SEARCH_APIKEY"]
    );
    ```
    
    Then, update the builder code where we instantiate the Semantic Kernel. We can remove the OpenAI embedding generation service and the Qdrant memory store from the builder, and replace them with the Azure Cognitive Search memory that we just created.

    ```
    IKernel kernel = new KernelBuilder()
        .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
        .Configure(config => config.AddOpenAIChatCompletionService(
            serviceId: "chat",
            modelId: "gpt-3.5-turbo",
            apiKey: apiKey))
        .WithMemory(memory)
        .Build();
    ```

    > No changes need to be made to `SearchMemoriesAsync`, since it uses the kernel's semantic memory abstraction to generate context for the query. While the underlying memory source has changed, this abstraction has not.

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
    using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;

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

            // Create a memory connector to Azure Cognitive Search that will be used to store memories.
            AzureCognitiveSearchMemory memory = new AzureCognitiveSearchMemory(
               configuration["AZURE_COGNITIVE_SEARCH_ENDPOINT"],
               configuration["AZURE_COGNITIVE_SEARCH_APIKEY"]
            );

            // Create the kernel with chat completion and memory.
            IKernel kernel = new KernelBuilder()
                .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
                .Configure(config => config.AddOpenAIChatCompletionService(
                    serviceId: "chat",
                    modelId: "gpt-3.5-turbo",
                    apiKey: apiKey))
                .WithMemory(memory)
                .Build();

            return kernel;
        });

        // Register the chat completion service.
        services.AddSingleton<IChatCompletion>(sp =>
        sp.GetRequiredService<IKernel>().GetService<IChatCompletion>());

        // Create a new chat history.
        const string instructions = "You are a helpful friendly assistant.";
        services.AddSingleton<ChatHistory>(sp =>
            sp.GetRequiredService<IChatCompletion>().CreateNewChat(instructions));
    });

    hostBuilder.Build().Run();
    ```
    </details>

    <details>
    <summary>MyChatFunction.cs (unchanged)</summary>

    ```csharp
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
        public class MyChatFunction
        {
            private readonly ILogger _logger;
            private readonly IKernel _kernel;
            private readonly IChatCompletion _chat;
            private readonly ChatHistory _chatHistory;

            public MyChatFunction(ILoggerFactory loggerFactory, IKernel kernel, ChatHistory chatHistory, IChatCompletion chat)
            {
                _logger = loggerFactory.CreateLogger<MyChatFunction>();
                _kernel = kernel;
                _chat = chat;
                _chatHistory = chatHistory;
            }

            [Function("MyChatFunction")]
            public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
            {
                _logger.LogInformation("C# HTTP trigger function processed a request.");

                //_chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, await req.ReadAsStringAsync() ?? string.Empty);
                string message = await SearchMemoriesAsync(_kernel, await req.ReadAsStringAsync() ?? string.Empty);
                _chatHistory!.AddMessage(ChatHistory.AuthorRoles.User, message);

                string reply = await _chat.GenerateMessageAsync(_chatHistory, new ChatRequestSettings());

                _chatHistory.AddMessage(ChatHistory.AuthorRoles.Assistant, reply);

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

                return result.ToString();
            }
        }
    }

    ```
    </details>
   
Before running our updated code, we'll need to populate an Azure Cognitive Search index.

## Populate the search index
In this section we create and populate an Azure Cognitive Search index with example data (i.e., Microsoft's 2022 10-K financial report). This will take approximately 5 minutes to import.

1. Open a terminal, change directory to this repo, and run the `importmemories` tool to populate the search index with your data.
    > Make sure the `--collection` argument matches the `collectionName` variable in the `SearchMemoriesAsync` method above.
    
    > **Note:** This may take several minutes to several hours depending on the size of your data. This repo contains Microsoft's 2022 10-K financial report data as an example which should normally take about 5 minutes to import.
        
	```bash
    cd /src/semantic-kernel-rag-chat
    cd src/importmemories
    dotnet run -- --memory-store-type azurecognitivesearch --memory-store-url $AZURE_COGNITIVE_SEARCH_ENDPOINT --collection ms10k --text-file ../../data/ms10k.txt
	```

    > If you want to reset the memory store, you can delete the index from your service via the Azure portal or [the Azure Cognitive Search REST API](https://learn.microsoft.com/en-us/rest/api/searchservice/delete-index). The index name is the same as the `--collection` argument (e.g. `ms10k`).

## Run the function locally
1. With the Azure Cognitive Search service running and populated, run your Azure Function locally by opening a terminal, changing directory to your Azure Function project (e.g., `myrepo/src/myfunc`), and starting the function by running
    ```bash
    func start
    ```
    > Make a note of the URL displayed (e.g., `http://localhost:7071/api/MyChatFunction`).

1. Start the test console application
   Open a second terminal and change directory to the `chatconsole` project folder (e.g., `myrepo/src/chatconsole`) and run the application using the Azure Function URL.
   ```bash
   dotnet run http://localhost:7071/api/MyChatFunction
   ```
1. Type a message and press enter to verify that we are able to chat with the AI!
    ```
    Input: Hello, how are you?
    AI: Hello! As an AI language model, I don't have feelings, but I'm functioning properly and ready to 
    assist you. How can I help you today?
    ```
   
 1. Now let's try ask the same question from before about Microsoft's 2022 revenue
    ```
    Input: What was Microsoft's cloud revenue for 2022?
    AI: Microsoft's cloud revenue for 2022 was $91.2 billion.
    ```
    > The AI now has the ability to search through the Microsoft 10-K financial report and find the answer to our question.
    > Let's try another...
    ```
    Input: Did linkedin's revenue grow in 2022?
    AI: Yes, LinkedIn's revenue grew in 2022. It increased by $3.5 billion or 34% driven by a strong job 
    market in the Talent Solutions business and advertising demand in the Marketing Solutions business.
    ```
    

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




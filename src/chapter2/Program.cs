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
    config.AddUserSecrets<Program>();
});

hostBuilder.ConfigureServices(services =>
{
    services.AddSingleton<IKernel>(sp =>
    {
        IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
        string openAiApiKey = configuration["OPENAI_APIKEY"] ?? "";
        string qdrantEndpoint = configuration["QDRANT_ENDPOINT"] ?? "";

        Uri qdrantUri = new Uri(qdrantEndpoint);

        QdrantMemoryStore memoryStore = new QdrantMemoryStore(
            endpoint: $"{qdrantUri.Scheme}://{qdrantUri.Host}:{qdrantUri.Port}",
            vectorSize: 1536,
            loggerFactory: sp.GetRequiredService<ILoggerFactory>()
        );


        IKernel kernel = new KernelBuilder()
            .WithLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
            .WithOpenAIChatCompletionService(
                "gpt-3.5-turbo",
                openAiApiKey
            )
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", openAiApiKey)
            .WithMemoryStorage(memoryStore)
            .Build();

        return kernel;
    });

    services.AddSingleton<IChatCompletion>(sp =>
    sp.GetRequiredService<IKernel>().GetService<IChatCompletion>());

    const string instructions = "You are a helpful friendly assistant.";
    services.AddSingleton<ChatHistory>(sp =>
        sp.GetRequiredService<IChatCompletion>().CreateNewChat(instructions));
});

hostBuilder.Build().Run();
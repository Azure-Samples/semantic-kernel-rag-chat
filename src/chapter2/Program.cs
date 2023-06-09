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
        string openAiApiKey = configuration["OPENAI_APIKEY"];

        QdrantMemoryStore memoryStore = new QdrantMemoryStore(
           host: "http://localhost",
           port: 6333,
           vectorSize: 1536,
           logger: sp.GetRequiredService<ILogger<QdrantMemoryStore>>());

        IKernel kernel = new KernelBuilder()
            .WithLogger(sp.GetRequiredService<ILogger<IKernel>>())
            .Configure(config => config.AddOpenAIChatCompletionService(
                modelId: "gpt-3.5-turbo",
                apiKey: openAiApiKey))
            .Configure(c => c.AddOpenAITextEmbeddingGenerationService(
                modelId: "text-embedding-ada-002",
                apiKey: openAiApiKey))
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
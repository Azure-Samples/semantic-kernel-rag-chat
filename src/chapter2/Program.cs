using BlingFire;
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
        IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
        string apiKey = configuration["OPENAI_APIKEY"];
        
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
            .Configure(c => c.AddOpenAIEmbeddingGenerationService(
                serviceId: "text-embedding-ada-002",
                modelId: "text-embedding-ada-002",
                apiKey: apiKey))
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

IHost host = hostBuilder.Build();

// This code reads a text file, splits it into sentences, and saves each sentence in a memory collection.
IKernel kernel = host.Services.GetRequiredService<IKernel>();
ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
if ((await kernel.Memory.GetCollectionsAsync()).Any())
{
    logger.LogInformation("Data already in memory store - skipping import.");
}
else
{
    string filePath = "{path to text file}"; // Example: c:/src/repo/data/ms10k.txt
    const string memoryCollectionName = "{collectioName}"; // Example: ms10k

    IEnumerable<string> sentences =
        BlingFireUtils.GetSentences(File.ReadAllText(filePath));

    int i = 0;
    foreach (var sentence in sentences)
    {
        if (i % 100 == 0) logger.LogInformation($"{i}/{sentences.Count()}");
        kernel.Memory.SaveInformationAsync(
            collection: memoryCollectionName,
            text: sentence,
            id: (i++).ToString(),
            description: sentence)
            .GetAwaiter().GetResult();
    }
}

host.Run();
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
                "text-embedding-ada-002",
                "text-embedding-ada-002",
                apiKey))
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
string[] filePaths = new string[] { "ms10k.txt" };
IKernel kernel = host.Services.GetRequiredService<IKernel>();
foreach (string filePath in filePaths)
{
    string text = File.ReadAllText(filePath);
    IEnumerable<string> sentences = BlingFireUtils.GetSentences(text);

    int i = 0;
    foreach (var sentence in sentences)
    {
        kernel.Memory.SaveInformationAsync(
            collection: "Microsoft10K",
            text: sentence,
            id: (i++).ToString(),
            description: sentence)
            .GetAwaiter().GetResult();
    }
}

host.Run();
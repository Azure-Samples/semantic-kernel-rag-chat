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
    config.AddUserSecrets<Program>();
});

hostBuilder.ConfigureServices(services =>
{
    services.AddSingleton<IKernel>(sp =>
    {
        IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
        string openAiApiKey = configuration["OPENAI_APIKEY"] ?? "";
        string azureCognitiveSearchUrl = configuration["AZURE_COGNITIVE_SEARCH_URL"] ?? "";
        string azureCognitiveSearchApiKey = configuration["AZURE_COGNITIVE_SEARCH_APIKEY"] ?? "";

        AzureCognitiveSearchMemoryStore memory = new AzureCognitiveSearchMemoryStore(
            azureCognitiveSearchUrl,
            azureCognitiveSearchApiKey
        );

        IKernel kernel = new KernelBuilder()
            .WithLoggerFactory(sp.GetRequiredService<ILoggerFactory>())
            .WithOpenAIChatCompletionService(
                "gpt-3.5-turbo",
                openAiApiKey)
            .WithMemoryStorage(memory)
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
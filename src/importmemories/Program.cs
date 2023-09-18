using BlingFire;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;

internal class Program
{
    /// <summary>
    /// This program imports text files into a Qdrant VectorDB using Semantic Kernel.
    /// </summary>
    /// <param name="memoryType">Either "qdrant" or "azurecognitivesearch"</param>
    /// <param name="memoryUrl">The URL to a running Qdrant VectorDB (e.g., http://localhost:6333) or to your Azure Cognitive Search endpoint.</param>
    /// <param name="collection">Name of the database collection in which to import (e.g., "mycollection").</param>
    /// <param name="textFile">Text files to import.</param>
    static async Task Main(string memoryType, string memoryUrl, string collection, params FileInfo[] textFile)
    {
        // Validate arguments.
        if (textFile.Length == 0)
        {
            Console.Error.WriteLine("No text files provided. Use '--help' for usage.");
            return;
        }

        IKernel kernel;
        IConfiguration config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .Build();

        if (memoryType.Equals("qdrant", StringComparison.InvariantCultureIgnoreCase))
        {
            // Get the OpenAI API key from the configuration.
            string? openAiApiKey = config["OPENAI_APIKEY"];

            if (string.IsNullOrWhiteSpace(openAiApiKey))
            {
                Console.Error.WriteLine("Please set the 'OPENAI_APIKEY' user secret with your OpenAI API key.");
                return;
            }

            // Create a new memory store that will store the embeddings in Qdrant.
            Uri qdrantUri = new Uri(memoryUrl);
            QdrantMemoryStore memoryStore = new QdrantMemoryStore(
                endpoint: $"{qdrantUri.Scheme}://{qdrantUri.Host}:{qdrantUri.Port}",
                vectorSize: 1536);

            // Create a new kernel with an OpenAI Embedding Generation service.
            kernel = new KernelBuilder()
                .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", openAiApiKey)
                .WithMemoryStorage(memoryStore)
                .Build();

        }
        else if (memoryType.Equals("azurecognitivesearch", StringComparison.InvariantCultureIgnoreCase))
        {
            // Get the Azure Cognitive Search API key from the environment.
            string? azureCognitiveSearchApiKey = config["AZURE_COGNITIVE_SEARCH_APIKEY"];
            string? openAiApiKey = config["OPENAI_APIKEY"];

            if (string.IsNullOrWhiteSpace(azureCognitiveSearchApiKey))
            {
                Console.Error.WriteLine("Please set the 'AZURE_COGNITIVE_SEARCH_APIKEY' user secret with your Azure Cognitive Search API key.");
                return;
            }

            AzureCognitiveSearchMemoryStore memory = new AzureCognitiveSearchMemoryStore(
                memoryUrl,
                azureCognitiveSearchApiKey
            );

            // Create a new kernel with an OpenAI Embedding Generation service.
            kernel = new KernelBuilder()
                .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", openAiApiKey)
                .WithMemoryStorage(memory)
                .Build();
        }
        else
        {
            Console.Error.WriteLine("Not a supported memory type. Use '--help' for usage.");
            return;
        }

        await ImportMemoriesAsync(kernel, collection, textFile);
    }

    static async Task ImportMemoriesAsync(IKernel kernel, string collection, params FileInfo[] textFile)
    {

        // Use sequential memory IDs; this makes it easier to retrieve sentences near a given sentence.
        int memoryId = 0;

        // Import the text files.
        int fileCount = 0;
        foreach (FileInfo fileInfo in textFile)
        {
            Console.WriteLine($"Importing [{++fileCount}/{fileInfo.Length}] {fileInfo.FullName}");

            // Read the text file.
            string text = File.ReadAllText(fileInfo.FullName);

            // Split the text into sentences.
            string[] sentences = BlingFireUtils.GetSentences(text).ToArray();

            // Save each sentence to the memory store.
            int sentenceCount = 0;
            foreach (string sentence in sentences)
            {
                ++sentenceCount;
                if (sentenceCount % 10 == 0)
                {
                    // Log progress every 10 sentences.
                    Console.WriteLine($"[{fileCount}/{fileInfo.Length}] {fileInfo.FullName}: {sentenceCount}/{sentences.Length}");
                }

                await kernel.Memory.SaveInformationAsync(
                    collection: collection,
                    text: sentence,
                    id: memoryId++.ToString(),
                    description: sentence);
            }
        }

        Console.WriteLine("Done!");
    }
}
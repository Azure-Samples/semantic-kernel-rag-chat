using System.Reflection.Metadata.Ecma335;
using BlingFire;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using Microsoft.SemanticKernel.Memory;

internal class Program
{
    /// <summary>
    /// This program imports text files into a Qdrant VectorDB using Semantic Kernel.
    /// </summary>
    /// <param name="qdrantUrl">The URL to a running Qdrant VectorDB (e.g., http://localhost:6333)</param>
    /// <param name="collectionName">Name of the database collection in which to import (e.g., "mycollection").</param>
    /// <param name="textFiles">Text files to import.</param>
    static async Task Main(string qdrantUrl, string collectionName, params FileInfo[] textFiles)
    {
        // Validate arguments.
        if (textFiles.Length == 0)
        {
            Console.Error.WriteLine("No text files provided. Use '--help' for usage.");
            return;
        }

        // Get the OpenAI API key from the environment.
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_APIKEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Please set the 'OPENAI_APIKEY' environment variable with your OpenAI API key.");
            return;
        }

        // Create a new memory store that will store the embeddings in Qdrant.
        Uri qdrantUri = new Uri(qdrantUrl);
        QdrantMemoryStore memoryStore = new QdrantMemoryStore(
            host: $"{qdrantUri.Scheme}://{qdrantUri.Host}",
            port: qdrantUri.Port,
            vectorSize: 1536);

        // Create a new kernel with an OpenAI Embedding Generation service.
        IKernel kernel = new KernelBuilder()
            .Configure(c => c.AddOpenAIEmbeddingGenerationService(
                serviceId: "embedding",
                modelId: "text-embedding-ada-002",
                apiKey: apiKey))
            .WithMemoryStorage(memoryStore)
            .Build();

        
        
        // Use sequential memory IDs; this makes it easier to retrieve sentences near a given sentence.
        int memoryId = 0;

        // Import the text files.
        int fileCount = 0;
        foreach (FileInfo textFile in textFiles)
        {
            Console.WriteLine($"Importing [{++fileCount}/{textFiles.Length}] {textFile.FullName}");
            
            // Read the text file.
            string text = File.ReadAllText(textFile.FullName);

            // Split the text into sentences.
            string[] sentences = BlingFireUtils.GetSentences(text).ToArray();

            // Save each sentence to the memory store.
            int sentenceCount = 0;
            foreach (var sentence in sentences)
            {
                ++sentenceCount;
                if (sentenceCount % 10 == 0)
                {
                    // Log progress every 10 sentences.
                    Console.WriteLine($"[{fileCount}/{textFiles.Length}] {textFile.FullName}: {sentenceCount}/{sentences.Length}");
                }

                await kernel.Memory.SaveInformationAsync(
                    collection: collectionName,
                    text: sentence,
                    id: memoryId++.ToString(),
                    description: sentence);
            }
        }
    }
}
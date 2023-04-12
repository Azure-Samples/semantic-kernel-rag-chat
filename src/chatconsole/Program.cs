using System.Text;

internal class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length < 1 && string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Usage: chatconsole.exe {url}");
            Console.Error.WriteLine("Example: chatconsole.exe http://localhost:7077/api/MySemanticKernelFunction");
            return;
        }
        
        string url = args[0];

        Console.WriteLine("Hello! This is a chat console.");
        Console.WriteLine(url);

        while (true)
        {
            Console.Write("Input: ");
            string? input = Console.ReadLine() ?? string.Empty;

            using HttpClient client = new();

            Task<HttpResponseMessage> response = client.PostAsync(
                requestUri: url,
                content: new StringContent(input, Encoding.UTF8, "text/plain"));

            Console.WriteLine($"AI: {await response.Result.Content.ReadAsStringAsync()}");
        }
    }
}
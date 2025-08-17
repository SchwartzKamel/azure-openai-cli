using Azure;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using OpenAI.Chat;
using dotenv.net;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            // Always load from the baked‑in .env file
            DotEnv.Load(new DotEnvOptions(
                envFilePaths: new[] { ".env" },
                overwriteExistingVars: true,
                trimValues: true
            ));

            if (args.Length == 0)
            {
                Console.Error.WriteLine("[ERROR] No prompt provided. Usage: <prompt>");
                return 1;
            }

            string userPrompt = string.Join(' ', args);

            string? azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZUREOPENAIENDPOINT");
            string? azureOpenAiModel = Environment.GetEnvironmentVariable("AZUREOPENAIMODEL");
            string? azureOpenAiApiKey = Environment.GetEnvironmentVariable("AZUREOPENAIAPI");
            string? systemPrompt = Environment.GetEnvironmentVariable("SYSTEMPROMPT");

            if (string.IsNullOrEmpty(azureOpenAiEndpoint))
                throw new ArgumentNullException(nameof(azureOpenAiEndpoint), "Azure OpenAI endpoint is not set.");
            if (string.IsNullOrEmpty(azureOpenAiApiKey))
                throw new ArgumentNullException(nameof(azureOpenAiApiKey), "Azure OpenAI API key is not set.");

            var endpoint = new Uri(azureOpenAiEndpoint);
            var deploymentName = azureOpenAiModel ?? throw new ArgumentNullException(nameof(azureOpenAiModel), "Azure OpenAI model is not set.");
            var apiKey = azureOpenAiApiKey;

            AzureOpenAIClient azureClient = new(
                endpoint,
                new AzureKeyCredential(apiKey));
            ChatClient chatClient = azureClient.GetChatClient(deploymentName);

            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 10000,
                Temperature = 0.55f,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
            };

            #pragma warning disable AOAI001
            requestOptions.SetNewMaxCompletionTokensPropertyEnabled(true);
            #pragma warning restore AOAI001

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt),
            };

            var response = chatClient.CompleteChatStreaming(messages);

            foreach (StreamingChatCompletionUpdate update in response)
            {
                foreach (ChatMessageContentPart updatePart in update.ContentUpdate)
                {
                    System.Console.Write(updatePart.Text);
                }
            }
            System.Console.WriteLine("");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UNHANDLED ERROR] {ex.GetType().Name}: {ex.Message}");
            return 99;
        }
    }
}

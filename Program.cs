using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using SemanticKernelPlayground.Plugins;

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var embedding = configuration["EmbeddingModel"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey)
    .AddAzureOpenAITextEmbeddingGeneration(embedding, endpoint, apiKey);

builder.Services.AddLogging(configure => configure.AddConsole());
builder.Services.AddLogging(configure => configure.SetMinimumLevel(LogLevel.Information));

var kernel = builder.Build();

var memory = new KernelMemoryBuilder()
    .WithAzureOpenAITextGeneration(new AzureOpenAIConfig()
    {
        APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
        Endpoint = endpoint,
        Deployment = modelName,
        Auth = AzureOpenAIConfig.AuthTypes.APIKey,
        APIKey = apiKey,
    })
    .WithAzureOpenAITextEmbeddingGeneration(new AzureOpenAIConfig()
    {
        APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
        Endpoint = endpoint,
        Deployment = embedding,
        Auth = AzureOpenAIConfig.AuthTypes.APIKey,
        APIKey = apiKey,
    })
    //.WithSimpleVectorDb(new SimpleVectorDbConfig()
    //{
    //    StorageType = FileSystemTypes.Disk
    //})
    //.WithSimpleFileStorage(new SimpleFileStorageConfig()
    //{
    //    StorageType = FileSystemTypes.Disk
    //})
    //.WithCustomTextPartitioningOptions(new TextPartitioningOptions()
    //{
    //    MaxTokensPerParagraph = 100,
    //    OverlappingTokens = 20
    //})
    .Build<MemoryServerless>();


// ingesting data to memory
var fileList = new List<string>()
{
    "SampleData/Elena-Adam-facts.txt",
    "SampleData/Noa-Daniel-facts.txt"
};

foreach (var file in fileList)
{
    await memory.ImportDocumentAsync(file, Path.GetFileName(file));
    Console.WriteLine("Importing file: " + file);
}

//kernel.ImportPluginFromObject(new MemoryPlugin(memory, waitForIngestionToComplete: true), "memory");

var searchPlugin = new SearchPlugin(memory);
kernel.Plugins.AddFromObject(searchPlugin);

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

AzureOpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var history = new ChatHistory();

history.AddSystemMessage("You are a RAG‐enabled assistant. For every query:\n" +
                         "1. Always try to invoke the “SearchPlugin” to retrieve relevant text chunks.\n" +
                         "2. Base your answer on those chunks whenever possible.\n" +
                         "3. Cite each fact with its source in the form (DocumentName, paragraph #).\n" +
                         "Keep answers concise and grounded in the retrieved material.");

do
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Me > ");
    Console.ResetColor();

    var userInput = Console.ReadLine();
    if (userInput == "exit")
    {
        break;
    }

    history.AddUserMessage(userInput!);

    var streamingResponse =
        chatCompletionService.GetStreamingChatMessageContentsAsync(
            history,
            openAiPromptExecutionSettings,
            kernel);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("Agent > ");
    Console.ResetColor();

    var fullResponse = "";
    await foreach (var chunk in streamingResponse)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(chunk.Content);
        Console.ResetColor();
        fullResponse += chunk.Content;
    }
    Console.WriteLine();

    history.AddMessage(AuthorRole.Assistant, fullResponse);


} while (true);
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

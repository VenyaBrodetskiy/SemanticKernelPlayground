using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelPlayground;
using SemanticKernelPlayground.Agents;
using SemanticKernelPlayground.DataIngestion;
using SemanticKernelPlayground.Plugins;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var embedding = configuration["EmbeddingModel"] ?? throw new ApplicationException("ModelName not found");
//var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var builder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelName, apiKey)
    .AddOpenAIEmbeddingGenerator( embedding, apiKey);

builder.Services.AddInMemoryVectorStore();

builder.Services.AddLogging(configure =>
{
    configure.AddConsole();
    configure.SetMinimumLevel(LogLevel.Information);
});

var kernelWithSearchTools = builder.Build();

var builder2 = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelName, apiKey);

var kernelWithoutTools = builder2.Build();

// ingesting data to memory
var fileList = new List<string>()
{
    "SampleData/ForensicReport.txt",
    "SampleData/SecurityFootageReport.txt",
    "SampleData/Statement_MrsGreen.txt",
    "SampleData/Statement_WitnessB.txt"
};

var vectorStore = kernelWithSearchTools.GetRequiredService<InMemoryVectorStore>();
var embeddingGenerationService = kernelWithSearchTools.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
foreach (var file in fileList)
{
    var textChunks = DocumentReader.ParseFile(file);
    var dataUploader = new DataUploader(vectorStore, embeddingGenerationService);
    await dataUploader.UploadToVectorStore("investigationCase", textChunks);
}

var searchPlugin = new SearchPlugin(vectorStore, embeddingGenerationService);
kernelWithSearchTools.Plugins.AddFromObject(searchPlugin);

var searchInDataAgent = SearchInDataAgent.Create(kernelWithSearchTools);
var contradictionAnalysisAgent = ContradictionAnalysisAgent.Create(kernelWithoutTools);
var investigatorAgent = InvestigatorAgent.Create(kernelWithoutTools);

ChatHistory history = [];
ValueTask responseCallback(ChatMessageContent response)
{
    history.Add(response);

    if (response.Content is null)
    {
        if (response.InnerContent is OpenAI.Chat.ChatCompletion chatCompletion)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(
                $"Calling function: {chatCompletion.ToolCalls[0].FunctionName} with argument {chatCompletion.ToolCalls[0].FunctionArguments}");
            Console.ResetColor();

        }
        return ValueTask.CompletedTask;
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write($"{response.AuthorName}> ");
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"{response.Content}");

    //Console.ForegroundColor = ConsoleColor.Cyan;
    //Console.Write("Me > ");
    //Console.ResetColor();
    return ValueTask.CompletedTask;
}

//var userInput = Console.ReadLine();
//var userInput = "Who is the biggest liar in this case";
var userInput = "Find some internal contradictions in statements of Mrs Green";

var orchestration = new GroupChatOrchestration(
    new AiGroupChatManager(userInput,
        kernelWithoutTools.GetRequiredService<IChatCompletionService>())
    {
        MaximumInvocationCount = 10,
    },
    investigatorAgent,
    searchInDataAgent,
    contradictionAnalysisAgent
)
{
    ResponseCallback = responseCallback,
    LoggerFactory = kernelWithSearchTools.GetRequiredService<ILoggerFactory>()
};

var runtime = new InProcessRuntime();
await runtime.StartAsync();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.Write("Me > ");
Console.ResetColor();


var userChatMessage = new ChatMessageContent(AuthorRole.User, userInput);

var agentResponses = await orchestration.InvokeAsync(userInput, runtime);
string text = await agentResponses.GetValueAsync();
Console.WriteLine($"\n# RESULT: {text}");
await runtime.RunUntilIdleAsync();

Console.WriteLine($"\n# FINISHED #################");


#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.



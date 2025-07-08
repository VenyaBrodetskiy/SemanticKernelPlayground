using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Orchestration.Handoff;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.InMemory;
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
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey)
    .AddAzureOpenAIEmbeddingGenerator(embedding, endpoint, apiKey);

builder.Services.AddInMemoryVectorStore();

builder.Services.AddLogging(configure => configure.AddConsole());
builder.Services.AddLogging(configure => configure.SetMinimumLevel(LogLevel.Information));

var kernel = builder.Build();

//var credentials = new AzureCliCredential();
//// azure ai agent
//var azureAgentsClient =
//    AzureAIAgent.CreateAgentsClient("https://venyab-0723-resource.services.ai.azure.com/api/projects/venyab-0723", credentials);
//var azureAgent = await azureAgentsClient.Administration.GetAgentAsync("asst_hksaCOBws7ppSMrU5dUKZbYu");
//var agent = new AzureAIAgent(azureAgent, azureAgentsClient);

var builder2 = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);

var kernel2 = builder2.Build();

var investigatorAgent = new ChatCompletionAgent()
{
    Name = "InvestigatorAgent",
    Description = "An agent that manages other agents in order to provide investigation about the case",
    Kernel = kernel2,
    Arguments = new(
        new AzureOpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        }),
    Instructions = "You are a investigator agent. You task is to manage, orchestrate other agents in order to provide deep investigation about the case"
};


// ingesting data to memory
var fileList = new List<string>()
{
    "SampleData/Elena-Adam-facts.txt",
    "SampleData/Noa-Daniel-facts.txt"
};

var vectorStore = kernel.GetRequiredService<InMemoryVectorStore>();
var embeddingGenerationService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
foreach (var file in fileList)
{
    var textChunks = DocumentReader.ParseFile(file);
    var dataUploader = new DataUploader(vectorStore, embeddingGenerationService);
    await dataUploader.UploadToVectorStore("loveStory", textChunks);
}

var searchPlugin = new SearchPlugin(vectorStore, embeddingGenerationService);
kernel.Plugins.AddFromObject(searchPlugin);

var searchInDataAgent = new ChatCompletionAgent()
{
    Name = "SearchInDataAgent",
    Description = "An agent that searches for data in vector store and returns relevant information.",
    Kernel = kernel,
    Arguments = new(
        new AzureOpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        }),
    Instructions = "You are a RAG‐enabled assistant. For every query:\n" +
                   "1. Always try to invoke the “SearchPlugin” to retrieve relevant text chunks.\n" +
                   "2. Base your answer on those chunks whenever possible.\n" +
                   "3. Cite each fact with its source in the form (DocumentName, paragraph #).\n" +
                   "Keep answers concise and grounded in the retrieved material."
};

var thread = new ChatHistoryAgentThread();
//var thread = new AzureAIAgentThread(agent.Client);

var handoffs = OrchestrationHandoffs
    .StartWith(investigatorAgent)
    .Add(investigatorAgent, searchInDataAgent, "Ask this agent if you need someone to search in files/memory for you");

ChatHistory history = [];
ValueTask responseCallback(ChatMessageContent response)
{
    history.Add(response);

    if (response.Content is null) 
        return ValueTask.CompletedTask;

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("Agent > ");
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"{response.AuthorName}: {response.Content}");

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Me > ");
    Console.ResetColor();
    return ValueTask.CompletedTask;
}

var orchestration = new HandoffOrchestration(
    handoffs, investigatorAgent, searchInDataAgent)
{
    ResponseCallback = responseCallback,
};

var runtime = new InProcessRuntime();
await runtime.StartAsync();

Console.ForegroundColor = ConsoleColor.Cyan;
Console.Write("Me > ");
Console.ResetColor();
do
{

    var userInput = Console.ReadLine();
    if (userInput == "exit")
    {
        break;
    }

    var userChatMessage = new ChatMessageContent(AuthorRole.User, userInput);

    var agentResponses = await orchestration.InvokeAsync(userInput, runtime);

    

} while (true);
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

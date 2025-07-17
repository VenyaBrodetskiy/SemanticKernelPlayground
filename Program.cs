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
using SemanticKernelPlayground.DataIngestion;
using SemanticKernelPlayground.Plugins;
using System.Text.Json;

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

var searchInDataAgent = new ChatCompletionAgent()
{
    Name = "SearchInDataAgent",
    Description = "An agent that searches for facts, statements and returns relevant information.",
    Kernel = kernelWithSearchTools,
    Arguments = new(
        new OpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.0f,
        }),
    Instructions = "You are an agent that searches for data in vector store and returns relevant information." +
                   "Your task is to listen to InvestigatorAgent directions and follow it as a good boy" +
                   "You will receive some question/query/context. Your task is to find all relevant information from vector store about this question and filter out irrelevant. You do not provide any additional thoughts on the topic, you do not answer user question - ONLY DATA" +
                   "For every query:\n" +
                   "1. Always try to invoke the \"SearchPlugin\" to retrieve relevant text chunks.\n" +
                   "2. Check content of chunks and decide if information is relevant to your task or not\n" +
                   "3. Based on data relevancy which you got your might come up to decision to adjust query and continue searching or stop searching.\n" +
                   "4. Return back only relevant information, cite each fact with its source in the form (DocumentName, paragraph #).\n" +
                   "You must try different queries to be sure that you collected all relevant data. For example, if you asked about contradictions, you should provide all significant statements of this person, not contradictions itself, there is another agent for this task! ONLY PROVIDE FACTS OR STATEMENTS"
};

var contradictionAnalysisAgent = new ChatCompletionAgent()
{
    Name = "AnalysisAgent",    
    Description = "An agent that analyzes given data for contradictions, inconsistencies, and conflicting statements. Can't search for data",
    Kernel = kernelWithoutTools,
    Arguments = new(
        new OpenAIPromptExecutionSettings() 
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        }),
    Instructions = "You are an analysis specialist. Your task is to:\n" +
                   "1. Carefully examine provided data for any contradictions, inconsistencies, or conflicting statements.\n" +
                   "2. Compare different sources and identify discrepancies in facts, timelines, or accounts.\n" +
                   "3. Look for logical inconsistencies within individual statements or across multiple sources.\n" +
                   "4. Highlight any suspicious patterns or elements that don't align.\n" +
                   "5. If you have suspicions, that you didn't get full data, anyway give conclusion, but also mention what is missing, so that other agents could provide it to you.\n" +
                   "6. Present your findings clearly, citing specific sources and explaining the nature of each contradiction.\n" +
                   "Always be thorough and objective in your analysis. If you need more data to verify potential contradictions, ask for it."
};

var investigatorAgent = new ChatCompletionAgent()
{
    Name = "InvestigatorAgent",
    Description = "An agent that manages other agents in order to provide investigation about the case",
    Kernel = kernelWithoutTools,
    Arguments = new(
        new OpenAIPromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            ResponseFormat = typeof(GroupChatManagerResult<string>)
        }),
    Instructions = $"""
                    You are an investigator agent. Your task is to manage and orchestrate other agents to provide deep investigation about the case. 
                    You have access to two specialized agents:
                    1. SearchInDataAgent - Use this agent to search for specific information in the case files and evidence, like statements, facts.
                    2. AnalysisAgent - Use this agent to analyze data, but he needs data to be provided first
                    Both agents can return to you with their findings for you to summarize and present comprehensive results.
                    Coordinate their efforts to build a complete picture of the case.

                    You task is to create plan of investigation, explaining which agent should be called first, which should be called next. Also explain it to other agents, explain why you selected them and what is their task. Put this information in Reason field.
                    In value field you should put the name of the participant you would like to select next. If you think that investigation is finished, then return InvestigatorAgent with Reason = "Investigation is finished, I will summarize results now" and then call FilterResults method to summarize results.
                    """,
};

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
    new AIGroupChatManager(userInput,
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


public sealed class AIGroupChatManager(string topic, IChatCompletionService chatCompletion) : GroupChatManager
{
    private static class Prompts
    {
        public static string Termination(string topic) =>
            $"""
                You are a lead investigator determining if the current investigation on '{topic}' is complete.
                Evaluate whether all relevant information has been gathered, contradictions analyzed, and a conclusion reached.
                If the investigation has reached a comprehensive conclusion with sufficient evidence and analysis, respond with True.
                If there are still unanswered questions, unexplored angles, or insufficient analysis, respond with False.
                Respond ONLY with True or False
                """;

        public static string Selection(string topic, string participants) =>
            $"""
                You are an investigator agent. Your task is to manage and orchestrate other agents to provide deep investigation about the case. " +
                "You have access to two specialized agents:\n" +
                "1. SearchInDataAgent - Use this agent to search for specific information in the case files and evidence.\n" +
                "2. AnalysisAgent - Use this agent to analyze data for contradictions, inconsistencies, and conflicting statements.\n" +
                "Both agents can return to you with their findings for you to summarize and present comprehensive results. " +
                "Coordinate their efforts to build a complete picture of the case.
                
                Please respond with only the name of the participant you would like to select.
                In Reason explain why you selected this participant and what task you want them to perform.
                """;

        public static string Filter(string topic) =>
            $"""
                You are a lead investigator finalizing the investigation on '{topic}'.
                Synthesize the key findings from the investigation, including:
                
                1. Summary of critical evidence discovered
                2. Major contradictions or inconsistencies identified
                3. Conclusions that can be drawn from the evidence
                4. Any remaining uncertainties or limitations of the investigation
                
                Present a comprehensive but concise final report on the investigation's findings.
                """;
    }

    /// <inheritdoc/>
    public override ValueTask<GroupChatManagerResult<string>> FilterResults(ChatHistory history,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Filtering results]> ");
        Console.ResetColor();

        var result = GetResponseAsync<string>(history, Prompts.Filter(topic), cancellationToken);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Result.Value}, reason: {result.Result.Reason}");
        Console.ResetColor();
        return result;
    }

    /// <inheritdoc/>
    public override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(ChatHistory history, GroupChatTeam team,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Selecting next agent]> ");
        Console.ResetColor();

        

        var lastMessage = history.LastOrDefault();
        if (lastMessage?.AuthorName == "InvestigatorAgent")
        {
            var managerDecision = JsonSerializer.Deserialize<GroupChatManagerResult<string>>(lastMessage.Content);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Result: {managerDecision.Value}, reason: {managerDecision.Reason}");
            Console.ResetColor();
            return ValueTask.FromResult(managerDecision);
        }
        if (string.IsNullOrWhiteSpace(lastMessage.Content))
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("Error: Last message content is null, cannot determine next agent. Doing fallback");
            Console.ResetColor();
            var result2 = GetResponseAsync<string>(history, Prompts.Selection(topic, team.FormatList()), cancellationToken);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Result: {result2.Result.Value}, reason: {result2.Result.Reason}");
            Console.ResetColor();
            //int randIndex = new Random().Next(0, team.Count - 1);
            //var next = team.Skip(randIndex).First().Key;
            //GroupChatManagerResult<string> result2 = new(next) { Reason = "Some good reason " };

            return result2;
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: InvestigatorAgent, reason: Auto route to Investigator");
        Console.ResetColor();

        GroupChatManagerResult<string> result = new("InvestigatorAgent") { Reason = "Some good reason " };
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInput(ChatHistory history, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Should request user input?]> ");
        Console.ResetColor();
        return ValueTask.FromResult(new GroupChatManagerResult<bool>(false) { Reason = "The AI group chat manager does not request user input." });
    }

    /// <inheritdoc/>
    public override async ValueTask<GroupChatManagerResult<bool>> ShouldTerminate(ChatHistory history, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Should terminate?]> ");
        Console.ResetColor();

        GroupChatManagerResult<bool> result = await base.ShouldTerminate(history, cancellationToken);
        if (!result.Value)
        {
            result = await this.GetResponseAsync<bool>(history, Prompts.Termination(topic), cancellationToken);
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Value}, reason: {result.Reason}");
        Console.ResetColor();
        return result;
    }

    private async ValueTask<GroupChatManagerResult<TValue>> GetResponseAsync<TValue>(ChatHistory history, string prompt, CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings executionSettings = new() { ResponseFormat = typeof(GroupChatManagerResult<TValue>) };
        ChatHistory request = [.. history, new ChatMessageContent(AuthorRole.System, prompt)];
        ChatMessageContent response = await chatCompletion.GetChatMessageContentAsync(request, executionSettings, kernel: null, cancellationToken);
        string responseText = response.ToString();
        return
            JsonSerializer.Deserialize<GroupChatManagerResult<TValue>>(responseText) ??
            throw new InvalidOperationException($"Failed to parse response: {responseText}");
    }
}
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.



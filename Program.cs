using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using SemanticKernelPlayground.Plugins;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var repositoryPathRef = new RepositoryPathHolder();

var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);


// Add GitPlugin
builder.Plugins.Add(new GitPlugin(() => repositoryPathRef.Path).CreatePlugin());

// Add ReleaseNotes prompt plugin
var promptPath = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", "PromptPlugins");
builder.Plugins.AddFromPromptDirectory(promptPath);

// Add "setrepo" command
builder.Plugins.Add(KernelPluginFactory.CreateFromFunctions("Custom", new[]
{
    KernelFunctionFactory.CreateFromMethod((string path) =>
    {
        repositoryPathRef.Path = path;
        return $"Repository path set to:\n{path}";
    }, "SetRepositoryPath", "Sets Git repo path for GitPlugin")
}));

var kernel = builder.Build();

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

AzureOpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var history = new ChatHistory();
var systemPrompt = """
You are a helpful assistant with Git and prompt capabilities.
You can fetch recent commits and generate release notes.
Use GitPlugin functions and ReleaseNotes prompt when requested.
""";
history.AddSystemMessage(systemPrompt);

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

public class RepositoryPathHolder
{
    public string? Path { get; set; }
}
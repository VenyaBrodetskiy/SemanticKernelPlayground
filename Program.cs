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

var textMemoryPlugin = new TextMemoryPlugin();

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

// Register in-memory TextMemoryPlugin under the name "TextMemory"
builder.Plugins.Add(KernelPluginFactory.CreateFromObject(textMemoryPlugin, "TextMemory"));

var kernel = builder.Build();

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

AzureOpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var history = new ChatHistory();
var systemPrompt = """
You are a helpful assistant with Git, release-notes, and codebase search.
Use GitPlugin for commits, ReleaseNotes prompt when asked,
and the TextMemoryPlugin for code searches (via `docsearch`).
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

    // 1) Manual setrepo handler: set path & index code
    if (userInput.StartsWith("setrepo ", StringComparison.OrdinalIgnoreCase))
    {
        var path = userInput["setrepo ".Length..].Trim();
        repositoryPathRef.Path = path;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Repository path set to:\n{path}");
        Console.ResetColor();

        Console.WriteLine("Indexing .cs files into memory...");
        await CodeIndexer.IndexCodeAsync(path, textMemoryPlugin);
        Console.WriteLine("Indexing complete.");
        continue;
    }

    // 2) docsearch handler: search in-memory store
    if (userInput.StartsWith("docsearch ", StringComparison.OrdinalIgnoreCase))
    {
        var query = userInput["docsearch ".Length..];
        var result = await textMemoryPlugin.SearchAsync("codebase", query);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Agent >");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(result);
        Console.ResetColor();
        continue;
    }

    history.AddUserMessage(userInput);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("Agent > ");
    Console.ResetColor();

        var streamingResponse =
        chatCompletionService.GetStreamingChatMessageContentsAsync(
            history,
            openAiPromptExecutionSettings,
            kernel);

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
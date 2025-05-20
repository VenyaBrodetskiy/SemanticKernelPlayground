using System.ComponentModel;
using System.Text;
using System.Text.Json;
using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace SemanticKernelPlayground.Plugins;

public class GitPlugin
{
    private string? _repoPath;
    private readonly Kernel _kernel;

    public GitPlugin()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
            .Build();

        var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
        var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
        var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

        var builder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);

        builder.Services.AddLogging(configure => configure.AddConsole());
        builder.Services.AddLogging(configure => configure.SetMinimumLevel(LogLevel.Information));

        var promptPlugins = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", "SeparatePrompts") ?? throw new ApplicationException("PromptPlugins are missing");

        builder.Plugins.AddFromPromptDirectory(promptPlugins);
        _kernel = builder.Build();
    }

    [KernelFunction]
    [Description("Set the repository path for git operations")]
    public string SetRepository(
        [Description("Absolute path to the git repository.")]
        string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath) ||
            !Directory.Exists(repoPath) ||
            !Repository.IsValid(repoPath))
        {
            return $"❌ '{repoPath}' is not a valid Git repository. Please try again.";
        }

        _repoPath = repoPath;
        return $"✅ Repository set to '{repoPath}'.";
    }

    [KernelFunction]
    [Description("Get the latest commits from the currently set git repository")]
    public string GetCommits(
        [Description("Number of commits to retrieve.")]
        int nOfCommits)
    {
        if (string.IsNullOrEmpty(_repoPath))
        {
            return "⚠️ No repository defined. Please run **SetRepository** first.";
        }

        using var repo = new Repository(_repoPath);
        var commits = repo.Commits.Take(nOfCommits);

        var sb = new StringBuilder();
        var repoName = Path.GetFileName(repo.Info.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar));
        sb.AppendLine($"### Last {nOfCommits} commits in `{repoName}`");
        foreach (var commit in commits)
        {
            sb.AppendLine(
                $"- `{commit.Id.Sha[..8]}` • {commit.Author.Name} on {commit.Author.When:yyyy-MM-dd}  \n  {commit.Message}"
            );
        }

        return sb.ToString();
    }

    [KernelFunction]
    [Description("Analyze commit patterns and provide insights")]
    public async Task<string> AnalyzeCommitPatterns(
        [Description("Number of commits to analyze")]
        int commitCount)
    {
        if (string.IsNullOrEmpty(_repoPath))
        {
            return "⚠️ No repository defined. Please run **SetRepository** first.";
        }

        using var repo = new Repository(_repoPath);
        var commits = repo.Commits.Take(commitCount).ToList();

        var commitData = new StringBuilder();
        foreach (var commit in commits)
        {
            commitData.AppendLine($"Hash: {commit.Id.Sha[..8]}");
            commitData.AppendLine($"Author: {commit.Author.Name}");
            commitData.AppendLine($"Date: {commit.Author.When:yyyy-MM-dd HH:mm:ss}");
            commitData.AppendLine($"Message: {commit.Message.Trim()}");
            commitData.AppendLine($"Files changed: {commit.Tree.Count}");
            commitData.AppendLine();
        }

        // ######### SIMPLE PROMPT #########
        var resultSimple = await _kernel.InvokeAsync("SeparatePrompts", "PatternsAnalyzerSimple", new ()
        {
            ["commits"] = commitData.ToString()
        });

        Console.WriteLine($"\n###############################\n" +
                          $"Simple analysis results: \n{resultSimple}");

        // ######### CoT structured PROMPT #########
        var args = new KernelArguments
        {
            ["commits"] = commitData.ToString(),
            ExecutionSettings = new Dictionary<string, PromptExecutionSettings>
            {
                {
                    "default",
                    new AzureOpenAIPromptExecutionSettings
                    {
                        ResponseFormat = typeof(GitHistoryAnalysisResult)
                    }
                }
            }
        };
        var resultCoT = await _kernel.InvokeAsync("SeparatePrompts", "PatternsAnalyzerCoT", args);

        var result = JsonSerializer.Deserialize<GitHistoryAnalysisResult>(resultCoT.ToString() ?? string.Empty) ?? new GitHistoryAnalysisResult();
        Console.WriteLine($"\n###############################\n" +
                          $"Chain of thought analysis results: \n" +
                          $"Reasoning: {result.Reasoning}\n" +
                          $"Answer: {result.Answer}");
        return result.ToString();
    }
}

public record GitHistoryAnalysisResult
{
    public string Reasoning { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

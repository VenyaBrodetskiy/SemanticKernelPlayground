using System.ComponentModel;
using System.Text;
using LibGit2Sharp;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace SemanticKernelPlayground.Plugins;

public class GitPlugin(Kernel kernel)
{
    private string? _repoPath;

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

        var args = new KernelArguments
        {
            ["commits"] = commitData.ToString()
        };

        args.ExecutionSettings = new Dictionary<string, PromptExecutionSettings>
        {
            {
                "default",
                new AzureOpenAIPromptExecutionSettings
                {
                    ResponseFormat = typeof(PatterAnalysisResult)
                }
            }
        };

        var result = await kernel.InvokeAsync("PromptPlugins", "PatternsAnalyzer", args);
        return result.ToString();
    }
}

public record PatterAnalysisResult
{
    public string Reasoning { get; set; } = string.Empty;
    public string Patterns { get; set; } = string.Empty;
}

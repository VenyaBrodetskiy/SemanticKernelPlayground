using System.ComponentModel;
using System.Text;
using LibGit2Sharp;
using Microsoft.SemanticKernel;

namespace SemanticKernelPlayground.Plugins;

public class GitPlugin
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
}

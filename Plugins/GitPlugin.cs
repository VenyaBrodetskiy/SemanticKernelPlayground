using LibGit2Sharp;
using Microsoft.SemanticKernel;

namespace SemanticKernelPlayground.Plugins;

public class GitPlugin
{
    private readonly Func<string?> _getRepoPath;

    public GitPlugin(Func<string?> getRepoPath)
    {
        _getRepoPath = getRepoPath;
    }

    public KernelPlugin CreatePlugin()
    {
        return KernelPluginFactory.CreateFromFunctions("GitPlugin", new[]
        {
            KernelFunctionFactory.CreateFromMethod((string path) =>
            {
                if (!Directory.Exists(path) || !Repository.IsValid(path))
                    return $"Invalid or non-Git directory: {path}";
                return $"Repository path confirmed as: {path}";
            }, "SetRepositoryPath", "Sets the Git repository path."),

            KernelFunctionFactory.CreateFromMethod((string numberOfCommits) =>
            {
                var repoPath = _getRepoPath();
                if (string.IsNullOrEmpty(repoPath))
                    return "Repository path not set. Use 'setrepo <path>'.";

                if (!Repository.IsValid(repoPath))
                    return $"Invalid Git repository: {repoPath}";

                if (!int.TryParse(numberOfCommits, out var count))
                    return "Invalid number format.";

                using var repo = new Repository(repoPath);
                return string.Join("\n", repo.Commits
                    .Take(count)
                    .Select(c => $"- {c.MessageShort} (by {c.Author.Name} on {c.Author.When.LocalDateTime})"));
            }, "GetLatestCommits", "Returns latest N commits.")
        });
    }
}

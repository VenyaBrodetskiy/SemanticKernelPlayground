namespace SemanticKernelPlayground.Plugins;

public static class CodeIndexer
{
    // Now takes your TextMemoryPlugin directly
    public static async Task IndexCodeAsync(string repoPath, TextMemoryPlugin memoryPlugin)
    {
        if (string.IsNullOrEmpty(repoPath) || !Directory.Exists(repoPath))
        {
            Console.WriteLine($"Repository path not found: {repoPath}");
            return;
        }

        Console.WriteLine($"Indexing .cs files in: {repoPath}");
        var files = Directory.GetFiles(repoPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i += 10)
            {
                var chunk = string.Join('\n', lines.Skip(i).Take(10));
                var id = $"{Path.GetFileName(file)}-chunk-{i / 10}";

                await memoryPlugin.SaveAsync("codebase", id, chunk);
            }
        }

        Console.WriteLine($"Indexed {files.Length} files into memory.");
    }
}

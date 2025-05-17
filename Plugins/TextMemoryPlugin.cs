using Microsoft.SemanticKernel;

namespace SemanticKernelPlayground.Plugins;

public class TextMemoryPlugin
{
    // In-process store: collection → (key → text)
    private readonly Dictionary<string, Dictionary<string, string>> _store
        = new(StringComparer.OrdinalIgnoreCase);

    [KernelFunction]
    public Task<string> SaveAsync(string collection, string key, string text)
    {
        if (!_store.TryGetValue(collection, out var col))
        {
            col = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _store[collection] = col;
        }
        col[key] = text;
        return Task.FromResult($"Saved chunk '{key}' to collection '{collection}'.");
    }

    [KernelFunction]
    public Task<string> RetrieveAsync(string collection, string key)
    {
        if (_store.TryGetValue(collection, out var col) &&
            col.TryGetValue(key, out var txt))
        {
            return Task.FromResult(txt);
        }
        return Task.FromResult($"No entry for key '{key}' in '{collection}'.");
    }

    [KernelFunction]
    public Task<string> SearchAsync(string collection, string query)
    {
        if (!_store.TryGetValue(collection, out var col))
        {
            return Task.FromResult("No matches found.");
        }

        var matches = col.Values
            .Where(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(t => $"- {t}");

        var result = string.Join('\n', matches);
        return Task.FromResult(string.IsNullOrEmpty(result)
            ? "No matches found."
            : result);
    }
}

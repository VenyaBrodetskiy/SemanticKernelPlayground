using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.KernelMemory;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace SemanticKernelPlayground.Plugins;

public class SearchPlugin(IKernelMemory memory)
{
    [KernelFunction]
    [Description("Search for data in vector store")]
    public async Task<MemoryAnswer> SearchInLoveStoryCollection(
        [Description("The search query")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5)
    {
        var result = await memory.AskAsync(query);

        return result;
    }
}

#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
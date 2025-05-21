using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using SemanticKernelPlayground.Models;
using System.ComponentModel;
using System.Text;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace SemanticKernelPlayground.Plugins;

public class SearchPlugin(IVectorStore vectorStore,
    ITextEmbeddingGenerationService embeddingService)
{
    [KernelFunction]
    [Description("Search for data in vector store")]
    public async Task<string> SearchInLoveStoryCollection(
        [Description("The search query")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5)
    {
        var collection = vectorStore.GetCollection<string, TextChunk>("loveStory");
        
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(query);

        var searchResults = collection.SearchEmbeddingAsync(queryEmbedding, maxResults);

        var resultList = new List<VectorSearchResult<TextChunk>>();
        await foreach (var result in searchResults)
        {
            resultList.Add(result);
        }

        if (!resultList.Any())
        {
            return "No relevant information found for your query in vector store";
        }

        var builder = new StringBuilder();
        builder.AppendLine("### Relevant information about your query:");
        builder.AppendLine();

        foreach (var result in resultList)
        {
            builder.AppendLine($"**File: {result.Record.DocumentName}, Paragraph: {result.Record.ParagraphId}, Relevancy: {result.Score}**");

            builder.AppendLine(result.Record.Text);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}

#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
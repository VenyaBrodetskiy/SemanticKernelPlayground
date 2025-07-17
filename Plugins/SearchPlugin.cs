using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using SemanticKernelPlayground.Models;
using System.ComponentModel;
using System.Text;

namespace SemanticKernelPlayground.Plugins;

public class SearchPlugin(VectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingService)
{
    [KernelFunction]
    [Description("Search for data in vector store")]
    public async Task<string> SearchInInvestigationCaseCollection(
        [Description("The search query")] string query,
        [Description("Maximum number of results to return")] int maxResults)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Search query: {query}, Max results: {maxResults}");
        Console.ResetColor();
        var collection = vectorStore.GetCollection<string, TextChunk>("investigationCase");
        
        var queryEmbedding = await embeddingService.GenerateAsync(query);

        var searchResults = collection.SearchAsync(queryEmbedding, maxResults);

        var resultList = new List<VectorSearchResult<TextChunk>>();
        await foreach (var result in searchResults)
        {
            resultList.Add(result);
        }

        if (resultList.Count == 0)
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
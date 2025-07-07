using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using SemanticKernelPlayground.Models;

namespace SemanticKernelPlayground.DataIngestion;

public class DataUploader(VectorStore vectorStore, IEmbeddingGenerator<string, Embedding<float>> textEmbeddingGenerator)
{
    public async Task UploadToVectorStore(string collectionName, IEnumerable<TextChunk> textChunk)
    {
        var collection = vectorStore.GetCollection<string, TextChunk>(collectionName);
        await collection.EnsureCollectionExistsAsync();

        foreach (var chunk in textChunk)
        {
            Console.WriteLine($"Generating embedding for paragraph: {chunk.ParagraphId}");
            chunk.TextEmbedding = await textEmbeddingGenerator.GenerateAsync(chunk.Text);

            Console.WriteLine($"Upserting chunk to vector store: {chunk.Key}");
            await collection.UpsertAsync(chunk);
        }
    }
}

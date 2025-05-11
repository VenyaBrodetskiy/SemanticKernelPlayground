using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;
using SemanticKernelPlayground.Models;

namespace SemanticKernelPlayground.DataIngestion;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class DataUploader(IVectorStore vectorStore, ITextEmbeddingGenerationService textEmbeddingGenerator)
{
    public async Task UploadToVectorStore(string collectionName, IEnumerable<TextChunk> textChunk)
    {
        var collection = vectorStore.GetCollection<string, TextChunk>(collectionName);
        await collection.CreateCollectionIfNotExistsAsync();

        foreach (var chunk in textChunk)
        {
            Console.WriteLine($"Generating embedding for paragraph: {chunk.ParagraphId}");
            chunk.TextEmbedding = await textEmbeddingGenerator.GenerateEmbeddingAsync(chunk.Text);

            Console.WriteLine($"Upserting chink to vector store: {chunk.Key}");
            await collection.UpsertAsync(chunk);
        }
    }
}

#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
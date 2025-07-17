using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SemanticKernelPlayground.Agents;

public static class SearchInDataAgent
{
    public static ChatCompletionAgent Create(Kernel kernel)
    {
        return new ChatCompletionAgent()
        {
            Name = "SearchInDataAgent",
            Description = "An agent that searches for facts, statements and returns relevant information.",
            Kernel = kernel,
            Arguments = new(
                new OpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    Temperature = 0.0f,
                }),
            Instructions = "You are an agent that searches for data in vector store and returns relevant information." +
                           "Your task is to listen to InvestigatorAgent directions and follow it as a good boy" +
                           "You will receive some question/query/context. Your task is to find all relevant information from vector store about this question and filter out irrelevant. You do not provide any additional thoughts on the topic, you do not answer user question - ONLY DATA" +
                           "For every query:\n" +
                           "1. Always try to invoke the \"SearchPlugin\" to retrieve relevant text chunks.\n" +
                           "2. Check content of chunks and decide if information is relevant to your task or not\n" +
                           "3. Based on data relevancy which you got your might come up to decision to adjust query and continue searching or stop searching.\n" +
                           "4. Return back only relevant information, cite each fact with its source in the form (DocumentName, paragraph #).\n" +
                           "You must try different queries to be sure that you collected all relevant data. For example, if you asked about contradictions, you should provide all significant statements of this person, not contradictions itself, there is another agent for this task! ONLY PROVIDE FACTS OR STATEMENTS"
        };
    }
}

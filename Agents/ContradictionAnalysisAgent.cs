using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SemanticKernelPlayground.Agents;

public static class ContradictionAnalysisAgent
{
    public static ChatCompletionAgent Create(Kernel kernel)
    {
        return new ChatCompletionAgent()
        {
            Name = "AnalysisAgent",    
            Description = "An agent that analyzes given data for contradictions, inconsistencies, and conflicting statements. Can't search for data",
            Kernel = kernel,
            Arguments = new(
                new OpenAIPromptExecutionSettings() 
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                }),
            Instructions = "You are an analysis specialist. Your task is to:\n" +
                           "1. Carefully examine provided data for any contradictions, inconsistencies, or conflicting statements.\n" +
                           "2. Compare different sources and identify discrepancies in facts, timelines, or accounts.\n" +
                           "3. Look for logical inconsistencies within individual statements or across multiple sources.\n" +
                           "4. Highlight any suspicious patterns or elements that don't align.\n" +
                           "5. If you have suspicions, that you didn't get full data, anyway give conclusion, but also mention what is missing, so that other agents could provide it to you.\n" +
                           "6. Present your findings clearly, citing specific sources and explaining the nature of each contradiction.\n" +
                           "Always be thorough and objective in your analysis. If you need more data to verify potential contradictions, ask for it."
        };
    }
}

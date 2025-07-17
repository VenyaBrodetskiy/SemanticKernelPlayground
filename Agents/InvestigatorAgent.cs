using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace SemanticKernelPlayground.Agents;

public static class InvestigatorAgent
{
    public static ChatCompletionAgent Create(Kernel kernel)
    {
        return new ChatCompletionAgent()
        {
            Name = "InvestigatorAgent",
            Description = "An agent that manages other agents in order to provide investigation about the case",
            Kernel = kernel,
            Arguments = new(
                new OpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    ResponseFormat = typeof(GroupChatManagerResult<string>)
                }),
            Instructions = $"""
                            You are an investigator agent. Your task is to manage and orchestrate other agents to provide deep investigation about the case. 
                            You have access to two specialized agents:
                            1. SearchInDataAgent - Use this agent to search for specific information in the case files and evidence, like statements, facts.
                            2. AnalysisAgent - Use this agent to analyze data, but he needs data to be provided first
                            Both agents can return to you with their findings for you to summarize and present comprehensive results.
                            Coordinate their efforts to build a complete picture of the case.

                            You task is to create plan of investigation, explaining which agent should be called first, which should be called next. Also explain it to other agents, explain why you selected them and what is their task. Put this information in Reason field.
                            In value field you should put the name of the participant you would like to select next. If you think that investigation is finished, then return InvestigatorAgent with Reason = "Investigation is finished, I will summarize results now" and then call FilterResults method to summarize results.
                            """,
        };
    }
}

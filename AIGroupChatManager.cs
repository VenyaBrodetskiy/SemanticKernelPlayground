using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0001

namespace SemanticKernelPlayground;

public sealed class AiGroupChatManager(string userRequest, IChatCompletionService chatCompletion) : GroupChatManager
{
    private static class Prompts
    {
        public static string Termination(string userRequest) =>
            $"""
             You are a lead investigator determining if the current investigation on '{userRequest}' is complete.
             Evaluate whether all relevant information has been gathered, contradictions analyzed, and a conclusion reached.
             If the investigation has reached a comprehensive conclusion with sufficient evidence and analysis, respond with True.
             If there are still unanswered questions, unexplored angles, or insufficient analysis, respond with False.
             Respond ONLY with True or False
             """;

        public static string Selection(string userRequest, string availableAgents) =>
            $"""
             You are an investigator agent. Your task is to manage and orchestrate other agents to provide deep investigation about the case. 
             You have access to two specialized agents:
             1. SearchInDataAgent - Use this agent to search for specific information in the case files and evidence, like statements, facts.
             2. AnalysisAgent - Use this agent to analyze facts, statement etc, but he needs data to be provided first
             Both agents can return to you with their findings for you to summarize and present comprehensive results.
             Coordinate their efforts to build a complete picture of the case.
             
             You task is to summarize results of investigation efforts done up to this moment, create plan of investigation (next steps only), explaining which agent should be called first, which should be called next. Also explain it to other agents, explain why you selected them and what is their task. Put this information in Reason field.
             In value field you should put the name of the participant you would like to select next. If you think that investigation is finished, then return InvestigatorAgent with Reason = "Investigation is finished, I will summarize results now" and then call FilterResults method to summarize results.
             """;

        public static string Filter(string userRequest) =>
            $"""
             You are a lead investigator finalizing the investigation on '{userRequest}'.
             Synthesize the key findings from the investigation, including:

             1. Summary of critical evidence discovered
             2. Major contradictions or inconsistencies identified
             3. Conclusions that can be drawn from the evidence
             4. Any remaining uncertainties or limitations of the investigation

             Present a comprehensive but concise final report on the investigation's findings.
             """;
    }

    /// <inheritdoc/>
    public override ValueTask<GroupChatManagerResult<string>> FilterResults(ChatHistory history,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Filtering results]> ");
        Console.ResetColor();

        var result = GetResponseAsync<string>(history, Prompts.Filter(userRequest), cancellationToken);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Result.Value}, reason: {result.Result.Reason}");
        Console.ResetColor();
        return result;
    }

    /// <inheritdoc/>
    public override ValueTask<GroupChatManagerResult<string>> SelectNextAgent(ChatHistory history, GroupChatTeam team,
        CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Selecting next agent]> ");
        Console.ResetColor();

        var result = GetResponseAsync<string>(history, Prompts.Selection(userRequest, team.FormatList()), cancellationToken);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Result.Value}, reason: {result.Result.Reason}");
        Console.ResetColor();

        history.AddMessage(AuthorRole.Tool, result.Result.Reason);
        return result;
    }

    /// <inheritdoc/>
    public override ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInput(ChatHistory history, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Should request user input?]> ");
        Console.ResetColor();
        return ValueTask.FromResult(new GroupChatManagerResult<bool>(false) { Reason = "The AI group chat manager does not request user input." });
    }

    /// <inheritdoc/>
    public override async ValueTask<GroupChatManagerResult<bool>> ShouldTerminate(ChatHistory history, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("Chat manager [Should terminate?]> ");
        Console.ResetColor();

        GroupChatManagerResult<bool> result = await base.ShouldTerminate(history, cancellationToken);
        if (!result.Value)
        {
            result = await this.GetResponseAsync<bool>(history, Prompts.Termination(userRequest), cancellationToken);
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Value}, reason: {result.Reason}");
        Console.ResetColor();

        return result;
    }

    private async ValueTask<GroupChatManagerResult<TValue>> GetResponseAsync<TValue>(ChatHistory history, string prompt, CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings executionSettings = new() { ResponseFormat = typeof(GroupChatManagerResult<TValue>) };
        var request = new ChatHistory(history);
        request.AddAssistantMessage(prompt);
        ChatMessageContent response = await chatCompletion.GetChatMessageContentAsync(request, executionSettings, kernel: null, cancellationToken);
        string responseText = response.ToString();
        return
            JsonSerializer.Deserialize<GroupChatManagerResult<TValue>>(responseText) ??
            throw new InvalidOperationException($"Failed to parse response: {responseText}");
    }
}
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0001

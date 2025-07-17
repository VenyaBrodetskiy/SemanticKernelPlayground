using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0001

namespace SemanticKernelPlayground;

public sealed class AiGroupChatManager(string topic, IChatCompletionService chatCompletion) : GroupChatManager
{
    private static class Prompts
    {
        public static string Termination(string topic) =>
            $"""
             You are a lead investigator determining if the current investigation on '{topic}' is complete.
             Evaluate whether all relevant information has been gathered, contradictions analyzed, and a conclusion reached.
             If the investigation has reached a comprehensive conclusion with sufficient evidence and analysis, respond with True.
             If there are still unanswered questions, unexplored angles, or insufficient analysis, respond with False.
             Respond ONLY with True or False
             """;

        public static string Selection(string topic, string participants) =>
            $"""
             You are an investigator agent. Your task is to manage and orchestrate other agents to provide deep investigation about the case. " +
             "You have access to two specialized agents:\n" +
             "1. SearchInDataAgent - Use this agent to search for specific information in the case files and evidence.\n" +
             "2. AnalysisAgent - Use this agent to analyze data for contradictions, inconsistencies, and conflicting statements.\n" +
             "Both agents can return to you with their findings for you to summarize and present comprehensive results. " +
             "Coordinate their efforts to build a complete picture of the case.

             Please respond with only the name of the participant you would like to select.
             In Reason explain why you selected this participant and what task you want them to perform.
             """;

        public static string Filter(string topic) =>
            $"""
             You are a lead investigator finalizing the investigation on '{topic}'.
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

        var result = GetResponseAsync<string>(history, Prompts.Filter(topic), cancellationToken);
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

        

        var lastMessage = history.LastOrDefault();
        if (lastMessage?.AuthorName == "InvestigatorAgent")
        {
            var managerDecision = JsonSerializer.Deserialize<GroupChatManagerResult<string>>(lastMessage.Content);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Result: {managerDecision.Value}, reason: {managerDecision.Reason}");
            Console.ResetColor();
            return ValueTask.FromResult(managerDecision);
        }
        if (string.IsNullOrWhiteSpace(lastMessage.Content))
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("Error: Last message content is null, cannot determine next agent. Doing fallback");
            Console.ResetColor();
            var result2 = GetResponseAsync<string>(history, Prompts.Selection(topic, team.FormatList()), cancellationToken);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Result: {result2.Result.Value}, reason: {result2.Result.Reason}");
            Console.ResetColor();
            //int randIndex = new Random().Next(0, team.Count - 1);
            //var next = team.Skip(randIndex).First().Key;
            //GroupChatManagerResult<string> result2 = new(next) { Reason = "Some good reason " };

            return result2;
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: InvestigatorAgent, reason: Auto route to Investigator");
        Console.ResetColor();

        GroupChatManagerResult<string> result = new("InvestigatorAgent") { Reason = "Some good reason " };
        return ValueTask.FromResult(result);
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
            result = await this.GetResponseAsync<bool>(history, Prompts.Termination(topic), cancellationToken);
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Value}, reason: {result.Reason}");
        Console.ResetColor();
        return result;
    }

    private async ValueTask<GroupChatManagerResult<TValue>> GetResponseAsync<TValue>(ChatHistory history, string prompt, CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings executionSettings = new() { ResponseFormat = typeof(GroupChatManagerResult<TValue>) };
        ChatHistory request = [.. history, new ChatMessageContent(AuthorRole.System, prompt)];
        ChatMessageContent response = await chatCompletion.GetChatMessageContentAsync(request, executionSettings, kernel: null, cancellationToken);
        string responseText = response.ToString();
        return
            JsonSerializer.Deserialize<GroupChatManagerResult<TValue>>(responseText) ??
            throw new InvalidOperationException($"Failed to parse response: {responseText}");
    }
}
#pragma warning restore SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0001

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
        private static string FormatTeam(GroupChatTeam team)
        {
            if (team.Count == 0)
                return "No specific agents are currently available.";

            var agentDescriptions = team.Select((kvp, index) => 
                $"{index + 1}. {kvp.Key} ({kvp.Value.Type}) - {kvp.Value.Description}");
            
            return string.Join("\n", agentDescriptions);
        }

        private static string BaseInvestigationPrompt(string userRequest) =>
            $"""
             You are an investigator planner agent. Your task is to manage and orchestrate other agents to provide deep investigation about the criminal case. User request is '{userRequest}'. 
             """;

        public static string Termination(string userRequest) =>
            $"""
             {BaseInvestigationPrompt(userRequest)}

             Your job is to decide whether the investigation is complete. To do so:

             1. **Review full chat history** (all system/user/assistant and Function/Tool messages).
             2. **List every investigator planner instruction from his last planning** issued so far (e.g. “Call SearchInDataAgent”, “Call AnalysisAgent”, “Call FilterResults”). Most important planner instruction is the last instruction.
             3. **Short summary of status so far**:
                - **Planner said:** “…”  
                - **Investigation current status:** e.g. “SearchInDataAgent ran and returned X (good/bad). AnalysisAgent has/has not run.”  
                - **Next planned steps:** based on planner’s outstanding instructions
             4. **Final decision**:
                - If **any** planner step is still pending or incomplete, return **False**.
                - Only if **all** planner steps are executed, contradictions analyzed, return **True**.

             Overall, you should tend to continue investigation always, especially if investigation planner has not yet concluded that investigation is finished.
             
             **Output format** (no extra text):
             - **Value:** `True` or `False`  
             - **Reason:** A deep reasoning covering:
               1. Which planner steps remain pending/incomplete (or “none”).  
               2. The investigation status summary.  
               3. Why you judged completion status.

             Respond **only** with the JSON-style fields `Value` and `Reason`.
             """;

        public static string Selection(string userRequest, GroupChatTeam team) =>
            $"""
             {BaseInvestigationPrompt(userRequest)}
             
             You have access to the following specialized agents:
             {FormatTeam(team)}
             These agents can return to you with their findings for you to summarize and present comprehensive results.
             Coordinate their efforts to build a complete picture of the case.

             Your task is to decide the next steps of the investigation and select the most appropriate agents to continue.
             Based on the current state of the investigation, determine:
             1. What specific tasks need to be accomplished in next couple of calls
             2. Which agents are best suited for this task
             3. What you expect from the selected agents
             
             As an investigator, you have big experience and good feeling about where to dig deeper, so guide your team to success, be very cautious and sceptical about their findings, check twice everything.
             
             Explain to other agents why you selected them and what is their specific task. 
             It's especially important to give good instructions to next agent, as after his actions you might decide to adjust investigation flow.
             In the beginning of your response mention, that you are investigator planner, and that your instructions should be respected by other agents.
             Put this information in the Reason field. Make it clear and it's okay to be verbose.
             In the Value field, put the name of the participant you would like to select next. Never return null or empty value, even if you think that investigation is finished, still return one of the available agents, with Reason = "Investigation is finished, I need to summarize results now"
             """;

        public static string Filter(string userRequest) =>
            $"""
             You are a lead investigator finalizing the investigation on '{userRequest}'.
             Synthesize the key findings from the investigation, including:

             1. Summary of critical evidence discovered
             2. Major contradictions or inconsistencies identified
             3. Conclusions that can be drawn from the evidence
             4. Any remaining uncertainties or limitations of the investigation

             Present a comprehensive final report on the investigation's findings.
             Remember than user won't see previous chat history, so you need to summarize everything in a single response.
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

        var result = GetResponseAsync<string>(history, Prompts.Selection(userRequest, team), cancellationToken);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Result.Value}, reason: {result.Result.Reason}");
        Console.ResetColor();

        history.AddMessage(AuthorRole.Assistant, result.Result.Reason);
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
            result = await GetResponseAsync<bool>(history, Prompts.Termination(userRequest), cancellationToken);
            history.AddMessage(AuthorRole.Assistant, result.Reason);
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Result: {result.Value}, reason: {result.Reason}");
        Console.ResetColor();

        return result;
    }

    private async ValueTask<GroupChatManagerResult<TValue>> GetResponseAsync<TValue>(ChatHistory history, string prompt, CancellationToken cancellationToken = default)
    {
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            ResponseFormat = typeof(GroupChatManagerResult<TValue>),
            Temperature = 0.1f,
        };
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

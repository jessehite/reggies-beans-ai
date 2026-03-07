using System.Text.Json;
using ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.FeatureAnalysis;

public sealed class GeneratePlanHandler : StageHandler<RequirementsDocument, ImplementationPlan>
{
    private const string SystemPrompt = """
        You are a software architect. Create a concrete implementation plan from the given requirements document.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "approach": "high-level description of the implementation approach",
          "tasks": [
            {"name": "task name", "description": "what needs to be done and why"}
          ]
        }

        Include 3-7 concrete, actionable tasks. Order them by implementation sequence.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILlmClient _llm;

    public GeneratePlanHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<ImplementationPlan>> HandleAsync(
        RequirementsDocument input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var requirementsJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Create an implementation plan for these requirements:\n\n{requirementsJson}");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<ImplementationPlan>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = StripMarkdownFences(response.Content);
            var plan = JsonSerializer.Deserialize<ImplementationPlan>(json, JsonOptions);
            if (plan is null)
                return HandleResult<ImplementationPlan>.Failed("LLM returned null implementation plan.");

            return HandleResult<ImplementationPlan>.Succeeded(plan);
        }
        catch (JsonException ex)
        {
            return HandleResult<ImplementationPlan>.Failed(
                $"Failed to parse LLM response as ImplementationPlan: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }

    private static string StripMarkdownFences(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }
        return trimmed;
    }
}

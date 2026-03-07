using System.Text.Json;
using ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.FeatureAnalysis;

public sealed class AnalyzeRequirementsHandler : StageHandler<FeatureRequest, RequirementsDocument>
{
    private const string SystemPrompt = """
        You are a software requirements analyst. Analyze the given feature request and return a structured requirements document.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "summary": "one-sentence summary of the feature",
          "goals": ["goal 1", "goal 2"],
          "constraints": ["constraint 1"],
          "acceptanceCriteria": ["criterion 1", "criterion 2"]
        }

        Be concrete and specific. Extract real goals and constraints from the description.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILlmClient _llm;

    public AnalyzeRequirementsHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<RequirementsDocument>> HandleAsync(
        FeatureRequest input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Analyze this feature request:\n\n{input.Description}");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<RequirementsDocument>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = StripMarkdownFences(response.Content);
            var document = JsonSerializer.Deserialize<RequirementsDocument>(json, JsonOptions);
            if (document is null)
                return HandleResult<RequirementsDocument>.Failed("LLM returned null requirements document.");

            return HandleResult<RequirementsDocument>.Succeeded(document);
        }
        catch (JsonException ex)
        {
            return HandleResult<RequirementsDocument>.Failed(
                $"Failed to parse LLM response as RequirementsDocument: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
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

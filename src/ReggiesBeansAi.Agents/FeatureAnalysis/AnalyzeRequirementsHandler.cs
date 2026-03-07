using System.Text.Json;
using ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.FeatureAnalysis;

public sealed class AnalyzeRequirementsHandler : StageHandler<FeatureRequest, RequirementsDocument>
{
    private const string SystemPrompt = """
        You are a software requirements analyst. A developer has submitted a feature request describing something they want added to their software application.

        Your job is to read their description and extract structured requirements from it. You are NOT building anything — you are analyzing what they asked for.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "summary": "one-sentence summary of what the developer wants",
          "goals": ["what this feature should achieve"],
          "constraints": ["technical or UX constraints implied by the request"],
          "acceptanceCriteria": ["specific, testable conditions that must be true when the feature is done"]
        }

        Base everything strictly on what the developer described. Do not invent requirements they did not mention.
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
            UserPrompt: $"A developer submitted this feature request for their application:\n\n{input.Description}");

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

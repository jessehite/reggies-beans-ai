using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class GoNoGoHandler : StageHandler<TechFeasibilityReport, GoNoGoDecision>
{
    private const string SystemPrompt = """
        You are a product strategy director. Synthesize the technical feasibility assessment into a clear Go/No-Go recommendation for each product idea.

        Use these decision rules:
        - "build": strong market fit, feasible complexity, manageable risk
        - "defer": promising but premature (wrong timing, missing capability, or needs more research)
        - "kill": poor market fit, excessive complexity, or unacceptable risk

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "decisions": [
            {
              "ideaTitle": "exact title from input",
              "recommendation": "build" or "defer" or "kill",
              "compositeScore": 7.2,
              "keyAssumptions": ["assumption that could invalidate this decision"],
              "rationale": "two to three sentence explanation of the recommendation"
            }
          ],
          "executiveSummary": "one-paragraph summary of the overall decision landscape suitable for a stakeholder briefing"
        }

        Order decisions from strongest "build" recommendation to weakest. CompositeScore should reflect combined market and technical signals on a 1-10 scale.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public GoNoGoHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<GoNoGoDecision>> HandleAsync(
        TechFeasibilityReport input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var reportJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Synthesize the following technical feasibility report into Go/No-Go decisions:\n\n{reportJson}",
            Model: "claude-sonnet-4-6");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<GoNoGoDecision>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var decision = JsonSerializer.Deserialize<GoNoGoDecision>(json, JsonOptions);
            if (decision is null)
                return HandleResult<GoNoGoDecision>.Failed("LLM returned null go/no-go decision.");

            return HandleResult<GoNoGoDecision>.Succeeded(decision);
        }
        catch (JsonException ex)
        {
            return HandleResult<GoNoGoDecision>.Failed(
                $"Failed to parse LLM response as GoNoGoDecision: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

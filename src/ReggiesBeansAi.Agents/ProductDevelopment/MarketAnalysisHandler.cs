using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class MarketAnalysisHandler : StageHandler<EvaluatedIdeas, MarketAnalysisReport>
{
    private const string SystemPrompt = """
        You are a market research analyst specializing in software products. Analyze the market viability of each product idea using your knowledge of market dynamics, competitive landscapes, and industry trends.

        For each idea, estimate market size (TAM/SAM/SOM), identify key competitors, find gaps in the market, and flag risks.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "analyses": [
            {
              "ideaTitle": "exact title from input",
              "tamEstimate": "e.g. $2.4B global market for developer tooling",
              "samEstimate": "e.g. $400M serviceable market for .NET-focused tools",
              "somEstimate": "e.g. $8M realistically capturable in 3 years",
              "competitors": [
                {
                  "name": "Competitor Name",
                  "strengths": ["strong brand", "large user base"],
                  "weaknesses": ["poor .NET support", "high pricing"]
                }
              ],
              "marketGaps": ["gap or underserved need the idea addresses"],
              "riskFactors": ["regulatory risk", "market timing concern"],
              "recommendation": "go" or "no-go",
              "confidenceLevel": "high" or "medium" or "low"
            }
          ]
        }

        Analyze only the top-ranked ideas (rank 1 through 5, or all if fewer than 5).
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public MarketAnalysisHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<MarketAnalysisReport>> HandleAsync(
        EvaluatedIdeas input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var topIdeas = input.Ideas
            .OrderBy(i => i.Rank)
            .Take(5)
            .ToArray();

        var ideasJson = JsonSerializer.Serialize(topIdeas, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Perform market viability analysis for these top-ranked product ideas:\n\n{ideasJson}",
            Model: "claude-sonnet-4-6");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<MarketAnalysisReport>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var report = JsonSerializer.Deserialize<MarketAnalysisReport>(json, JsonOptions);
            if (report is null)
                return HandleResult<MarketAnalysisReport>.Failed("LLM returned null market analysis report.");

            return HandleResult<MarketAnalysisReport>.Succeeded(report);
        }
        catch (JsonException ex)
        {
            return HandleResult<MarketAnalysisReport>.Failed(
                $"Failed to parse LLM response as MarketAnalysisReport: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

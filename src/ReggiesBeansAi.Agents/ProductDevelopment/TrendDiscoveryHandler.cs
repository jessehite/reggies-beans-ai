using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class TrendDiscoveryHandler : StageHandler<DiscoveryPrompt, DiscoveredOpportunities>
{
    private const string SystemPrompt = """
        You are a market intelligence researcher with access to Google Search. Your job is to discover real, current trends and translate them into concrete software product opportunities.

        Using your search capabilities, research the user's area of interest. Look for:
        - Trending discussions on Hacker News, Reddit, and developer forums
        - Recently launched products and tools (Product Hunt, GitHub trending)
        - Pain points developers and businesses are actively complaining about
        - Emerging market gaps where existing solutions are inadequate
        - Industry shifts, regulatory changes, or technology inflection points

        Synthesize your findings into trend signals and opportunity profiles.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "trends": [
            {
              "source": "where you found this signal (e.g. Hacker News, Google Trends, industry report)",
              "description": "what the trend is and why it matters",
              "relevance": "how it connects to the user's area of interest"
            }
          ],
          "opportunities": [
            {
              "domain": "specific domain/industry niche (e.g. developer onboarding, healthcare compliance)",
              "targetAudience": "who would buy/use this product",
              "suggestedThemes": ["specific theme or angle to explore"],
              "rationale": "why this is a good opportunity right now, based on the trends you found",
              "supportingTrends": ["reference to specific trends above that support this opportunity"]
            }
          ]
        }

        Find 5 to 10 trend signals. Synthesize them into 3 to 5 distinct opportunity profiles. Every opportunity must be grounded in at least one real trend — do not fabricate opportunities without evidence.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public TrendDiscoveryHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<DiscoveredOpportunities>> HandleAsync(
        DiscoveryPrompt input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var sources = input.SignalSources.Length > 0
            ? string.Join(", ", input.SignalSources)
            : "Hacker News, Reddit, Product Hunt, Google Trends, industry publications";

        var userPrompt = $"""
            Research current trends and identify software product opportunities in this area:

            Area of interest: {input.AreaOfInterest}
            Signal sources to prioritize: {sources}

            Search for what's happening RIGHT NOW — recent discussions, launches, pain points, and market shifts. Ground every opportunity in real evidence.
            """;

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: userPrompt,
            Model: "gemini-3.1-pro-preview");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<DiscoveredOpportunities>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var opportunities = JsonSerializer.Deserialize<DiscoveredOpportunities>(json, JsonOptions);
            if (opportunities is null)
                return HandleResult<DiscoveredOpportunities>.Failed("LLM returned null discovery results.");

            return HandleResult<DiscoveredOpportunities>.Succeeded(opportunities);
        }
        catch (JsonException ex)
        {
            return HandleResult<DiscoveredOpportunities>.Failed(
                $"Failed to parse LLM response as DiscoveredOpportunities: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

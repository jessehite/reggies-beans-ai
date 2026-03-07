using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class IdeaEvaluationHandler : StageHandler<IdeaBatch, EvaluatedIdeas>
{
    private const string SystemPrompt = """
        You are a product evaluation analyst. Score each product idea on five criteria using a 1-10 scale, then compute a weighted composite score and rank all ideas.

        Scoring weights:
        - Novelty: 20%
        - Feasibility: 25%
        - MarketPotential: 25%
        - Differentiation: 15%
        - Alignment: 15%

        CompositeScore = (novelty * 0.20) + (feasibility * 0.25) + (marketPotential * 0.25) + (differentiation * 0.15) + (alignment * 0.15)

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "ideas": [
            {
              "title": "exact title from input",
              "noveltyScore": 7,
              "feasibilityScore": 8,
              "marketPotentialScore": 6,
              "differentiationScore": 7,
              "alignmentScore": 9,
              "compositeScore": 7.4,
              "justification": "two-sentence explanation of the scores",
              "rank": 1
            }
          ]
        }

        Rank 1 is the highest-scoring idea. Include all ideas from the input; do not drop any.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public IdeaEvaluationHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<EvaluatedIdeas>> HandleAsync(
        IdeaBatch input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var ideasJson = JsonSerializer.Serialize(input.Ideas, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Evaluate and score the following product ideas:\n\n{ideasJson}",
            Model: "claude-sonnet-4-6");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<EvaluatedIdeas>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var evaluated = JsonSerializer.Deserialize<EvaluatedIdeas>(json, JsonOptions);
            if (evaluated is null)
                return HandleResult<EvaluatedIdeas>.Failed("LLM returned null evaluation.");

            return HandleResult<EvaluatedIdeas>.Succeeded(evaluated);
        }
        catch (JsonException ex)
        {
            return HandleResult<EvaluatedIdeas>.Failed(
                $"Failed to parse LLM response as EvaluatedIdeas: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

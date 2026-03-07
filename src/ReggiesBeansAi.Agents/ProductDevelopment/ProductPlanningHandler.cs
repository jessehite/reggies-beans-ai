using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class ProductPlanningHandler : StageHandler<GoNoGoDecision, ProductRequirementsDocument>
{
    private const string SystemPrompt = """
        You are a senior product manager. Based on the Go/No-Go decision report, select the top "build" recommendation and generate a comprehensive Product Requirements Document (PRD) for it.

        The PRD must contain well-formed user stories with Given/When/Then acceptance criteria, a clear MVP scope, measurable success metrics, and concrete milestones with dependencies.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "productVision": "one to two sentence vision statement describing the product and the problem it solves",
          "userStories": [
            {
              "title": "As a [persona], I want to [action]",
              "description": "context and motivation for this story",
              "acceptanceCriteria": [
                {
                  "given": "the precondition",
                  "when": "the user action",
                  "then": "the expected outcome"
                }
              ]
            }
          ],
          "mvpFeatures": ["feature included in the initial release"],
          "futureFeatures": ["feature deferred to a later phase"],
          "successMetrics": [
            {
              "metric": "metric name",
              "target": "measurable target value"
            }
          ],
          "milestones": [
            {
              "name": "milestone name",
              "description": "what is delivered",
              "dependencies": ["other milestone or external dependency"]
            }
          ],
          "risks": ["risk that could impact delivery or adoption"]
        }

        Generate 5 to 8 user stories covering core MVP functionality. Include 3 to 5 success metrics with specific numeric targets.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public ProductPlanningHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<ProductRequirementsDocument>> HandleAsync(
        GoNoGoDecision input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var decisionJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Generate a PRD for the top 'build' idea from this Go/No-Go report:\n\n{decisionJson}",
            Model: "claude-opus-4-6");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<ProductRequirementsDocument>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var prd = JsonSerializer.Deserialize<ProductRequirementsDocument>(json, JsonOptions);
            if (prd is null)
                return HandleResult<ProductRequirementsDocument>.Failed("LLM returned null PRD.");

            return HandleResult<ProductRequirementsDocument>.Succeeded(prd);
        }
        catch (JsonException ex)
        {
            return HandleResult<ProductRequirementsDocument>.Failed(
                $"Failed to parse LLM response as ProductRequirementsDocument: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

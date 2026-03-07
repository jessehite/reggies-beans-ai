using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class TechFeasibilityHandler : StageHandler<MarketAnalysisReport, TechFeasibilityReport>
{
    private const string SystemPrompt = """
        You are a senior .NET software architect. Assess the technical feasibility of each product idea given a .NET ecosystem constraint (C#, ASP.NET Core, Entity Framework, Azure).

        For each idea, estimate build effort, rate complexity, identify the right tech stack, flag key technical risks with mitigations, flag build-vs-buy decisions, and list required NuGet packages or third-party services.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "assessments": [
            {
              "ideaTitle": "exact title from input",
              "estimatedEffort": "e.g. 12-16 developer-weeks across 3 phases",
              "complexityRating": 7,
              "techStackRecommendations": ["ASP.NET Core 8", "Entity Framework Core", "Azure Service Bus"],
              "keyRisks": [
                {
                  "risk": "Real-time sync complexity",
                  "mitigation": "Use SignalR with Azure backplane for horizontal scaling"
                }
              ],
              "buildVsBuyDecisions": ["Build: custom workflow engine; Buy: Stripe for payments"],
              "requiredPackages": ["MediatR", "FluentValidation", "Polly", "Serilog"]
            }
          ]
        }

        Complexity rating: 1 (trivial CRUD) to 10 (distributed systems, novel algorithms). Be conservative with effort estimates — add 30% buffer for integration and testing.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public TechFeasibilityHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<TechFeasibilityReport>> HandleAsync(
        MarketAnalysisReport input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var reportJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Assess technical feasibility for the following market-validated product ideas (using .NET ecosystem):\n\n{reportJson}",
            Model: "claude-opus-4-6");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<TechFeasibilityReport>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var report = JsonSerializer.Deserialize<TechFeasibilityReport>(json, JsonOptions);
            if (report is null)
                return HandleResult<TechFeasibilityReport>.Failed("LLM returned null tech feasibility report.");

            return HandleResult<TechFeasibilityReport>.Succeeded(report);
        }
        catch (JsonException ex)
        {
            return HandleResult<TechFeasibilityReport>.Failed(
                $"Failed to parse LLM response as TechFeasibilityReport: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

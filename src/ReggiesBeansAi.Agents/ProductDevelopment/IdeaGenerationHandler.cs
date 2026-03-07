using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class IdeaGenerationHandler : StageHandler<IdeationInput, IdeaBatch>
{
    private const string SystemPrompt = """
        You are a product ideation expert. Generate a batch of novel, diverse product ideas based on the provided domain, target audience, and constraints.

        Produce 5 to 10 distinct ideas. Avoid incremental improvements — focus on non-obvious concepts with genuine value.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "ideas": [
            {
              "title": "concise product name",
              "description": "one-paragraph description of the product and what it does",
              "targetPersona": "who this is for and their key pain point",
              "valueProposition": "the core benefit this delivers over alternatives",
              "tags": ["SaaS", "developer-tool", "consumer-app"]
            }
          ]
        }

        Do not repeat any rejected ideas listed in the input. Stay within the stated domain and audience constraints.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public IdeaGenerationHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<IdeaBatch>> HandleAsync(
        IdeationInput input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var userPrompt = $"""
            Generate product ideas for the following context:

            Domain: {input.Domain}
            Target audience: {input.TargetAudience}
            Seed themes: {(input.SeedThemes.Length > 0 ? string.Join(", ", input.SeedThemes) : "none")}
            Previously rejected ideas to avoid: {(input.RejectedIdeas.Length > 0 ? string.Join(", ", input.RejectedIdeas) : "none")}
            """;

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: userPrompt,
            Model: "claude-opus-4-6");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<IdeaBatch>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var batch = JsonSerializer.Deserialize<IdeaBatch>(json, JsonOptions);
            if (batch is null)
                return HandleResult<IdeaBatch>.Failed("LLM returned null idea batch.");

            return HandleResult<IdeaBatch>.Succeeded(batch);
        }
        catch (JsonException ex)
        {
            return HandleResult<IdeaBatch>.Failed(
                $"Failed to parse LLM response as IdeaBatch: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

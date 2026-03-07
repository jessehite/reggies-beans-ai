using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class BacklogGenerationHandler : StageHandler<ArchitectureDocument, ProductBacklog>
{
    private const string SystemPrompt = """
        You are an agile delivery lead. Break down the architecture into a prioritized product backlog with epics, user stories, and tasks. Then organize stories into two-week sprints based on dependencies and logical delivery order.

        Assume a team of 2 developers with a velocity of 20 story points per sprint.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "epics": [
            {
              "name": "epic name",
              "description": "what this epic delivers",
              "stories": [
                {
                  "title": "story title",
                  "description": "what needs to be implemented",
                  "storyPoints": 5,
                  "definitionOfDone": "specific, testable completion criteria",
                  "tasks": [
                    {
                      "name": "task name",
                      "description": "specific implementation step"
                    }
                  ],
                  "dependencies": ["other story titles this depends on"]
                }
              ]
            }
          ],
          "sprints": [
            {
              "sprintNumber": 1,
              "storyTitles": ["story title included in this sprint"],
              "totalPoints": 18
            }
          ]
        }

        Story point scale: 1 (trivial), 2 (small), 3 (medium), 5 (large), 8 (complex), 13 (very complex). Generate 3 to 5 epics with 2 to 4 stories each. Each story should have 2 to 5 concrete tasks.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public BacklogGenerationHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<ProductBacklog>> HandleAsync(
        ArchitectureDocument input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var architectureJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Generate a prioritized product backlog and sprint plan from this architecture document:\n\n{architectureJson}",
            Model: "claude-sonnet-4-6",
            MaxTokens: 16000);

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<ProductBacklog>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var backlog = JsonSerializer.Deserialize<ProductBacklog>(json, JsonOptions);
            if (backlog is null)
                return HandleResult<ProductBacklog>.Failed("LLM returned null product backlog.");

            return HandleResult<ProductBacklog>.Succeeded(backlog);
        }
        catch (JsonException ex)
        {
            return HandleResult<ProductBacklog>.Failed(
                $"Failed to parse LLM response as ProductBacklog: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

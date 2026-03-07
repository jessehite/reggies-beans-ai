using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class CodeGenerationHandler : StageHandler<ProductBacklog, GeneratedCodePackage>
{
    private const string SystemPrompt = """
        You are an expert .NET software engineer. Generate working C# source code for the Sprint 1 stories from the provided product backlog.

        Follow these conventions:
        - Clean Architecture: separate Domain, Application, Infrastructure, and API projects
        - ASP.NET Core 8 minimal APIs or controllers
        - Entity Framework Core for data access with a DbContext
        - Record types for DTOs, classes for services and repositories
        - Constructor injection for dependencies
        - Async/await throughout with CancellationToken parameters
        - Nullable reference types enabled

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "files": [
            {
              "path": "src/ProjectName/Folder/FileName.cs",
              "content": "full C# source code for this file",
              "fileType": "cs"
            }
          ],
          "solutionStructure": "description of the solution layout and project references"
        }

        Generate all files needed for the Sprint 1 stories to compile and run: domain models, interfaces, service implementations, EF Core DbContext, API endpoints, dependency injection setup, and appsettings.json. Aim for 8 to 15 files.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public CodeGenerationHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<GeneratedCodePackage>> HandleAsync(
        ProductBacklog input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var sprint1Stories = input.Sprints.Length > 0
            ? input.Sprints[0].StoryTitles
            : Array.Empty<string>();

        var sprint1Epics = input.Epics
            .Select(e => e with
            {
                Stories = e.Stories
                    .Where(s => sprint1Stories.Contains(s.Title))
                    .ToArray()
            })
            .Where(e => e.Stories.Length > 0)
            .ToArray();

        var sprint1Backlog = new { sprint = 1, epics = sprint1Epics };
        var backlogJson = JsonSerializer.Serialize(sprint1Backlog, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Generate C# source code for the following Sprint 1 backlog:\n\n{backlogJson}",
            Model: "claude-sonnet-4-6",
            MaxTokens: 16000);

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<GeneratedCodePackage>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var package = JsonSerializer.Deserialize<GeneratedCodePackage>(json, JsonOptions);
            if (package is null)
                return HandleResult<GeneratedCodePackage>.Failed("LLM returned null code package.");

            return HandleResult<GeneratedCodePackage>.Succeeded(package);
        }
        catch (JsonException ex)
        {
            return HandleResult<GeneratedCodePackage>.Failed(
                $"Failed to parse LLM response as GeneratedCodePackage: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

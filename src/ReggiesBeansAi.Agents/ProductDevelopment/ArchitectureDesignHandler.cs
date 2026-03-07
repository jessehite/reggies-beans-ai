using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class ArchitectureDesignHandler : StageHandler<ProductRequirementsDocument, ArchitectureDocument>
{
    private const string SystemPrompt = """
        You are a principal .NET software architect. Design the technical architecture for this product using Clean Architecture principles with ASP.NET Core 8, Entity Framework Core, and Azure as the target platform.

        Produce a coherent, internally consistent design covering system components, data model, API endpoints, infrastructure, security, and CI/CD.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "components": [
            {
              "name": "component name (e.g. ProductApi, WorkerService, BlazorFrontend)",
              "responsibility": "what this component does",
              "dependsOn": ["other component names this depends on"]
            }
          ],
          "dataModel": [
            {
              "name": "entity name",
              "fields": [
                {
                  "name": "fieldName",
                  "type": "C# type (e.g. string, int, Guid, DateTimeOffset)",
                  "required": true
                }
              ],
              "relationships": ["e.g. has many Orders, belongs to Customer"]
            }
          ],
          "apiEndpoints": [
            {
              "method": "GET",
              "path": "/api/resource/{id}",
              "description": "what this endpoint does",
              "requestBody": "JSON schema or 'none'",
              "responseBody": "JSON schema summary"
            }
          ],
          "infrastructureRecommendation": "description of Azure services to use (App Service, SQL Database, etc.)",
          "securityApproach": "authentication, authorization, and data protection strategy",
          "ciCdDesign": "CI/CD pipeline stages and tooling (GitHub Actions or Azure DevOps)"
        }

        Design for the MVP scope only. Keep components focused and dependencies minimal. Include 4 to 8 API endpoints covering the core user stories.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public ArchitectureDesignHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<ArchitectureDocument>> HandleAsync(
        ProductRequirementsDocument input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var prdJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Design the technical architecture for this product based on the PRD below:\n\n{prdJson}",
            Model: "claude-opus-4-6");

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<ArchitectureDocument>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var architecture = JsonSerializer.Deserialize<ArchitectureDocument>(json, JsonOptions);
            if (architecture is null)
                return HandleResult<ArchitectureDocument>.Failed("LLM returned null architecture document.");

            return HandleResult<ArchitectureDocument>.Succeeded(architecture);
        }
        catch (JsonException ex)
        {
            return HandleResult<ArchitectureDocument>.Failed(
                $"Failed to parse LLM response as ArchitectureDocument: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

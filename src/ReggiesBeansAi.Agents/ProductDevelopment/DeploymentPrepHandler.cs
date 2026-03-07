using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class DeploymentPrepHandler : StageHandler<CodeReviewReport, DeploymentPackage>
{
    private const string SystemPrompt = """
        You are a senior DevOps engineer specializing in .NET applications on Azure. Generate all deployment artifacts needed to ship the application to a production Azure environment.

        Generate the following artifacts:
        - Dockerfile (multi-stage build, .NET 8 runtime image)
        - docker-compose.yml for local development
        - Azure Bicep template for infrastructure (App Service, SQL Database, Key Vault, Application Insights)
        - GitHub Actions CI/CD pipeline (build, test, deploy stages)
        - Environment-specific appsettings (Development, Staging, Production)
        - Database migration execution script
        - Health check endpoint configuration

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "deploymentFiles": [
            {
              "path": "relative path to the file (e.g. Dockerfile, infra/main.bicep, .github/workflows/deploy.yml)",
              "content": "full file content",
              "fileType": "dockerfile" or "yaml" or "bicep" or "json" or "sh"
            }
          ],
          "rollbackProcedure": "step-by-step rollback instructions if the deployment fails",
          "healthCheckConfig": "description of health check endpoints and monitoring setup"
        }

        Only generate deployment artifacts — do not modify source code. If the code review report contains critical findings, note them in the rollbackProcedure as known risks.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public DeploymentPrepHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<DeploymentPackage>> HandleAsync(
        CodeReviewReport input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var reportJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Generate deployment artifacts for an application that passed code review with the following report:\n\n{reportJson}",
            Model: "claude-sonnet-4-6",
            MaxTokens: 32000);

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<DeploymentPackage>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var package = JsonSerializer.Deserialize<DeploymentPackage>(json, JsonOptions);
            if (package is null)
                return HandleResult<DeploymentPackage>.Failed("LLM returned null deployment package.");

            return HandleResult<DeploymentPackage>.Succeeded(package);
        }
        catch (JsonException ex)
        {
            return HandleResult<DeploymentPackage>.Failed(
                $"Failed to parse LLM response as DeploymentPackage: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

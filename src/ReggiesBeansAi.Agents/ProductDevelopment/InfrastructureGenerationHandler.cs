using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class InfrastructureGenerationHandler : StageHandler<GeneratedFrontendPackage, FullStackPackage>
{
    private const string SystemPrompt = """
        You are a senior DevOps engineer. Given a full-stack application with a .NET 8 backend and a React Native / Expo frontend, generate all infrastructure files needed to run the app locally via Docker Compose.

        Generate the following files:
        - backend/Dockerfile — multi-stage .NET 8 build; expose port 8080
        - frontend/Dockerfile — Node 20 image, runs `npx expo export --platform web` then serves with `npx serve dist`; expose port 8081
        - docker-compose.yml — at the repo root; wires backend (port 8080) and frontend (port 8081) together; includes any required databases or other services derived from the code
        - .env.template — all environment variables the app needs with placeholder values and comments
        - startup.ps1 — PowerShell script that runs `docker compose up -d --build` and prints the URLs

        Rules:
        - Inspect the backend files to determine the correct project file name, startup class, and any required connection strings or env vars.
        - Inspect the frontend files to determine the package.json location and any required EXPO_PUBLIC_* env vars.
        - If the backend uses a SQL database, add a `db` service (mcr.microsoft.com/mssql/server:2022-latest) with a health check and make the backend depend_on it.
        - Set ASPNETCORE_URLS=http://+:8080 and ASPNETCORE_ENVIRONMENT=Development for the backend container.
        - Use named volumes for database data directories.
        - The docker-compose.yml must work on Windows Docker Desktop without any manual steps beyond `docker compose up`.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "backendFiles": [ { "path": "...", "content": "...", "fileType": "..." } ],
          "frontendFiles": [ { "path": "...", "content": "...", "fileType": "..." } ],
          "infraFiles": [
            { "path": "backend/Dockerfile", "content": "...", "fileType": "dockerfile" },
            { "path": "frontend/Dockerfile", "content": "...", "fileType": "dockerfile" },
            { "path": "docker-compose.yml", "content": "...", "fileType": "yaml" },
            { "path": ".env.template", "content": "...", "fileType": "env" },
            { "path": "startup.ps1", "content": "...", "fileType": "ps1" }
          ],
          "backendStructure": "copy from input unchanged",
          "frontendStructure": "copy from input unchanged",
          "dockerComposeOverview": "brief description of services, ports, and any dependencies (e.g. database)"
        }

        Pass backendFiles and frontendFiles through unchanged from the input. Only add new files to infraFiles.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public InfrastructureGenerationHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<FullStackPackage>> HandleAsync(
        GeneratedFrontendPackage input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var inputJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Generate Docker Compose infrastructure for this full-stack application:\n\n{inputJson}",
            Model: "claude-sonnet-4-6",
            MaxTokens: 32000);

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<FullStackPackage>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var package = JsonSerializer.Deserialize<FullStackPackage>(json, JsonOptions);
            if (package is null)
                return HandleResult<FullStackPackage>.Failed("LLM returned null infrastructure package.");

            return HandleResult<FullStackPackage>.Succeeded(package);
        }
        catch (JsonException ex)
        {
            return HandleResult<FullStackPackage>.Failed(
                $"Failed to parse LLM response as FullStackPackage: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

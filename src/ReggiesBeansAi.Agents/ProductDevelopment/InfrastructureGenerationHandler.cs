using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class InfrastructureGenerationHandler : StageHandler<GeneratedFrontendPackage, FullStackPackage>
{
    private const string SystemPrompt = """
        You are a senior DevOps engineer. Given an infrastructure manifest describing a full-stack application (.NET 8 backend + React Native / Expo frontend), generate all infrastructure files needed to run the app locally via Docker Compose.

        Generate the following files:
        - backend/Dockerfile — multi-stage .NET 8 build; expose port 8080
        - frontend/Dockerfile — Node 20 image, runs `npx expo export --platform web` then serves with `npx serve dist`; expose port 8081
        - docker-compose.yml — at the repo root; wires backend (port 8080) and frontend (port 8081) together; includes any required databases or other services derived from the manifest
        - .env.template — all environment variables the app needs with placeholder values and comments
        - startup.ps1 — PowerShell script that runs `docker compose up -d --build` and prints the URLs

        Rules:
        - Use the projectFilePath from the manifest to set the correct COPY and build paths in the backend Dockerfile.
        - If databaseType is not null, add the appropriate database service (e.g. mcr.microsoft.com/mssql/server:2022-latest for sqlserver, postgres:16 for postgres) with a health check and make the backend depend_on it.
        - Include all connectionStrings, backendEnvVars, and frontendEnvVars in the .env.template with placeholder values.
        - Set ASPNETCORE_URLS=http://+:8080 and ASPNETCORE_ENVIRONMENT=Development for the backend container.
        - Use named volumes for database data directories.
        - The docker-compose.yml must work on Windows Docker Desktop without any manual steps beyond `docker compose up`.

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "infraFiles": [
            { "path": "backend/Dockerfile", "content": "...", "fileType": "dockerfile" },
            { "path": "frontend/Dockerfile", "content": "...", "fileType": "dockerfile" },
            { "path": "docker-compose.yml", "content": "...", "fileType": "yaml" },
            { "path": ".env.template", "content": "...", "fileType": "env" },
            { "path": "startup.ps1", "content": "...", "fileType": "ps1" }
          ],
          "dockerComposeOverview": "brief description of services, ports, and any dependencies"
        }
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
        var manifestPayload = new
        {
            infraManifest = input.InfraManifest,
            backendStructure = input.BackendStructure,
            frontendStructure = input.FrontendStructure
        };

        var manifestJson = JsonSerializer.Serialize(manifestPayload, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Generate Docker Compose infrastructure for this application:\n\n{manifestJson}",
            Model: "claude-sonnet-4-6",
            MaxTokens: 16000);

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
            var llmResult = JsonSerializer.Deserialize<InfraLlmResponse>(json, JsonOptions);
            if (llmResult is null)
                return HandleResult<FullStackPackage>.Failed("LLM returned null infrastructure response.");

            var package = new FullStackPackage(
                BackendFiles: input.BackendFiles,
                FrontendFiles: input.FrontendFiles,
                InfraFiles: llmResult.InfraFiles,
                BackendStructure: input.BackendStructure,
                FrontendStructure: input.FrontendStructure,
                DockerComposeOverview: llmResult.DockerComposeOverview);

            return HandleResult<FullStackPackage>.Succeeded(package);
        }
        catch (JsonException ex)
        {
            return HandleResult<FullStackPackage>.Failed(
                $"Failed to parse LLM response as infrastructure output: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }

    private sealed record InfraLlmResponse(
        GeneratedFile[] InfraFiles,
        string DockerComposeOverview);
}

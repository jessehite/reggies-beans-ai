using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class FrontendGenerationHandler : StageHandler<GeneratedCodePackage, GeneratedFrontendPackage>
{
    private const string SystemPrompt = """
        You are an expert React Native / Expo mobile engineer. Given a .NET backend API codebase, generate a polished React Native + Expo mobile app for the same Sprint 1 features.

        Tech stack:
        - Expo SDK 51 (managed workflow)
        - TypeScript throughout
        - React Navigation v6 (stack + bottom tabs)
        - React Native Paper for UI components (Material Design 3)
        - Zustand for lightweight global state
        - TanStack Query v5 (React Query) for server state and caching
        - Axios for HTTP calls to the backend API
        - expo-secure-store for auth token storage

        File structure:
        - app.json, package.json, tsconfig.json — project config
        - App.tsx — root with NavigationContainer and theme
        - src/navigation/ — stack and tab navigators
        - src/screens/ — one file per screen
        - src/components/ — reusable UI components
        - src/api/ — Axios client and typed endpoint functions matching the backend
        - src/store/ — Zustand stores
        - src/types/ — shared TypeScript interfaces mirroring the backend DTOs

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "files": [
            {
              "path": "relative/path/FileName.tsx",
              "content": "full TypeScript/TSX source code",
              "fileType": "tsx"
            }
          ],
          "solutionStructure": "description of the app screen flow and how it connects to the backend",
          "infraManifest": {
            "projectFilePath": "src/ProjectName/ProjectName.csproj",
            "databaseType": "sqlserver | postgres | sqlite | null",
            "connectionStrings": ["DefaultConnection"],
            "backendEnvVars": ["JWT_SECRET"],
            "packageJsonPath": "frontend/package.json",
            "frontendEnvVars": ["EXPO_PUBLIC_API_URL"]
          }
        }

        The infraManifest summarises what a DevOps engineer would need to Dockerise this app:
        - projectFilePath: the .csproj path from the backend files
        - databaseType: the database engine used (null if none)
        - connectionStrings: names of connection strings from appsettings / DbContext
        - backendEnvVars: any environment variables the backend needs beyond ASPNETCORE_*
        - packageJsonPath: path to the frontend package.json
        - frontendEnvVars: any EXPO_PUBLIC_* or other env vars the frontend needs

        Generate 10 to 14 files: config files, App.tsx, navigators, 3 to 4 screens, 2 to 3 shared components, API client, and types. Use realistic mock data where the real API isn't wired yet — mark with // TODO: replace with API call. Make the UI polished with proper loading states, error handling, and empty states.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public FrontendGenerationHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<GeneratedFrontendPackage>> HandleAsync(
        GeneratedCodePackage input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        // Send the backend solution structure and API surface (not full file content) to keep tokens manageable
        var apiSurface = new
        {
            solutionStructure = input.SolutionStructure,
            apiFiles = input.Files
                .Where(f => f.Path.Contains("Controller") || f.Path.Contains("DTO") || f.Path.Contains("Dto") || f.Path.Contains("Service"))
                .Select(f => new { f.Path, f.Content })
                .ToArray()
        };

        var apiJson = JsonSerializer.Serialize(apiSurface, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Generate a React Native + Expo mobile app for this .NET backend API:\n\n{apiJson}",
            Model: "claude-sonnet-4-6",
            MaxTokens: 32000);

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<GeneratedFrontendPackage>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var llmResult = JsonSerializer.Deserialize<FrontendLlmResponse>(json, JsonOptions);
            if (llmResult is null)
                return HandleResult<GeneratedFrontendPackage>.Failed("LLM returned null frontend package.");

            var manifest = llmResult.InfraManifest ?? new InfraManifest(
                ProjectFilePath: "unknown",
                DatabaseType: null,
                ConnectionStrings: [],
                BackendEnvVars: [],
                PackageJsonPath: "package.json",
                FrontendEnvVars: []);

            var result = new GeneratedFrontendPackage(
                BackendFiles: input.Files,
                FrontendFiles: llmResult.Files,
                BackendStructure: input.SolutionStructure,
                FrontendStructure: llmResult.SolutionStructure,
                InfraManifest: manifest);

            return HandleResult<GeneratedFrontendPackage>.Succeeded(result);
        }
        catch (JsonException ex)
        {
            return HandleResult<GeneratedFrontendPackage>.Failed(
                $"Failed to parse LLM response as frontend package: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }

    private sealed record FrontendLlmResponse(
        GeneratedFile[] Files,
        string SolutionStructure,
        InfraManifest? InfraManifest);
}

using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class AutomatedTestingHandler : StageHandler<GeneratedCodePackage, TestResults>
{
    private const string SystemPrompt = """
        You are a senior .NET test engineer. Given the generated source code, write comprehensive xUnit tests using FluentAssertions. Then simulate running those tests and report realistic results.

        Test conventions:
        - xUnit with FluentAssertions
        - Arrange/Act/Assert pattern with clear section comments
        - Test happy paths, edge cases, and failure scenarios
        - Use Moq or NSubstitute for mocking dependencies
        - One test class per production class being tested

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "sourceFiles": [
            {
              "path": "exact path from the input code package",
              "content": "exact content from the input code package",
              "fileType": "cs"
            }
          ],
          "files": [
            {
              "filePath": "tests/ProjectName.Tests/Folder/ClassNameTests.cs",
              "content": "full xUnit test class source code",
              "passCount": 8,
              "failCount": 0,
              "failureMessages": []
            }
          ],
          "coverageReport": "summary of code coverage by component, e.g. Domain: 95%, Application: 82%, API: 70%",
          "identifiedIssues": ["any bugs or issues discovered while writing or running the tests"],
          "qualityAssessment": "overall assessment of test quality, coverage, and any gaps in testing"
        }

        The sourceFiles array must contain all files from the input code package unchanged. Generate one test file per non-trivial source file. If any simulated tests fail, include realistic failure messages.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public AutomatedTestingHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<TestResults>> HandleAsync(
        GeneratedCodePackage input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var packageJson = JsonSerializer.Serialize(input, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Write and simulate xUnit tests for the following generated code package:\n\n{packageJson}",
            Model: "claude-sonnet-4-6",
            MaxTokens: 16000);

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<TestResults>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var results = JsonSerializer.Deserialize<TestResults>(json, JsonOptions);
            if (results is null)
                return HandleResult<TestResults>.Failed("LLM returned null test results.");

            return HandleResult<TestResults>.Succeeded(results);
        }
        catch (JsonException ex)
        {
            return HandleResult<TestResults>.Failed(
                $"Failed to parse LLM response as TestResults: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

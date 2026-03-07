using System.Text.Json;
using ReggiesBeansAi.Agents.Llm;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Agents.ProductDevelopment;

public sealed class CodeReviewHandler : StageHandler<TestResults, CodeReviewReport>
{
    private const string SystemPrompt = """
        You are a principal .NET engineer performing a code review. Review the provided source code and test results for correctness, security, performance, maintainability, and adherence to .NET best practices.

        Review checklist:
        - Async/await correctness (no async void, proper CancellationToken usage)
        - Nullable reference type handling
        - EF Core patterns (N+1 queries, proper use of AsNoTracking)
        - OWASP top 10 security concerns (injection, auth, sensitive data exposure)
        - Exception handling completeness
        - Naming conventions (PascalCase types, camelCase fields, meaningful names)
        - SOLID principle adherence
        - Test quality (are tests meaningful, are edge cases covered)

        Severity levels: "critical" (must fix before ship), "major" (should fix), "minor" (nice to fix), "suggestion" (optional improvement)

        Return ONLY valid JSON with this exact structure — no explanation, no markdown, no code fences:
        {
          "findings": [
            {
              "severity": "critical" or "major" or "minor" or "suggestion",
              "file": "path to the file with the issue",
              "description": "clear description of the issue and why it matters",
              "suggestedFix": "concrete fix or code snippet"
            }
          ],
          "qualityScore": 78,
          "passed": true
        }

        QualityScore is 0-100. Set passed to true if there are no critical findings and qualityScore >= 70. List all findings; do not omit minor or suggestion items.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmClient _llm;

    public CodeReviewHandler(ILlmClient llm)
    {
        _llm = llm;
    }

    protected override async Task<HandleResult<CodeReviewReport>> HandleAsync(
        TestResults input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        var reviewInput = new
        {
            sourceFiles = input.SourceFiles,
            testFiles = input.Files,
            coverageReport = input.CoverageReport,
            identifiedIssues = input.IdentifiedIssues,
            qualityAssessment = input.QualityAssessment
        };

        var reviewJson = JsonSerializer.Serialize(reviewInput, JsonOptions);

        var request = new LlmRequest(
            SystemPrompt: SystemPrompt,
            UserPrompt: $"Perform a code review on the following source code and test results:\n\n{reviewJson}",
            Model: "claude-opus-4-6",
            MaxTokens: 8192);

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return HandleResult<CodeReviewReport>.Failed($"LLM call failed: {ex.Message}");
        }

        try
        {
            var json = LlmResponseParser.StripMarkdownFences(response.Content);
            var report = JsonSerializer.Deserialize<CodeReviewReport>(json, JsonOptions);
            if (report is null)
                return HandleResult<CodeReviewReport>.Failed("LLM returned null code review report.");

            return HandleResult<CodeReviewReport>.Succeeded(report);
        }
        catch (JsonException ex)
        {
            return HandleResult<CodeReviewReport>.Failed(
                $"Failed to parse LLM response as CodeReviewReport: {ex.Message}. Response was: {response.Content[..Math.Min(200, response.Content.Length)]}");
        }
    }
}

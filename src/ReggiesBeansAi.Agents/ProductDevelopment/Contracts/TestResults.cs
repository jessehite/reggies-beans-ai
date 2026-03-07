namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record TestResults(
    TestFileResult[] Files,
    string CoverageReport,
    string[] IdentifiedIssues,
    string QualityAssessment)
{
    // Populated by AutomatedTestingHandler from the GeneratedCodePackage input, not by the LLM
    public GeneratedFile[] SourceFiles { get; init; } = [];
};

public sealed record TestFileResult(
    string FilePath,
    string Content,
    int PassCount,
    int FailCount,
    string[] FailureMessages);

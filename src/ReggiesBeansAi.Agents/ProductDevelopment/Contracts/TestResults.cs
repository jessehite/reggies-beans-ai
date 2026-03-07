namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record TestResults(
    GeneratedFile[] SourceFiles,
    TestFileResult[] Files,
    string CoverageReport,
    string[] IdentifiedIssues,
    string QualityAssessment);

public sealed record TestFileResult(
    string FilePath,
    string Content,
    int PassCount,
    int FailCount,
    string[] FailureMessages);

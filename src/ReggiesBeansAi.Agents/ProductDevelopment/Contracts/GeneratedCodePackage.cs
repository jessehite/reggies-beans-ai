namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record GeneratedCodePackage(
    GeneratedFile[] Files,
    string SolutionStructure);

public sealed record GeneratedFile(
    string Path,
    string Content,
    string FileType);

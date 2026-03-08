namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record GeneratedFrontendPackage(
    GeneratedFile[] BackendFiles,
    GeneratedFile[] FrontendFiles,
    string BackendStructure,
    string FrontendStructure,
    InfraManifest InfraManifest);

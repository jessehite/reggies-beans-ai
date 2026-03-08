namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record FullStackPackage(
    GeneratedFile[] BackendFiles,
    GeneratedFile[] FrontendFiles,
    GeneratedFile[] InfraFiles,
    string BackendStructure,
    string FrontendStructure,
    string DockerComposeOverview);

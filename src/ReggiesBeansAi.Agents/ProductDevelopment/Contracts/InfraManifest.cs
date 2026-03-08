namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record InfraManifest(
    string ProjectFilePath,
    string? DatabaseType,
    string[] ConnectionStrings,
    string[] BackendEnvVars,
    string PackageJsonPath,
    string[] FrontendEnvVars);

namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record DeploymentPackage(
    GeneratedFile[] DeploymentFiles,
    string RollbackProcedure,
    string HealthCheckConfig);

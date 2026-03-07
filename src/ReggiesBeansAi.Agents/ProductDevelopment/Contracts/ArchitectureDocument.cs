namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record ArchitectureDocument(
    SystemComponent[] Components,
    DataEntity[] DataModel,
    ApiEndpoint[] ApiEndpoints,
    string InfrastructureRecommendation,
    string SecurityApproach,
    string CiCdDesign);

public sealed record SystemComponent(
    string Name,
    string Responsibility,
    string[] DependsOn);

public sealed record DataEntity(
    string Name,
    EntityField[] Fields,
    string[] Relationships);

public sealed record EntityField(
    string Name,
    string Type,
    bool Required);

public sealed record ApiEndpoint(
    string Method,
    string Path,
    string Description,
    string RequestBody,
    string ResponseBody);

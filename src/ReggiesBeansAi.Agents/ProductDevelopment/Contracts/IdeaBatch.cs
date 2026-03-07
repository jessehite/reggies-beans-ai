namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record IdeaBatch(ProductIdea[] Ideas);

public sealed record ProductIdea(
    string Title,
    string Description,
    string TargetPersona,
    string ValueProposition,
    string[] Tags);

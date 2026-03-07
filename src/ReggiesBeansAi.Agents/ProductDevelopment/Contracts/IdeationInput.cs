namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record IdeationInput(
    string Domain,
    string TargetAudience,
    string[] SeedThemes,
    string[] RejectedIdeas);

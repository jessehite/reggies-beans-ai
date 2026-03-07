namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record DiscoveryPrompt(
    string AreaOfInterest,
    string[] SignalSources);

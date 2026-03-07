namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record DiscoveredOpportunities(
    TrendSignal[] Trends,
    OpportunityProfile[] Opportunities);

public sealed record TrendSignal(
    string Source,
    string Description,
    string Relevance);

public sealed record OpportunityProfile(
    string Domain,
    string TargetAudience,
    string[] SuggestedThemes,
    string Rationale,
    string[] SupportingTrends);

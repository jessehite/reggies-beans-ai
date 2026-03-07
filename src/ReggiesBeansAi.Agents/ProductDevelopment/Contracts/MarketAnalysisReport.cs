namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record MarketAnalysisReport(IdeaMarketAnalysis[] Analyses);

public sealed record IdeaMarketAnalysis(
    string IdeaTitle,
    string TamEstimate,
    string SamEstimate,
    string SomEstimate,
    Competitor[] Competitors,
    string[] MarketGaps,
    string[] RiskFactors,
    string Recommendation,
    string ConfidenceLevel);

public sealed record Competitor(
    string Name,
    string[] Strengths,
    string[] Weaknesses);

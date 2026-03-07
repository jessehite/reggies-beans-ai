namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record EvaluatedIdeas(ScoredIdea[] Ideas);

public sealed record ScoredIdea(
    string Title,
    int NoveltyScore,
    int FeasibilityScore,
    int MarketPotentialScore,
    int DifferentiationScore,
    int AlignmentScore,
    double CompositeScore,
    string Justification,
    int Rank);

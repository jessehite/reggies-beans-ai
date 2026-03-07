namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record ProductRequirementsDocument(
    string ProductVision,
    UserStory[] UserStories,
    string[] MvpFeatures,
    string[] FutureFeatures,
    SuccessMetric[] SuccessMetrics,
    Milestone[] Milestones,
    string[] Risks);

public sealed record UserStory(
    string Title,
    string Description,
    AcceptanceCriterion[] AcceptanceCriteria);

public sealed record AcceptanceCriterion(
    string Given,
    string When,
    string Then);

public sealed record SuccessMetric(
    string Metric,
    string Target);

public sealed record Milestone(
    string Name,
    string Description,
    string[] Dependencies);

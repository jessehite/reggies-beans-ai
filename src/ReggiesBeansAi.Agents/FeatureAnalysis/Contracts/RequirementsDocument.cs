namespace ReggiesBeansAi.Agents.FeatureAnalysis.Contracts;

public sealed record RequirementsDocument(
    string Summary,
    string[] Goals,
    string[] Constraints,
    string[] AcceptanceCriteria);

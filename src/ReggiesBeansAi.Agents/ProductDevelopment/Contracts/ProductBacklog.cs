namespace ReggiesBeansAi.Agents.ProductDevelopment.Contracts;

public sealed record ProductBacklog(
    Epic[] Epics,
    SprintPlan[] Sprints);

public sealed record Epic(
    string Name,
    string Description,
    BacklogStory[] Stories);

public sealed record BacklogStory(
    string Title,
    string Description,
    int StoryPoints,
    string DefinitionOfDone,
    BacklogTask[] Tasks,
    string[] Dependencies);

public sealed record BacklogTask(
    string Name,
    string Description);

public sealed record SprintPlan(
    int SprintNumber,
    string[] StoryTitles,
    int TotalPoints);

using ReggiesBeansAi.Orchestrator.Model;

namespace ReggiesBeansAi.Orchestrator.Tests.Model;

public class WorkflowDefinitionBuilderTests
{
    private record InputA(string Value);
    private record OutputA(string Value);
    private record OutputB(int Count);

    [Fact]
    public void Build_WithValidStages_ReturnsDefinition()
    {
        var definition = new WorkflowDefinitionBuilder("test-workflow", "Test Workflow")
            .AddStage<InputA, OutputA>("stage-1", "Stage One")
            .AddStage<OutputA, OutputB>("stage-2", "Stage Two", maxAttempts: 3)
            .AddStage<OutputB, string>("stage-3", "Stage Three", requiresHumanInput: true)
            .Build();

        Assert.Equal("test-workflow", definition.Id);
        Assert.Equal("Test Workflow", definition.Name);
        Assert.Equal(3, definition.Stages.Count);

        Assert.Equal("stage-1", definition.Stages[0].Id);
        Assert.Equal(typeof(InputA), definition.Stages[0].InputType);
        Assert.Equal(typeof(OutputA), definition.Stages[0].OutputType);
        Assert.Equal(1, definition.Stages[0].MaxAttempts);
        Assert.False(definition.Stages[0].RequiresHumanInput);

        Assert.Equal(3, definition.Stages[1].MaxAttempts);
        Assert.True(definition.Stages[2].RequiresHumanInput);
    }

    [Fact]
    public void Build_WithNoStages_Throws()
    {
        var builder = new WorkflowDefinitionBuilder("empty", "Empty");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("at least one stage", ex.Message);
    }

    [Fact]
    public void Build_WithDuplicateStageIds_Throws()
    {
        var builder = new WorkflowDefinitionBuilder("dup", "Dup")
            .AddStage<InputA, OutputA>("same-id", "First")
            .AddStage<OutputA, OutputB>("same-id", "Second");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("same-id", ex.Message);
    }

    [Fact]
    public void Build_WithMismatchedTypes_Throws()
    {
        var builder = new WorkflowDefinitionBuilder("mismatch", "Mismatch")
            .AddStage<InputA, OutputA>("stage-1", "First")
            .AddStage<InputA, OutputB>("stage-2", "Second"); // InputA != OutputA

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("OutputA", ex.Message);
        Assert.Contains("InputA", ex.Message);
    }

    [Fact]
    public void Build_SingleStage_Succeeds()
    {
        var definition = new WorkflowDefinitionBuilder("single", "Single")
            .AddStage<InputA, OutputA>("only-stage", "Only Stage")
            .Build();

        Assert.Single(definition.Stages);
        Assert.Equal("only-stage", definition.Stages[0].Id);
    }
}

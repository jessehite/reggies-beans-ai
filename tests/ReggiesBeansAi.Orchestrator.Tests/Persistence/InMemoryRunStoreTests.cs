using ReggiesBeansAi.Orchestrator.Model;
using ReggiesBeansAi.Orchestrator.Persistence;

namespace ReggiesBeansAi.Orchestrator.Tests.Persistence;

public class InMemoryRunStoreTests
{
    private readonly InMemoryRunStore _store = new();

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var run = WorkflowRun.Create("test-wf", ["stage-1", "stage-2"]);
        run.Status = WorkflowStatus.Running;
        run.Stages[0].Status = StageStatus.Completed;
        run.Stages[0].OutputJson = "{\"value\":\"hello\"}";

        await _store.SaveAsync(run, CancellationToken.None);

        var loaded = await _store.LoadAsync(run.RunId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(run.RunId, loaded.RunId);
        Assert.Equal(run.WorkflowId, loaded.WorkflowId);
        Assert.Equal(WorkflowStatus.Running, loaded.Status);
        Assert.Equal(2, loaded.Stages.Count);
        Assert.Equal(StageStatus.Completed, loaded.Stages[0].Status);
        Assert.Equal("{\"value\":\"hello\"}", loaded.Stages[0].OutputJson);
        Assert.Equal(StageStatus.Pending, loaded.Stages[1].Status);
    }

    [Fact]
    public async Task Load_UnknownId_ReturnsNull()
    {
        var result = await _store.LoadAsync("does-not-exist", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task List_ReturnsAllSavedRuns()
    {
        var run1 = WorkflowRun.Create("wf-1", ["s1"]);
        var run2 = WorkflowRun.Create("wf-2", ["s1"]);

        await _store.SaveAsync(run1, CancellationToken.None);
        await _store.SaveAsync(run2, CancellationToken.None);

        var runs = await _store.ListAsync(CancellationToken.None);

        Assert.Equal(2, runs.Count);
        Assert.Contains(runs, r => r.RunId == run1.RunId);
        Assert.Contains(runs, r => r.RunId == run2.RunId);
    }

    [Fact]
    public async Task Save_IsolatesCopies()
    {
        var run = WorkflowRun.Create("wf", ["s1"]);

        await _store.SaveAsync(run, CancellationToken.None);

        // Mutate the original after saving
        run.Status = WorkflowStatus.Failed;
        run.Stages[0].Status = StageStatus.Failed;

        var loaded = await _store.LoadAsync(run.RunId, CancellationToken.None);

        // Stored version should be unaffected
        Assert.NotNull(loaded);
        Assert.Equal(WorkflowStatus.Created, loaded.Status);
        Assert.Equal(StageStatus.Pending, loaded.Stages[0].Status);
    }
}

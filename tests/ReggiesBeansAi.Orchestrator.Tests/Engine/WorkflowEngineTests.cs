using Microsoft.Extensions.Logging.Abstractions;
using ReggiesBeansAi.Orchestrator.Engine;
using ReggiesBeansAi.Orchestrator.Handlers;
using ReggiesBeansAi.Orchestrator.Model;
using ReggiesBeansAi.Orchestrator.Persistence;

namespace ReggiesBeansAi.Orchestrator.Tests.Engine;

public class WorkflowEngineTests
{
    private record StageData(string Value);

    private static WorkflowDefinition BuildThreeStageWorkflow(
        int maxAttempts = 1,
        bool thirdStageHumanInput = false)
    {
        return new WorkflowDefinitionBuilder("test-wf", "Test Workflow")
            .AddStage<StageData, StageData>("stage-1", "Stage 1", maxAttempts: maxAttempts)
            .AddStage<StageData, StageData>("stage-2", "Stage 2", maxAttempts: maxAttempts)
            .AddStage<StageData, StageData>("stage-3", "Stage 3",
                maxAttempts: maxAttempts, requiresHumanInput: thirdStageHumanInput)
            .Build();
    }

    private static WorkflowEngine CreateEngine(
        IRunStore store,
        Dictionary<string, IStageHandler> handlers)
    {
        return new WorkflowEngine(store, handlers, NullLogger<WorkflowEngine>.Instance);
    }

    private const string InitialInput = "{\"value\":\"start\"}";

    [Fact]
    public async Task Start_AllStagesSucceed_RunCompleted()
    {
        var store = new InMemoryRunStore();
        var handler1 = new StubHandler().SucceedsWith("{\"value\":\"from-1\"}");
        var handler2 = new StubHandler().SucceedsWith("{\"value\":\"from-2\"}");
        var handler3 = new StubHandler().SucceedsWith("{\"value\":\"from-3\"}");
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = handler1,
            ["stage-2"] = handler2,
            ["stage-3"] = handler3
        };
        var workflow = BuildThreeStageWorkflow();
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Completed, run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.All(run.Stages, s => Assert.Equal(StageStatus.Completed, s.Status));

        // Verify stage chaining: stage-2 received stage-1's output
        Assert.Equal("{\"value\":\"from-1\"}", handler2.LastInputJson);
        Assert.Equal("{\"value\":\"from-2\"}", handler3.LastInputJson);

        // Verify final output stored
        Assert.Equal("{\"value\":\"from-3\"}", run.Stages[2].OutputJson);
    }

    [Fact]
    public async Task Start_PersistsAfterEveryTransition()
    {
        var store = new RecordingRunStore();
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            ["stage-2"] = new StubHandler().Succeeds(),
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow();
        var engine = CreateEngine(store, handlers);

        await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        // At minimum: Created, Running, then for each stage (Running + Completed), then final Completed
        // = 2 + (2 * 3) + 1 = 9 saves
        Assert.True(store.SaveCount >= 9,
            $"Expected at least 9 saves, got {store.SaveCount}");

        // First save should be Created, second Running, last Completed
        Assert.Equal(WorkflowStatus.Created, store.SavedStatuses[0]);
        Assert.Equal(WorkflowStatus.Running, store.SavedStatuses[1]);
        Assert.Equal(WorkflowStatus.Completed, store.SavedStatuses[^1]);
    }

    [Fact]
    public async Task Start_HandlerFails_NoRetries_RunFailed()
    {
        var store = new InMemoryRunStore();
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            ["stage-2"] = new StubHandler().Fails("stage-2 broke"),
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow(maxAttempts: 1);
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Failed, run.Status);
        Assert.Equal(StageStatus.Completed, run.Stages[0].Status);
        Assert.Equal(StageStatus.Failed, run.Stages[1].Status);
        Assert.Equal("stage-2 broke", run.Stages[1].Error);
        Assert.Equal(StageStatus.Skipped, run.Stages[2].Status);
    }

    [Fact]
    public async Task Start_HandlerFails_RetriesAndSucceeds()
    {
        var store = new InMemoryRunStore();
        var stage2Handler = new StubHandler()
            .Fails("transient error")
            .SucceedsWith("{\"value\":\"recovered\"}");
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            ["stage-2"] = stage2Handler,
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow(maxAttempts: 3);
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Completed, run.Status);
        Assert.Equal(StageStatus.Completed, run.Stages[1].Status);
        Assert.Equal(2, run.Stages[1].AttemptCount);
    }

    [Fact]
    public async Task Start_HandlerFails_ExhaustsRetries()
    {
        var store = new InMemoryRunStore();
        var stage2Handler = new StubHandler()
            .Fails("fail-1")
            .Fails("fail-2")
            .Fails("fail-3");
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            ["stage-2"] = stage2Handler,
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow(maxAttempts: 3);
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Failed, run.Status);
        Assert.Equal(StageStatus.Failed, run.Stages[1].Status);
        Assert.Equal(3, run.Stages[1].AttemptCount);
        Assert.Equal(StageStatus.Skipped, run.Stages[2].Status);
    }

    [Fact]
    public async Task Start_HumanGate_PausesRun()
    {
        var store = new InMemoryRunStore();
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            ["stage-2"] = new StubHandler().Succeeds(),
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow(thirdStageHumanInput: true);
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        Assert.Equal(WorkflowStatus.WaitingForInput, run.Status);
        Assert.Equal(StageStatus.Completed, run.Stages[0].Status);
        Assert.Equal(StageStatus.Completed, run.Stages[1].Status);
        Assert.Equal(StageStatus.Pending, run.Stages[2].Status);
        Assert.NotNull(run.Stages[2].InputJson); // Input is staged for the human
    }

    [Fact]
    public async Task Resume_AfterHumanGate_CompletesRun()
    {
        var store = new InMemoryRunStore();
        var stage3Handler = new StubHandler().SucceedsWith("{\"value\":\"approved\"}");
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            ["stage-2"] = new StubHandler().Succeeds(),
            ["stage-3"] = stage3Handler
        };
        var workflow = BuildThreeStageWorkflow(thirdStageHumanInput: true);
        var engine = CreateEngine(store, handlers);

        var paused = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);
        Assert.Equal(WorkflowStatus.WaitingForInput, paused.Status);

        var humanInput = "{\"value\":\"human-says-yes\"}";
        var completed = await engine.ResumeAsync(workflow, paused.RunId, humanInput, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Completed, completed.Status);
        Assert.Equal(StageStatus.Completed, completed.Stages[2].Status);
        Assert.Equal("{\"value\":\"approved\"}", completed.Stages[2].OutputJson);
        Assert.Equal(humanInput, stage3Handler.LastInputJson);
    }

    [Fact]
    public async Task Resume_WrongStatus_Throws()
    {
        var store = new InMemoryRunStore();
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            ["stage-2"] = new StubHandler().Succeeds(),
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow();
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);
        Assert.Equal(WorkflowStatus.Completed, run.Status);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ResumeAsync(workflow, run.RunId, "{}", CancellationToken.None));
    }

    [Fact]
    public async Task Start_MissingHandler_RunFailed()
    {
        var store = new InMemoryRunStore();
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = new StubHandler().Succeeds(),
            // stage-2 intentionally missing
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow();
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Failed, run.Status);
        Assert.Equal(StageStatus.Failed, run.Stages[1].Status);
        Assert.Contains("stage-2", run.Stages[1].Error);
        Assert.Equal(StageStatus.Skipped, run.Stages[2].Status);
    }

    [Fact]
    public async Task Start_FirstStageReceivesInitialInput()
    {
        var store = new InMemoryRunStore();
        var stage1Handler = new StubHandler().Succeeds();
        var handlers = new Dictionary<string, IStageHandler>
        {
            ["stage-1"] = stage1Handler,
            ["stage-2"] = new StubHandler().Succeeds(),
            ["stage-3"] = new StubHandler().Succeeds()
        };
        var workflow = BuildThreeStageWorkflow();
        var engine = CreateEngine(store, handlers);

        var run = await engine.StartAsync(workflow, InitialInput, CancellationToken.None);

        Assert.Equal(InitialInput, stage1Handler.LastInputJson);
        Assert.Equal(InitialInput, run.Stages[0].InputJson);
    }
}

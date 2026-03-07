namespace ReggiesBeansAi.Orchestrator.Model;

public sealed class WorkflowDefinitionBuilder
{
    private readonly string _id;
    private readonly string _name;
    private readonly List<StageDefinition> _stages = new();

    public WorkflowDefinitionBuilder(string id, string name)
    {
        _id = id;
        _name = name;
    }

    public WorkflowDefinitionBuilder AddStage<TInput, TOutput>(
        string id,
        string name,
        int maxAttempts = 1,
        int retryDelaySeconds = 0,
        bool requiresHumanInput = false)
    {
        _stages.Add(new StageDefinition
        {
            Id = id,
            Name = name,
            InputType = typeof(TInput),
            OutputType = typeof(TOutput),
            MaxAttempts = maxAttempts,
            RetryDelaySeconds = retryDelaySeconds,
            RequiresHumanInput = requiresHumanInput
        });

        return this;
    }

    public WorkflowDefinition Build()
    {
        if (_stages.Count == 0)
            throw new InvalidOperationException("Workflow must have at least one stage.");

        var seenIds = new HashSet<string>();
        foreach (var stage in _stages)
        {
            if (!seenIds.Add(stage.Id))
                throw new InvalidOperationException($"Duplicate stage ID: '{stage.Id}'.");
        }

        for (int i = 1; i < _stages.Count; i++)
        {
            var previousOutput = _stages[i - 1].OutputType;
            var currentInput = _stages[i].InputType;
            if (previousOutput != currentInput)
            {
                throw new InvalidOperationException(
                    $"Type mismatch between stage '{_stages[i - 1].Id}' output ({previousOutput.Name}) " +
                    $"and stage '{_stages[i].Id}' input ({currentInput.Name}).");
            }
        }

        return new WorkflowDefinition
        {
            Id = _id,
            Name = _name,
            Stages = _stages.ToList().AsReadOnly()
        };
    }
}

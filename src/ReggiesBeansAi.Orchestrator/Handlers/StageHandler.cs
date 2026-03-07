using System.Text.Json;

namespace ReggiesBeansAi.Orchestrator.Handlers;

public abstract class StageHandler<TInput, TOutput> : IStageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<StageHandlerResult> ExecuteAsync(
        string inputJson,
        StageContext context,
        CancellationToken cancellationToken)
    {
        TInput? input;
        try
        {
            input = JsonSerializer.Deserialize<TInput>(inputJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return StageHandlerResult.Failed($"Failed to deserialize input: {ex.Message}");
        }

        if (input is null)
            return StageHandlerResult.Failed("Deserialized input was null.");

        var result = await HandleAsync(input, context, cancellationToken);

        if (result.Success)
        {
            var outputJson = JsonSerializer.Serialize(result.Output, JsonOptions);
            return StageHandlerResult.Succeeded(outputJson);
        }

        return StageHandlerResult.Failed(result.Error!);
    }

    protected abstract Task<HandleResult<TOutput>> HandleAsync(
        TInput input,
        StageContext context,
        CancellationToken cancellationToken);
}

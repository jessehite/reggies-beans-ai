using System.Text.Json;
using System.Text.Json.Serialization;
using ReggiesBeansAi.Orchestrator.Model;
using ReggiesBeansAi.Orchestrator.Persistence;

namespace ReggiesBeansAi.Web;

/// <summary>Persists workflow runs as JSON files in a local directory.</summary>
public sealed class JsonFileRunStore : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _runsDirectory;

    public JsonFileRunStore(string runsDirectory)
    {
        _runsDirectory = runsDirectory;
        Directory.CreateDirectory(runsDirectory);
    }

    public async Task SaveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        var path = RunPath(run.RunId);
        var json = JsonSerializer.Serialize(run, JsonOptions);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        // Use FileShare.ReadWrite so concurrent GET requests don't cause sharing violations on Windows.
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await fs.WriteAsync(bytes, cancellationToken);
    }

    public async Task<WorkflowRun?> LoadAsync(string runId, CancellationToken cancellationToken)
    {
        var path = RunPath(runId);
        if (!File.Exists(path)) return null;
        return await ReadRunAsync(path, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowRun>> ListAsync(CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(_runsDirectory, "*.json");
        var runs = new List<WorkflowRun>(files.Length);

        foreach (var file in files)
        {
            var run = await ReadRunAsync(file, cancellationToken);
            if (run is not null)
                runs.Add(run);
        }

        return runs.OrderByDescending(r => r.CreatedAt).ToList();
    }

    private static async Task<WorkflowRun?> ReadRunAsync(string path, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);
        var json = await reader.ReadToEndAsync(cancellationToken);
        return JsonSerializer.Deserialize<WorkflowRun>(json, JsonOptions);
    }

    private string RunPath(string runId) => Path.Combine(_runsDirectory, $"{runId}.json");
}

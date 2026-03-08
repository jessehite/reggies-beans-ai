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
        CleanUpStaleTempFiles();
    }

    public async Task SaveAsync(WorkflowRun run, CancellationToken cancellationToken)
    {
        var finalPath = RunPath(run.RunId);
        var tempPath = Path.Combine(_runsDirectory, $"{run.RunId}.{Path.GetRandomFileName()}.tmp");

        var json = JsonSerializer.Serialize(run, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, finalPath, overwrite: true);
    }

    public async Task<WorkflowRun?> LoadAsync(string runId, CancellationToken cancellationToken)
    {
        var path = RunPath(runId);
        if (!File.Exists(path)) return null;

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<WorkflowRun>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<WorkflowRun>> ListAsync(CancellationToken cancellationToken)
    {
        var files = Directory.GetFiles(_runsDirectory, "*.json");
        var runs = new List<WorkflowRun>(files.Length);

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var run = JsonSerializer.Deserialize<WorkflowRun>(json, JsonOptions);
            if (run is not null)
                runs.Add(run);
        }

        return runs.OrderByDescending(r => r.CreatedAt).ToList();
    }

    private void CleanUpStaleTempFiles()
    {
        foreach (var file in Directory.GetFiles(_runsDirectory, "*.tmp"))
        {
            try { File.Delete(file); }
            catch { /* best effort */ }
        }
    }

    private string RunPath(string runId) => Path.Combine(_runsDirectory, $"{runId}.json");
}

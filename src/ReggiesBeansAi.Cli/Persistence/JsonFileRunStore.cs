using System.Text.Json;
using ReggiesBeansAi.Orchestrator.Model;
using ReggiesBeansAi.Orchestrator.Persistence;

namespace ReggiesBeansAi.Cli.Persistence;

public sealed class JsonFileRunStore : IRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
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
        // Unique temp name per write — no conflict if a prior write for this run was interrupted
        var tempPath = Path.Combine(_runsDirectory, $"{run.RunId}.{Path.GetRandomFileName()}.tmp");

        var json = JsonSerializer.Serialize(run, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        // Atomic replace: prevents partial writes on crash
        File.Move(tempPath, finalPath, overwrite: true);
    }

    private void CleanUpStaleTempFiles()
    {
        foreach (var file in Directory.GetFiles(_runsDirectory, "*.tmp"))
        {
            try { File.Delete(file); }
            catch { /* best effort — don't fail startup over a leftover temp file */ }
        }
    }

    public async Task<WorkflowRun?> LoadAsync(string runId, CancellationToken cancellationToken)
    {
        var path = RunPath(runId);
        if (!File.Exists(path))
            return null;

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

        return runs;
    }

    private string RunPath(string runId) =>
        Path.Combine(_runsDirectory, $"{runId}.json");
}

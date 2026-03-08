using System.Text.Json;
using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Cli.Handlers;

public sealed class FullStackReviewHandler : StageHandler<GeneratedFrontendPackage, GeneratedFrontendPackage>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected override Task<HandleResult<GeneratedFrontendPackage>> HandleAsync(
        GeneratedFrontendPackage input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  FULL-STACK REVIEW — Human Gate");
        Console.WriteLine("═══════════════════════════════════════════");

        // Extract backend files
        var backendDir = Path.Combine("output", context.RunId, "backend");
        ExtractFiles(input.BackendFiles, backendDir);
        Console.WriteLine($"\n  Backend (.NET API) extracted to: {backendDir}");
        Console.WriteLine($"    {input.BackendFiles.Length} files");
        Console.WriteLine($"    {input.BackendStructure}");

        // Extract frontend files
        var frontendDir = Path.Combine("output", context.RunId, "frontend");
        ExtractFiles(input.FrontendFiles, frontendDir);
        Console.WriteLine($"\n  Frontend (React Native + Expo) extracted to: {frontendDir}");
        Console.WriteLine($"    {input.FrontendFiles.Length} files");
        Console.WriteLine($"    {input.FrontendStructure}");

        Console.WriteLine();
        Console.WriteLine("  To run the mobile app:");
        Console.WriteLine($"    cd {frontendDir}");
        Console.WriteLine("    npm install");
        Console.WriteLine("    npx expo start");
        Console.WriteLine("  Then scan the QR code with Expo Go on your phone.");
        Console.WriteLine();
        Console.WriteLine("  Press Enter to continue to automated testing, or Ctrl+C to stop.");
        Console.ReadLine();

        return Task.FromResult(HandleResult<GeneratedFrontendPackage>.Succeeded(input));
    }

    private static void ExtractFiles(GeneratedFile[] files, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        foreach (var file in files)
        {
            var fullPath = Path.Combine(outputDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Content);
        }
    }
}

using ReggiesBeansAi.Agents.ProductDevelopment.Contracts;
using ReggiesBeansAi.Orchestrator.Handlers;

namespace ReggiesBeansAi.Cli.Handlers;

public sealed class OpportunitySelectionHandler : StageHandler<DiscoveredOpportunities, IdeationInput>
{
    protected override Task<HandleResult<IdeationInput>> HandleAsync(
        DiscoveredOpportunities input,
        StageContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine("  TREND DISCOVERY RESULTS");
        Console.WriteLine("═══════════════════════════════════════════");
        Console.WriteLine();

        Console.WriteLine("Trends detected:");
        for (int i = 0; i < input.Trends.Length; i++)
        {
            var t = input.Trends[i];
            Console.WriteLine($"  [{i + 1}] ({t.Source}) {t.Description}");
            Console.WriteLine($"      Relevance: {t.Relevance}");
        }

        Console.WriteLine();
        Console.WriteLine("───────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine("Opportunity profiles:");

        for (int i = 0; i < input.Opportunities.Length; i++)
        {
            var o = input.Opportunities[i];
            Console.WriteLine($"  [{i + 1}] {o.Domain}");
            Console.WriteLine($"      Audience: {o.TargetAudience}");
            Console.WriteLine($"      Themes:   {string.Join(", ", o.SuggestedThemes)}");
            Console.WriteLine($"      Why now:   {o.Rationale}");
            Console.WriteLine($"      Evidence:  {string.Join(", ", o.SupportingTrends)}");
            Console.WriteLine();
        }

        Console.WriteLine("───────────────────────────────────────────");
        Console.WriteLine();
        Console.Write($"Select an opportunity (1-{input.Opportunities.Length}), or 'C' for custom: ");
        var choice = Console.ReadLine()?.Trim();

        IdeationInput ideationInput;

        if (int.TryParse(choice, out var index)
            && index >= 1
            && index <= input.Opportunities.Length)
        {
            var selected = input.Opportunities[index - 1];

            Console.WriteLine($"Selected: {selected.Domain}");
            Console.WriteLine();

            // Let the human refine the selection
            Console.Write($"Domain [{selected.Domain}]: ");
            var domain = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(domain)) domain = selected.Domain;

            Console.Write($"Target audience [{selected.TargetAudience}]: ");
            var audience = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(audience)) audience = selected.TargetAudience;

            Console.Write($"Seed themes [{string.Join(", ", selected.SuggestedThemes)}]: ");
            var themesInput = Console.ReadLine()?.Trim();
            var themes = string.IsNullOrEmpty(themesInput)
                ? selected.SuggestedThemes
                : themesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Console.Write("Rejected ideas to avoid (comma-separated, or leave blank): ");
            var rejectedInput = Console.ReadLine() ?? string.Empty;
            var rejected = rejectedInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            ideationInput = new IdeationInput(domain, audience, themes, rejected);
        }
        else
        {
            Console.WriteLine("Custom input:");
            Console.WriteLine();

            Console.Write("Domain/industry: ");
            var domain = Console.ReadLine() ?? string.Empty;

            Console.Write("Target audience: ");
            var audience = Console.ReadLine() ?? string.Empty;

            Console.Write("Seed themes (comma-separated): ");
            var themesInput = Console.ReadLine() ?? string.Empty;
            var themes = themesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Console.Write("Rejected ideas to avoid (comma-separated, or leave blank): ");
            var rejectedInput = Console.ReadLine() ?? string.Empty;
            var rejected = rejectedInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            ideationInput = new IdeationInput(domain, audience, themes, rejected);
        }

        Console.WriteLine();

        return Task.FromResult(HandleResult<IdeationInput>.Succeeded(ideationInput));
    }
}

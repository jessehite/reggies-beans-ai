namespace ReggiesBeansAi.Agents.Llm;

internal static class LlmResponseParser
{
    internal static string StripMarkdownFences(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            // Search for a fence on its own line (\n```) to avoid matching ``` embedded in content strings
            var lastFence = trimmed.LastIndexOf("\n```");
            if (firstNewline > 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }
        return trimmed;
    }
}

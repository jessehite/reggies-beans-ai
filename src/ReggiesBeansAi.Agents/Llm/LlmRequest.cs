namespace ReggiesBeansAi.Agents.Llm;

public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    string Model = "claude-sonnet-4-6",
    int MaxTokens = 8192);

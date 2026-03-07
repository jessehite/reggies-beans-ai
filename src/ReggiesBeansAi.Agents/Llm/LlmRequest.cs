namespace ReggiesBeansAi.Agents.Llm;

public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt,
    string Model = "claude-opus-4-5-20251101",
    int MaxTokens = 8192);

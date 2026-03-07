namespace ReggiesBeansAi.Agents.Llm;

public sealed record LlmResponse(
    string Content,
    int InputTokens,
    int OutputTokens);

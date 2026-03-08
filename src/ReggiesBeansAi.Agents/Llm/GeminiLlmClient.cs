using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReggiesBeansAi.Agents.Llm;

/// <summary>
/// ILlmClient implementation for Google's Gemini API with Google Search grounding enabled.
/// Ignores LlmRequest.Model — always uses the model configured at construction time,
/// since model names are provider-specific.
/// </summary>
public sealed class GeminiLlmClient : ILlmClient
{
    private const string ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiLlmClient(HttpClient httpClient, string apiKey, string model = "gemini-3.1-pro-preview")
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var url = $"{ApiBaseUrl}/{_model}:generateContent?key={_apiKey}";

        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = request.SystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = request.UserPrompt } }
                }
            },
            tools = new[]
            {
                new { google_search = new { } }
            },
            generationConfig = new
            {
                maxOutputTokens = request.MaxTokens
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(url, body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Gemini API returned {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var node = JsonNode.Parse(json)!;

        // Extract text from the first candidate's content parts
        var parts = node["candidates"]![0]!["content"]!["parts"]!.AsArray();
        var content = string.Join("", parts.Select(p => p!["text"]?.GetValue<string>() ?? ""));

        var usage = node["usageMetadata"];
        var inputTokens = usage?["promptTokenCount"]?.GetValue<int>() ?? 0;
        var outputTokens = usage?["candidatesTokenCount"]?.GetValue<int>() ?? 0;

        return new LlmResponse(content, inputTokens, outputTokens);
    }
}

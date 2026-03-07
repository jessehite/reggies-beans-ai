using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReggiesBeansAi.Agents.Llm;

public sealed class ClaudeLlmClient : ILlmClient
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;

    public ClaudeLlmClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = request.Model,
            max_tokens = request.MaxTokens,
            system = request.SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = request.UserPrompt }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(ApiUrl, body, JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Anthropic API returned {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var node = JsonNode.Parse(json)!;

        var content = node["content"]![0]!["text"]!.GetValue<string>();
        var inputTokens = node["usage"]!["input_tokens"]!.GetValue<int>();
        var outputTokens = node["usage"]!["output_tokens"]!.GetValue<int>();

        return new LlmResponse(content, inputTokens, outputTokens);
    }
}

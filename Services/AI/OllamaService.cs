using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace UniCP.Services.AI;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _modelName;

    public OllamaService(IConfiguration configuration)
    {
        _baseUrl = configuration["AI:OllamaUrl"] ?? "http://localhost:11434/api/generate";
        _modelName = configuration["AI:ModelName"] ?? "llama3";
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2); // AI can be slow
    }

    public async Task<AIResponse> GenerateResponseAsync(string systemPrompt, string userMessage, string contextData)
    {
        // 1. Construct the full prompt (Llama 3 Instruct Format or generic)
        // For Llama 3, it's better to use chat format but /api/generate takes a single prompt string unless we use /api/chat.
        // We will use /api/generate with a constructed prompt for simplicity or /api/chat if strict role adherence is needed.
        // Let's use /api/generate with a raw string for now, formatted clearly.
        
        var fullPrompt = $@"System: {systemPrompt}

Context Data:
{contextData}

User: {userMessage}

Assistant: (Respond in JSON)";

        var requestBody = new
        {
            model = _modelName,
            prompt = fullPrompt,
            stream = false,
            // strict JSON enforcing if supported or via prompt engineering
            req_json = true // Ollama supports format: "json" in newer versions
        };

        // For newer Ollama versions, we can send "format": "json"
        var jsonContent = new StringContent(JsonSerializer.Serialize(new { 
            model = _modelName, 
            prompt = fullPrompt, 
            stream = false,
            format = "json" 
        }), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_baseUrl, jsonContent);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var ollamaResult = JsonSerializer.Deserialize<OllamaApiResult>(responseString);

            if (!string.IsNullOrEmpty(ollamaResult?.Response))
            {
                try
                {
                    var cleanResponse = ollamaResult.Response.Trim();
                    // Remove markdown code blocks if present
                    if (cleanResponse.StartsWith("```"))
                    {
                        var firstLineEnd = cleanResponse.IndexOf('\n');
                        if (firstLineEnd > 0) 
                        {
                            cleanResponse = cleanResponse.Substring(firstLineEnd + 1);
                        }
                        var lastBacktick = cleanResponse.LastIndexOf("```");
                        if (lastBacktick > 0)
                        {
                            cleanResponse = cleanResponse.Substring(0, lastBacktick);
                        }
                    }
                    cleanResponse = cleanResponse.Trim();

                    var parsedResponse = JsonSerializer.Deserialize<AIResponse>(cleanResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsedResponse != null)
                    {
                        parsedResponse.PromptTokens = ollamaResult.PromptEvalCount;
                        parsedResponse.CompletionTokens = ollamaResult.EvalCount;
                        return parsedResponse;
                    }
                }
                catch
                {
                    // Fallback if LLM output invalid JSON
                    return new AIResponse 
                    { 
                        Text = ollamaResult.Response,
                        PromptTokens = ollamaResult.PromptEvalCount,
                        CompletionTokens = ollamaResult.EvalCount
                    };
                }
            }
        }
        catch (Exception ex)
        {
            return new AIResponse { Text = $"AI Hatası: {ex.Message}" };
        }

        return new AIResponse { Text = "Yanıt alınamadı." };
    }
}

public class AIResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    [JsonIgnore]
    public int PromptTokens { get; set; }

    [JsonIgnore]
    public int CompletionTokens { get; set; }
}

internal class OllamaApiResult
{
    [JsonPropertyName("response")]
    public string Response { get; set; }
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    
    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
    
    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}

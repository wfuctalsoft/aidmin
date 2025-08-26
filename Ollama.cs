using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class OllamaClient
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    protected List<ChatMessage> _chatHistory = new List<ChatMessage>();

    public OllamaClient(string baseUrl = "http://localhost:11434", string role = "",OllamaClient shared = null)
    {
        _client = new HttpClient();
        _baseUrl = baseUrl;
        if (!string.IsNullOrEmpty(role)) _chatHistory.Add(new ChatMessage() { Role = "system", Content = role });
        if (shared != null) _chatHistory = shared._chatHistory;
    }

    public IReadOnlyList<ChatMessage> ChatHistory => _chatHistory.AsReadOnly();

    public void ResetChat() => _chatHistory.Clear();

    public async Task<string> ChatAsync(
        string message,
        string model,
        double temperature = 0.7,
        int maxTokens = 512)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _chatHistory.Add(new ChatMessage { Role = "user", Content = message });
        }

        var request = new ChatRequest
        {
            Model = model,
            Messages = _chatHistory,
            Options = new GenerationOptions
            {
                Temperature = temperature,
                NumPredict = maxTokens
            }
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        using (var response = await _client.PostAsync($"{_baseUrl}/api/chat", content))
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Ошибка API: {response.StatusCode}\n{errorContent}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ChatResponse>(jsonResponse);

            if (result?.Message == null) return string.Empty;

            _chatHistory.Add(result.Message);
            return result.Message.Content.Trim();
        }
    }

    public void SaveHistory(string dialogName) => File.WriteAllText(dialogName + ".json", JsonSerializer.Serialize(_chatHistory));
    public void LoadHistory(string dialogName) => _chatHistory.AddRange(JsonSerializer.Deserialize<List<ChatMessage>>(File.ReadAllText(dialogName + ".json")));

    public class ChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

        [JsonPropertyName("options")]
        public GenerationOptions Options { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream => false;
        public ChatRequest() { }
    }

    public class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
        public ChatMessage() { }
    }

    public class GenerationOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("num_predict")]
        public int NumPredict { get; set; }
        public GenerationOptions() { }
    }

    public class ChatResponse
    {
        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; }
        public ChatResponse() { }
    }

    public async Task<List<OllamaModel>> GetAvailableModelsAsync()
    {
        var response = await _client.GetAsync($"{_baseUrl}/api/tags");
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Ошибка: {response.StatusCode}\n{errorContent}");
        }
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OllamaModelsResponse>(json);
        return result?.Models ?? new List<OllamaModel>();
    }

    public class OllamaModelsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; }

        public OllamaModelsResponse() { }
    }

    public class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("modified_at")]
        public DateTime ModifiedAt { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; }
        public OllamaModel() { }
    }
}
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniCP.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent?key=";

        public GeminiService(IConfiguration configuration)
        {
            _apiKey = configuration["Gemini:ApiKey"];
            _httpClient = new HttpClient();
        }

        public async Task<GeminiResponse> GenerateResponseAsync(string userMessage, string contextData)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return new GeminiResponse 
                { 
                    Text = "API Anahtarı bulunamadı. Lütfen appsettings.json dosyasında Gemini:ApiKey alanını doldurun.",
                    Action = null
                };
            }

            var systemPrompt = @"
            Role: You are 'Univera Asistan', a helpful and intelligent AI assistant for the Univera Connect portal.
            
            Context:
            You will receive specific data for the logged-in merchant in the 'Context Data' section:
            1. **Financial Summary**: Monthly and yearly totals.
            2. **Support Tickets**: Number of open tickets and status breakdown.
            3. **Last 5 Orders**: Specific details of recent orders.

            Instructions:
            - USE this data to answer questions directly.
            - If asked about ""open tickets"" or ""status"", refer to the Support Tickets section.
            - If asked about ""last order"" or ""recent activity"", refer to the Last 5 Orders section.
            - Do NOT make up numbers. Only use what is provided.
            - Always respond in Turkish.
            - Be polite, concise, and professional.

            Capabilities:
            1. Navigation: Helps users navigate to specific pages.
               - /Finans/Index (Keywords: Finans, Bütçe, Para, Fatura)
               - /N4B/Index (Keywords: Destek, Talep, Yardım, Ticket, Sorun)
               - /Musteri/Index (Keywords: Ana sayfa, Dashboard, Özet)
               - /Talepler/Create (Keywords: Yeni talep, Talep aç, Sorun bildir)
               - /Account/ChangePassword (Keywords: Şifre değiştir, Parola yenile)
            
            2. Actions: Can trigger specific actions.
               - Download Statement: Triggers a PDF download.
                 - Default (Monthly): /Finans/DownloadStatement?filter=month
                 - 3 Months: /Finans/DownloadStatement?filter=3months
                 - Annual: /Finans/DownloadStatement?filter=year

            OUTPUT FORMAT: You MUST strictly output a valid JSON object. Do not include markdown formatting (```json ... ```).
            - The JSON structure:
              {
                ""text"": ""Your friendly response to the user"",
                ""action"": ""navigate"" OR ""download"" OR null,
                ""payload"": ""The URL"" OR null
              }
            
            Example 1:
            User: 'Finans sayfasına git'
            JSON: { ""text"": ""Sizi finans sayfasına yönlendiriyorum."", ""action"": ""navigate"", ""payload"": ""/Finans/Index"" }

            Example 2:
            User: 'Açık talebim var mı?' (Context says: 2 adet açık destek talebiniz bulunuyor)
            JSON: { ""text"": ""Evet, şu anda sistemde işlem gören 2 adet açık destek talebiniz bulunmaktadır."", ""action"": ""navigate"", ""payload"": ""/N4B/Index"" }

            Example 3:
            User: 'En son ne sipariş verdim?' (Context lists orders)
            JSON: { ""text"": ""En son 15.01.2025 tarihinde 15.000 TL tutarında bir siparişiniz (No: 12345) görünmektedir."", ""action"": null, ""payload"": null }

            Example 4:
            User: 'Bu son siparişin detayını göster' (Context: Last Order ID is 12345)
            JSON: { ""text"": ""Sipariş detaylarını açıyorum..."", ""action"": ""navigate"", ""payload"": ""/Finans/Index?openOrder=12345"" }
            ";

            var fullPrompt = $"{systemPrompt}\n\nCONTEXT DATA:\n{contextData}\n\nUser: {userMessage}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = fullPrompt }
                        }
                    }
                }
            };

            var jsonContent = new StringContent( JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try 
            {
                var response = await _httpClient.PostAsync(ApiUrl + _apiKey, jsonContent);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var geminiResult = JsonSerializer.Deserialize<GeminiApiResult>(responseString);

                var rawText = geminiResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

                if (!string.IsNullOrEmpty(rawText))
                {
                    // Clean up markdown block if present
                    rawText = rawText.Replace("```json", "").Replace("```", "").Trim();
                    
                    try 
                    {
                        var parsedResponse = JsonSerializer.Deserialize<GeminiResponse>(rawText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        return parsedResponse ?? new GeminiResponse { Text = "Yanıt ayrıştırılamadı.", Action = null };
                    }
                    catch
                    {
                        // Fallback if JSON parsing fails (hallucination precaution)
                        return new GeminiResponse { Text = rawText, Action = null }; 
                    }
                }
            }
            catch (Exception ex)
            {
                return new GeminiResponse { Text = "Bir hata oluştu: " + ex.Message, Action = null };
            }

            return new GeminiResponse { Text = "Yanıt alınamadı.", Action = null };
        }
    }

    // Helper classes for JSON Serialization
    public class GeminiResponse
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
        
        [JsonPropertyName("action")]
        public string? Action { get; set; }
        
        [JsonPropertyName("payload")]
        public string? Payload { get; set; }
    }

    class GeminiApiResult
    {
        [JsonPropertyName("candidates")]
        public List<Candidate> Candidates { get; set; }
    }

    class Candidate
    {
        [JsonPropertyName("content")]
        public Content Content { get; set; }
    }

    class Content
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; }
    }

    class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}

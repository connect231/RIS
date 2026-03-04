using Microsoft.EntityFrameworkCore;
using UniCP.Models;
using UniCP.Models.AI;

namespace UniCP.Services.AI;

public class AIService
{
    private readonly OllamaService _ollamaService;
    private readonly DataContext _context;
    // Cost per 1000 tokens (example: $0.00) - Self hosted is "free" but we might charge users "credits"
    private const decimal COST_PER_1K_TOKENS = 0.50m; // Example: 0.50 Credits per 1k tokens

    public AIService(OllamaService ollamaService, DataContext context)
    {
        _ollamaService = ollamaService;
        _context = context;
    }

    public async Task<AIResponse> ProcessRequestAsync(int userId, string userMessage, string contextData)
    {
        // 1. Check Balance
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return new AIResponse { Text = "Kullanıcı bulunamadı." };

        // Simple check: User needs > 0 credits to start (or allow negative if post-paid)
        if (user.TokenBalance <= 0)
        {
             return new AIResponse { Text = "Yetersiz kredi. Lütfen bakiyenizi yükleyin.", Action = "navigate", Payload = "/Finans/Credits" };
        }

        // 2. Call AI
        var systemPrompt = @"
            Role: You are 'Univera Asistan', a helpful AI assistant for the Univera Connect portal.
            
            Background Data (DO NOT READ OUT LOUD UNLESS ASKED):
            {contextData}

            Instructions:
            1. Answer the user's question naturally and concisely in Turkish.
            2. NEVER summarize the 'Background Data' unless the user specifically asks for a summary (e.g., 'Durumum ne?', 'Özet geç').
            3. If the user says 'Merhaba' (Hello), just say 'Merhaba! Size nasıl yardımcı olabilirim?' and stop.
            4. Do NOT include raw data or JSON in the 'text' field.
            5. ONLY use the specific URLs provided in the 'Special Rules'. DO NOT invent or hallunicate new paths like '/Download/...'.
            6. Only use 'action' if the user explicitly wants to navigate or download something.

            Examples:
            User: Merhaba
            Assistant: { ""text"": ""Merhaba! Size nasıl yardımcı olabilirim?"" }

            User: Son siparişim ne kadar?
            Assistant: { ""text"": ""Son siparişiniz 12.01.2026 tarihinde 366.278,90 TL tutarındadır."" }

            Output JSON Format:
            { 
                ""text"": ""Your conversational answer here"", 
                ""action"": ""navigate"" | ""download"" | null, 
                ""payload"": ""url_string"" | { object } 
            }

            Special Rules for Actions:
            - If user asks to see/open order details (e.g. ""Sipariş detayını aç"", ""Aç""), use action: ""openOrder"" and payload: ""{OrderId}"".
            - If user asks for Dashboard/Home (e.g. ""Dashboard'ı aç"", ""Ana sayfa""), use action: ""navigate"" and payload: ""/Musteri/Index"".
            - If user refers to ""this order"", ""it"" or ""that"" (e.g. ""Bu siparişi göster"", ""Bunu aç"", ""Detayını ver"") without an ID, assume they mean the **most recent order** listed in ""Last 5 Orders"".
            - If user asks to download statement/report/excel (e.g. ""Ekstre indir"", ""Excel al"", ""3 aylık rapor""), use action: ""download"".
              - Payload should be: ""/Finans/ExportExcel?filter={FILTER}""
              - Filters: ""month"" (default), ""3months"", ""year"".
            - Find the relevant {OrderId} from the Context Data.

            Examples:
            User: Merhaba
            Assistant: { ""text"": ""Merhaba! Size nasıl yardımcı olabilirim?"" }

            User: Son siparişim ne kadar?
            Assistant: { ""text"": ""Son siparişiniz 12.01.2026 tarihinde 366.278,90 TL tutarındadır."" }

            User: Bu siparişi göster
            Assistant: { ""text"": ""Son siparişinizin detaylarını açıyorum."", ""action"": ""openOrder"", ""payload"": ""ORD-2024-001"" }

            User: 3 aylık ekstre indir
            Assistant: { ""text"": ""3 aylık ekstrenizi Excel formatında indiriyorum."", ""action"": ""download"", ""payload"": ""/Finans/ExportExcel?filter=3months"" }
        ";

        var response = await _ollamaService.GenerateResponseAsync(systemPrompt, userMessage, contextData);

        // 3. Calculate Cost
        // Simple formula: (Input + Output) / 1000 * Rate
        var totalTokens = response.PromptTokens + response.CompletionTokens;
        var cost = (totalTokens / 1000m) * COST_PER_1K_TOKENS;

        // Ensure minimum cost for interaction?
        if (cost == 0 && totalTokens > 0) cost = 0.01m; 

        // 4. Deduct & Log
        user.TokenBalance -= cost;
        
        var log = new AIServiceLog
        {
            UserId = userId,
            PromptSnippet = userMessage.Length > 100 ? userMessage.Substring(0, 100) + "..." : userMessage,
            PromptTokens = response.PromptTokens,
            CompletionTokens = response.CompletionTokens,
            Cost = cost,
            ModelName = "llama3", // Should come from config
            Timestamp = DateTime.UtcNow
        };

        _context.AIServiceLogs.Add(log);
        await _context.SaveChangesAsync();

        return response;
    }
}

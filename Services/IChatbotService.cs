using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IChatbotService
{
    Task<ChatbotMessageResponse> ProcessMessageAsync(string message, int? userId = null, string? sessionId = null);
    List<ChatbotQuickAction> GetSuggestedPrompts();
    Task<ChatbotConfigDto> GetConfigAsync();
    Task<ChatbotConfigDto> UpdateConfigAsync(UpdateChatbotConfigDto updateDto);
    Task<TestConnectionResponseDto> TestGeminiConnectionAsync(string? testApiKey = null, string? testModel = null);
    Task<List<string>> GetAvailableModelsAsync();
}

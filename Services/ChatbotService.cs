using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class ChatbotService : IChatbotService
{
    private readonly AppDbContext context;
    private readonly IConfiguration configuration;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<ChatbotService> logger;
    private readonly IDataProtector apiKeyProtector;
    private readonly string persistentSettingsPath;

    private static readonly ConcurrentDictionary<string, string> dynamicApiKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> dynamicModels = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object settingsLoadLock = new();
    private static readonly SemaphoreSlim settingsWriteLock = new(1, 1);
    private static string? dynamicProvider;
    private static string? dynamicAccountId;
    private static bool dynamicIsEnabled = true;
    private static bool persistentSettingsLoaded;

    public ChatbotService(
        AppDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<ChatbotService> logger,
        IDataProtectionProvider dataProtectionProvider)
    {
        this.context = context;
        this.configuration = configuration;
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
        this.logger = logger;
        apiKeyProtector = dataProtectionProvider.CreateProtector("Medika.Chatbot.ApiKeys.v1");
        persistentSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MedikaClinic",
            "chatbot-settings.json");
        LoadPersistentSettings();
    }

    private void LoadPersistentSettings()
    {
        lock (settingsLoadLock)
        {
            if (persistentSettingsLoaded) return;
            if (!File.Exists(persistentSettingsPath))
            {
                persistentSettingsLoaded = true;
                return;
            }

            try
            {
                var json = File.ReadAllText(persistentSettingsPath);
                var settings = JsonSerializer.Deserialize<PersistedChatbotSettings>(json)
                    ?? throw new InvalidDataException("Tệp cấu hình chatbot rỗng hoặc không hợp lệ.");
                if (settings.ProtectedApiKeys == null || settings.Models == null)
                    throw new InvalidDataException("Tệp cấu hình chatbot thiếu dữ liệu cần thiết.");

                foreach (var (provider, protectedKey) in settings.ProtectedApiKeys)
                    dynamicApiKeys[provider] = apiKeyProtector.Unprotect(protectedKey);
                foreach (var (provider, model) in settings.Models)
                    dynamicModels[provider] = model;

                dynamicProvider = settings.Provider;
                dynamicAccountId = settings.AccountId;
                dynamicIsEnabled = settings.IsEnabled;
                persistentSettingsLoaded = true;
            }
            catch (Exception exception) when (exception is IOException or JsonException or CryptographicException or InvalidDataException)
            {
                logger.LogError(exception, "Không thể tải cấu hình chatbot đã lưu trên máy.");
                throw new InvalidOperationException(
                    "Không thể đọc/giải mã cấu hình chatbot đã lưu. Hãy giữ nguyên hồ sơ người dùng và khóa bảo vệ dữ liệu của máy, hoặc nhập và lưu lại API key.",
                    exception);
            }
        }
    }

    private async Task PersistSettingsAsync()
    {
        await settingsWriteLock.WaitAsync();
        string? temporaryPath = null;
        try
        {
            var directory = Path.GetDirectoryName(persistentSettingsPath)
                ?? throw new InvalidOperationException("Không xác định được thư mục lưu cấu hình chatbot.");
            Directory.CreateDirectory(directory);

            var settings = new PersistedChatbotSettings
            {
                ProtectedApiKeys = dynamicApiKeys.ToDictionary(
                    entry => entry.Key,
                    entry => apiKeyProtector.Protect(entry.Value),
                    StringComparer.OrdinalIgnoreCase),
                Models = dynamicModels.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value,
                    StringComparer.OrdinalIgnoreCase),
                Provider = dynamicProvider,
                AccountId = dynamicAccountId,
                IsEnabled = dynamicIsEnabled
            };

            temporaryPath = $"{persistentSettingsPath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(settings));
            File.Move(temporaryPath, persistentSettingsPath, overwrite: true);
            persistentSettingsLoaded = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            logger.LogError(exception, "Không thể lưu cấu hình chatbot vào hồ sơ người dùng.");
            throw new InvalidOperationException(
                "Không thể lưu cấu hình chatbot bền vững. Hãy kiểm tra quyền ghi vào thư mục dữ liệu Medika của người dùng.",
                exception);
        }
        finally
        {
            try
            {
                if (temporaryPath != null && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException exception)
            {
                logger.LogWarning(exception, "Không thể xóa tệp tạm cấu hình chatbot.");
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "Không có quyền xóa tệp tạm cấu hình chatbot.");
            }
            finally
            {
                settingsWriteLock.Release();
            }
        }
    }

    private sealed class PersistedChatbotSettings
    {
        public Dictionary<string, string> ProtectedApiKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? Provider { get; set; }
        public string? AccountId { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    private string GetActiveApiKey()
    {
        var provider = GetActiveProvider();
        if (dynamicApiKeys.TryGetValue(provider, out var dynamicKey) && !string.IsNullOrWhiteSpace(dynamicKey))
            return dynamicKey.Trim();

        var configKey = provider switch
        {
            "Gemini" => configuration["Gemini:ApiKey"] ?? configuration["GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            "Mistral" => configuration["Mistral:ApiKey"] ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY"),
            "Groq" => configuration["Groq:ApiKey"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY"),
            "OpenRouter" => configuration["OpenRouter:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            "Cloudflare" => configuration["Cloudflare:ApiKey"] ?? Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN"),
            _ => null
        };
        return configKey?.Trim() ?? string.Empty;
    }

    private string GetActiveProvider()
    {
        if (!string.IsNullOrWhiteSpace(dynamicProvider)) return dynamicProvider;
        var configuredProvider = configuration["Chatbot:Provider"];
        if (!string.IsNullOrWhiteSpace(configuredProvider)) return NormalizeProvider(configuredProvider);
        return !string.IsNullOrWhiteSpace(configuration["Gemini:ApiKey"] ?? configuration["GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
            ? "Gemini"
            : "Mistral";
    }

    private static string NormalizeProvider(string? provider)
    {
        return provider?.Trim().ToLowerInvariant() switch
        {
            "mistral" => "Mistral",
            "groq" => "Groq",
            "openrouter" => "OpenRouter",
            "cloudflare" => "Cloudflare",
            _ => "Gemini"
        };
    }

    private string GetDefaultModel(string provider)
    {
        return provider switch
        {
            "Mistral" => "mistral-small-latest",
            "Groq" => "openai/gpt-oss-20b",
            "OpenRouter" => "mistralai/mistral-small-3.1-24b-instruct:free",
            "Cloudflare" => "@cf/meta/llama-3.1-8b-instruct",
            _ => configuration["Gemini:Model"] ?? "gemini-2.5-flash"
        };
    }

    private string GetActiveModel()
    {
        var provider = GetActiveProvider();
        var model = dynamicModels.TryGetValue(provider, out var selectedModel)
            ? selectedModel
            : GetDefaultModel(provider);
        if (model.Contains('('))
        {
            model = model.Split('(')[0].Trim();
        }
        if (model.StartsWith("models/"))
        {
            model = model["models/".Length..].Trim();
        }
        return model.Trim();
    }

    private bool IsAiEnabled()
    {
        return dynamicIsEnabled && (configuration.GetValue<bool?>("Chatbot:IsEnabled")
            ?? configuration.GetValue<bool?>("Gemini:IsEnabled")
            ?? true);
    }

    public Task<ChatbotConfigDto> GetConfigAsync()
    {
        var provider = GetActiveProvider();
        var key = GetActiveApiKey();
        var masked = string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : (key.Length > 8 ? $"{key[..4]}...{key[^4..]}" : "****");

        return Task.FromResult(new ChatbotConfigDto
        {
            ApiKeyMasked = masked,
            Model = GetActiveModel(),
            Provider = provider,
            AccountId = dynamicAccountId ?? configuration["Cloudflare:AccountId"] ?? string.Empty,
            IsEnabled = IsAiEnabled(),
            HasKey = !string.IsNullOrWhiteSpace(key),
        });
    }

    public async Task<ChatbotConfigDto> UpdateConfigAsync(UpdateChatbotConfigDto updateDto)
    {
        if (!string.IsNullOrWhiteSpace(updateDto.Provider))
            dynamicProvider = NormalizeProvider(updateDto.Provider);

        var provider = GetActiveProvider();
        if (updateDto.ApiKey != null)
        {
            dynamicApiKeys[provider] = updateDto.ApiKey.Trim();
        }
        if (!string.IsNullOrWhiteSpace(updateDto.Model))
        {
            var cleanModel = updateDto.Model.Trim();
            if (cleanModel.Contains('(')) cleanModel = cleanModel.Split('(')[0].Trim();
            if (cleanModel.StartsWith("models/")) cleanModel = cleanModel["models/".Length..].Trim();
            dynamicModels[provider] = cleanModel;
        }
        if (updateDto.AccountId != null)
            dynamicAccountId = updateDto.AccountId.Trim();
        if (updateDto.IsEnabled.HasValue)
        {
            dynamicIsEnabled = updateDto.IsEnabled.Value;
        }

        await PersistSettingsAsync();
        return await GetConfigAsync();
    }

    public async Task<List<string>> GetAvailableModelsAsync(string? provider = null, string? accountId = null)
    {
        var selectedProvider = string.IsNullOrWhiteSpace(provider) ? GetActiveProvider() : NormalizeProvider(provider);
        var apiKey = GetActiveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey)) return [];
        if (selectedProvider == "Gemini") return await QueryAvailableModelsAsync(apiKey);

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        using var request = new HttpRequestMessage(HttpMethod.Get, GetModelsEndpoint(selectedProvider, accountId));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[{Provider}] Model list request failed: {StatusCode}", selectedProvider, (int)response.StatusCode);
            return [];
        }

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);
        var models = (node?["data"] ?? node?["result"])?.AsArray()
            ?.Select(item => item?["id"]?.ToString() ?? item?["name"]?.ToString() ?? string.Empty)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Where(model => selectedProvider != "OpenRouter" || model.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        return models;
    }

    private static string GetModelsEndpoint(string provider, string? accountId) => provider switch
    {
        "Mistral" => "https://api.mistral.ai/v1/models",
        "Groq" => "https://api.groq.com/openai/v1/models",
        "OpenRouter" => "https://openrouter.ai/api/v1/models",
        "Cloudflare" when !string.IsNullOrWhiteSpace(accountId) =>
            $"https://api.cloudflare.com/client/v4/accounts/{Uri.EscapeDataString(accountId.Trim())}/ai/models/search?task=Text%20Generation&per_page=100",
        "Cloudflare" => throw new InvalidOperationException("Nhập Cloudflare Account ID để tải danh sách model."),
        _ => throw new InvalidOperationException($"Nhà cung cấp AI không được hỗ trợ: {provider}")
    };

    public async Task<TestConnectionResponseDto> TestConnectionAsync(string? provider, string? testApiKey, string? testModel, string? accountId)
    {
        var selectedProvider = string.IsNullOrWhiteSpace(provider) ? GetActiveProvider() : NormalizeProvider(provider);
        dynamicProvider = selectedProvider;
        if (accountId != null)
            dynamicAccountId = accountId.Trim();
        if (selectedProvider == "Gemini")
            return await TestGeminiConnectionAsync(testApiKey, testModel);

        var apiKey = !string.IsNullOrWhiteSpace(testApiKey) ? testApiKey.Trim() : GetActiveApiKey();
        var model = !string.IsNullOrWhiteSpace(testModel) ? testModel.Trim() : GetActiveModel();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new TestConnectionResponseDto { Success = false, Message = $"Chưa nhập API key {selectedProvider}." };
        if (selectedProvider == "Cloudflare" && string.IsNullOrWhiteSpace(accountId ?? dynamicAccountId ?? configuration["Cloudflare:AccountId"]))
            return new TestConnectionResponseDto { Success = false, Message = "Nhập Cloudflare Account ID để kiểm tra kết nối." };

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var endpoint = selectedProvider == "Cloudflare"
                ? $"https://api.cloudflare.com/client/v4/accounts/{Uri.EscapeDataString((accountId ?? dynamicAccountId ?? configuration["Cloudflare:AccountId"]!).Trim())}/ai/v1/chat/completions"
                : GetChatCompletionsEndpoint(selectedProvider);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model,
                messages = new[] { new { role = "user", content = "Reply with the single word OK." } },
                max_tokens = 16,
                temperature = 0
            }), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            var availableModels = await GetAvailableModelsForTestAsync(selectedProvider, apiKey, accountId);

            if (response.IsSuccessStatusCode)
            {
                if (!string.IsNullOrWhiteSpace(testApiKey))
                    dynamicApiKeys[selectedProvider] = apiKey;
                if (!string.IsNullOrWhiteSpace(testModel))
                    dynamicModels[selectedProvider] = model;
                if (selectedProvider == "Cloudflare" && !string.IsNullOrWhiteSpace(accountId))
                    dynamicAccountId = accountId.Trim();

                await PersistSettingsAsync();
                return new TestConnectionResponseDto
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Message = $"Kết nối {selectedProvider} thành công với model {model}.",
                    AvailableModels = availableModels
                };
            }

            var providerMessage = ExtractProviderError(responseBody);
            var message = (int)response.StatusCode == 429
                ? $"{selectedProvider} từ chối yêu cầu do giới hạn quota/rate limit. Hạn mức miễn phí phụ thuộc tài khoản và model."
                : $"{selectedProvider} trả lỗi {(int)response.StatusCode}: {providerMessage}";
            logger.LogWarning("[{Provider} Test] Status {StatusCode}: {ProviderMessage}", selectedProvider, (int)response.StatusCode, providerMessage);
            return new TestConnectionResponseDto
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                Message = message,
                ErrorDetails = providerMessage,
                AvailableModels = availableModels
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Provider} Test] Kết nối thất bại", selectedProvider);
            return new TestConnectionResponseDto
            {
                Success = false,
                Message = $"Không thể kết nối tới {selectedProvider}: {ex.Message}"
            };
        }
    }

    private async Task<List<string>> GetAvailableModelsForTestAsync(string provider, string apiKey, string? accountId)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Get, GetModelsEndpoint(provider, accountId));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return [];

            var node = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            var models = (node?["data"] ?? node?["result"])?.AsArray()
                ?.Select(item => item?["id"]?.ToString() ?? item?["name"]?.ToString() ?? string.Empty)
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Where(model => provider != "OpenRouter" || model.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            return models;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{Provider}] Không lấy được danh sách model", provider);
            return [];
        }
    }

    private static string GetChatCompletionsEndpoint(string provider) => provider switch
    {
        "Mistral" => "https://api.mistral.ai/v1/chat/completions",
        "Groq" => "https://api.groq.com/openai/v1/chat/completions",
        "OpenRouter" => "https://openrouter.ai/api/v1/chat/completions",
        _ => throw new InvalidOperationException($"Nhà cung cấp AI không được hỗ trợ: {provider}")
    };

    private static string ExtractProviderError(string responseBody)
    {
        try
        {
            var node = JsonNode.Parse(responseBody);
            return node?["error"]?["message"]?.ToString()
                ?? node?["message"]?.ToString()
                ?? "Không có nội dung lỗi chi tiết.";
        }
        catch
        {
            return "Không đọc được phản hồi lỗi từ nhà cung cấp.";
        }
    }

    private async Task<List<string>> QueryAvailableModelsAsync(string apiKey)
    {
        var models = new List<string>();
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var endpoints = new[]
            {
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}",
                $"https://generativelanguage.googleapis.com/v1/models?key={apiKey}"
            };

            foreach (var listUrl in endpoints)
            {
                try
                {
                    var res = await client.GetAsync(listUrl);
                    if (res.IsSuccessStatusCode)
                    {
                        var body = await res.Content.ReadAsStringAsync();
                        var node = JsonNode.Parse(body);
                        var items = node?["models"]?.AsArray();
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var name = item?["name"]?.ToString() ?? "";
                                var methods = item?["supportedGenerationMethods"]?.AsArray();
                                bool supportsGenerate = methods != null && methods.Any(m => m?.ToString() == "generateContent");
                                if (supportsGenerate)
                                {
                                    var cleanName = name.StartsWith("models/") ? name["models/".Length..] : name;
                                    if (!models.Contains(cleanName)) models.Add(cleanName);
                                }
                            }
                        }
                        if (models.Count > 0) break;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Gemini] Không thể lấy danh sách models từ Google");
        }
        return models;
    }

    private async Task<TestConnectionResponseDto> TestGeminiConnectionAsync(string? testApiKey = null, string? testModel = null)
    {
        var key = !string.IsNullOrWhiteSpace(testApiKey) ? testApiKey.Trim() : GetActiveApiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Gemini Test WARNING] Chưa có API Key để kiểm tra kết nối.");
            Console.ResetColor();
            return new TestConnectionResponseDto
            {
                Success = false,
                Message = "Chưa cung cấp API Key. Vui lòng nhập API Key vào ô cấu hình trước khi kiểm tra."
            };
        }

        var model = !string.IsNullOrWhiteSpace(testModel) ? testModel.Trim() : GetActiveModel();
        if (model.Contains('(')) model = model.Split('(')[0].Trim();
        if (model.StartsWith("models/")) model = model["models/".Length..].Trim();

        Console.WriteLine("\n================== [GEMINI TEST CONNECTION] ==================");
        Console.WriteLine($"[Gemini Test] Model: {model}");
        Console.WriteLine($"[Gemini Test] API Key: {(key.Length > 8 ? $"{key[..4]}...{key[^4..]}" : "****")}");

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            // Payload chuẩn: bắt buộc có role:"user" cho Gemini 2.x
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = "Xin chào! Hãy trả lời đúng một từ: 'OK'" } }
                    }
                }
            };
            var json = JsonSerializer.Serialize(requestBody);

            // Helper: thử gọi generateContent theo endpoint đã cho, trả về HttpResponseMessage
            async Task<(HttpResponseMessage Res, string Body)> TryGenerate(string endpointModel)
            {
                var body = new StringContent(json, Encoding.UTF8, "application/json");
                // Thử v1beta trước
                var urlBeta = $"https://generativelanguage.googleapis.com/v1beta/models/{endpointModel}:generateContent?key={key}";
                Console.WriteLine($"[Gemini Test] Thử  → {urlBeta[..urlBeta.IndexOf('?')]}");
                var r = await client.PostAsync(urlBeta, body);
                if (r.IsSuccessStatusCode) return (r, await r.Content.ReadAsStringAsync());

                if (r.StatusCode == HttpStatusCode.NotFound)
                {
                    // Thử endpoint v1 (GA)
                    var urlV1 = $"https://generativelanguage.googleapis.com/v1/models/{endpointModel}:generateContent?key={key}";
                    Console.WriteLine($"[Gemini Test] v1beta→404, thử v1 → {urlV1[..urlV1.IndexOf('?')]}");
                    var bodyV1 = new StringContent(json, Encoding.UTF8, "application/json");
                    var r2 = await client.PostAsync(urlV1, bodyV1);
                    return (r2, await r2.Content.ReadAsStringAsync());
                }

                return (r, await r.Content.ReadAsStringAsync());
            }

            var (res, responseBody) = await TryGenerate(model);

            // Nếu model đã chọn không tồn tại, trả danh sách model khả dụng để Admin tự chọn.
            var availableModels = new List<string>();

            if (!res.IsSuccessStatusCode && res.StatusCode == HttpStatusCode.NotFound)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[Gemini Test] Đang truy vấn ModelService.ListModels để tìm model thực sự khả dụng...");
                Console.ResetColor();

                availableModels = await QueryAvailableModelsAsync(key);
                if (availableModels.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[Gemini Test] Models khả dụng: {string.Join(", ", availableModels)}");
                    Console.ResetColor();
                }
            }

            if (res.IsSuccessStatusCode)
            {
                if (!string.IsNullOrWhiteSpace(testModel))
                    dynamicModels["Gemini"] = model;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Gemini Test SUCCESS] StatusCode: {(int)res.StatusCode}");
                Console.WriteLine($"[Gemini Test SUCCESS] Kết nối thành công với model: {model}");
                Console.ResetColor();
                Console.WriteLine("==============================================================\n");

                // Giữ hành vi hiện tại: nhận API key vừa nhập cho các lần gọi tiếp theo.
                if (!string.IsNullOrWhiteSpace(testApiKey))
                {
                    dynamicApiKeys["Gemini"] = key;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Gemini] Đã lưu runtime API key: ****...{key[^4..]}");
                    Console.ResetColor();
                }

                await PersistSettingsAsync();
                return new TestConnectionResponseDto
                {
                    Success = true,
                    StatusCode = (int)res.StatusCode,
                    Message = $"Kết nối Google Gemini AI ({model}) thành công!",
                    AvailableModels = availableModels
                };
            }

            // Xử lý lỗi cuối cùng
            var statusCode = (int)res.StatusCode;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Gemini Test ERROR] StatusCode: {statusCode} ({res.ReasonPhrase})");
            Console.WriteLine($"[Gemini Test ERROR] Response Body:\n{responseBody}");
            Console.ResetColor();
            Console.WriteLine("==============================================================\n");

            logger.LogError("[Gemini Test Error] Status: {StatusCode}, Body: {Body}", statusCode, responseBody);

            string errorMsgFromGoogle = string.Empty;
            string quotaModel = string.Empty;
            string quotaDescription = string.Empty;
            string retryDelay = string.Empty;
            try
            {
                var errNode = JsonNode.Parse(responseBody);
                var errorNode = errNode?["error"];
                errorMsgFromGoogle = errorNode?["message"]?.ToString() ?? string.Empty;
                if (errorNode?["details"] is JsonArray errorDetails)
                {
                    foreach (var detail in errorDetails)
                    {
                        var type = detail?["@type"]?.ToString() ?? string.Empty;
                        if (type.EndsWith("QuotaFailure", StringComparison.Ordinal) &&
                            detail?["violations"] is JsonArray violations)
                        {
                            var violation = violations.FirstOrDefault();
                            quotaModel = violation?["quotaDimensions"]?["model"]?.ToString() ?? string.Empty;
                            quotaDescription = violation?["description"]?.ToString() ?? string.Empty;
                        }
                        else if (type.EndsWith("RetryInfo", StringComparison.Ordinal))
                        {
                            retryDelay = detail?["retryDelay"]?.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch { }

            string userFriendlyMsg;
            if (statusCode == 400)
                userFriendlyMsg = "Yêu cầu không hợp lệ (400 Bad Request). Vui lòng kiểm tra lại mã Model hoặc định dạng yêu cầu.";
            else if (statusCode is 401 or 403)
                userFriendlyMsg = "Khóa API không hợp lệ hoặc bị từ chối (403/401). Vui lòng kiểm tra lại API Key từ Google AI Studio.";
            else if (statusCode == 404)
            {
                if (availableModels.Count > 0)
                    userFriendlyMsg = $"Model '{model}' không khả dụng với API key này. Hãy chọn một model trong danh sách được tải từ tài khoản của bạn.";
                else
                    userFriendlyMsg = $"Không tìm thấy Model '{model}' (404 Not Found). Vui lòng chọn model khác (ví dụ: gemini-2.5-flash hoặc gemini-2.5-pro).";
            }
            else if (statusCode == 429)
            {
                var modelInfo = !string.IsNullOrWhiteSpace(quotaModel) ? $" Model bị từ chối: {quotaModel}." : string.Empty;
                var quotaInfo = responseBody.Contains("limit: 0", StringComparison.OrdinalIgnoreCase)
                    ? " Hạn mức hiện tại của model bằng 0; chờ rồi thử lại sẽ không khắc phục nếu Google chưa cấp quota."
                    : string.Empty;
                var retryInfo = !string.IsNullOrWhiteSpace(retryDelay)
                    ? $" Google đề nghị thử lại sau {retryDelay}."
                    : string.Empty;
                userFriendlyMsg = $"Google từ chối yêu cầu vì quota Gemini API (429).{modelInfo}{quotaInfo}{retryInfo} Danh sách model hiển thị không bảo đảm tài khoản có quota cho model đó. Hãy chọn model đã kiểm tra kết nối thành công hoặc kiểm tra quota/billing trong Google AI Studio.";
            }
            else
                userFriendlyMsg = $"Google API báo lỗi: {statusCode} {res.ReasonPhrase}";

            return new TestConnectionResponseDto
            {
                Success = false,
                StatusCode = statusCode,
                Message = userFriendlyMsg,
                ErrorDetails = statusCode == 429
                    ? (!string.IsNullOrWhiteSpace(quotaDescription) ? quotaDescription : errorMsgFromGoogle)
                    : (!string.IsNullOrWhiteSpace(errorMsgFromGoogle) ? errorMsgFromGoogle : responseBody),
                AvailableModels = availableModels
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Gemini Test EXCEPTION] Lỗi kết nối: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine("==============================================================\n");
            logger.LogError(ex, "[Gemini Test Exception] Lỗi kết nối tới Google API");

            return new TestConnectionResponseDto
            {
                Success = false,
                Message = $"Không thể kết nối tới máy chủ Google (Lỗi mạng hoặc Timeout): {ex.Message}",
                ErrorDetails = ex.Message
            };
        }
    }


    public async Task<ChatbotMessageResponse> ProcessMessageAsync(string message, int? userId = null, string? sessionId = null)
    {
        var raw = (message ?? string.Empty).Trim();
        var lower = raw.ToLowerInvariant();

        var response = new ChatbotMessageResponse();

        // 1. Kiểm tra dấu hiệu cấp cứu / khẩn cấp tuyệt đối
        var emergencyKeywords = new[] { "đau ngực", "khó thở", "cấp cứu", "nguy kịch", "hôn mê", "đột quỵ", "co giật", "tức ngực", "bất tỉnh", "ngưng tim", "ngưng thở", "nôn ra máu" };
        if (emergencyKeywords.Any(k => lower.Contains(k)))
        {
            response.Intent = "emergency";
            response.Source = "Medika Safety Protocol";
            response.Reply = "**CẢNH BÁO: Đây có thể là tình trạng khẩn cấp, vui lòng gọi 115 hoặc đến cơ sở y tế gần nhất.**\n\n" +
                             "Dấu hiệu bạn vừa mô tả cần được can thiệp y tế khẩn cấp ngay lập tức! " +
                             "Vui lòng gọi ngay 115 hoặc Medika Clinic: 0335844656, hoặc đến ngay khoa Cấp cứu của cơ sở y tế gần nhất. " +
                             "Tuyệt đối không tự ý điều trị hay trì hoãn tại nhà.";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📞 Gọi 115 ngay", ActionType = "info", Value = "tel:115" },
                new() { Label = "📞 Medika: 0335844656", ActionType = "info", Value = "tel:0335844656" }
            };
            return response;
        }

        var activeSessionId = !string.IsNullOrWhiteSpace(sessionId) ? sessionId.Trim() : Guid.NewGuid().ToString();
        var bookingCacheKey = $"chatbot_booking_{activeSessionId}";
        var bookingDraft = memoryCache.Get<ChatbotBookingDraft>(bookingCacheKey);
        if (bookingDraft != null || IsAppointmentRequest(lower))
        {
            bookingDraft ??= new ChatbotBookingDraft();
            memoryCache.Set(bookingCacheKey, bookingDraft, TimeSpan.FromMinutes(30));
            return await HandleBookingFlowAsync(raw, lower, bookingCacheKey, bookingDraft, userId);
        }

        // Quản lý bộ nhớ hội thoại (Memory Cache) theo sessionId (SlidingExpiration: 30 phút)
        var cacheKey = $"chatbot_memory_{activeSessionId}";
        var history = memoryCache.Get<List<GeminiChatMessage>>(cacheKey) ?? new List<GeminiChatMessage>();

        // Đảm bảo không có 2 message 'user' liên tiếp nếu lượt trước bị gián đoạn
        while (history.Count > 0 && history[^1].Role == "user")
        {
            history.RemoveAt(history.Count - 1);
        }

        // Thêm tin nhắn hiện tại của user vào lịch sử
        history.Add(new GeminiChatMessage
        {
            Role = "user",
            Parts = new List<GeminiChatPart> { new() { Text = raw } }
        });
        memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });

        // TỐI ƯU HÓA CONTEXT WINDOW (Nhiệm vụ 3):
        // Loại bỏ việc nhét toàn bộ List<Doctor> vào systemPrompt mặc định.
        // Chỉ query SQL Server (EF Core) 2-3 bác sĩ phù hợp nhất khi người dùng hỏi về chuyên khoa hoặc tìm bác sĩ.
        var matchedSpecialty = DetectSpecialty(lower);
        List<Doctor> relevantDoctors = new();

        if (!string.IsNullOrEmpty(matchedSpecialty))
        {
            relevantDoctors = await context.Doctors.AsNoTracking()
                .Where(d => d.IsActive && d.Specialty.Contains(matchedSpecialty))
                .OrderByDescending(d => d.ExperienceYears)
                .Take(3)
                .ToListAsync();
        }
        else if (lower.Contains("bác sĩ") || lower.Contains("chuyên khoa") || lower.Contains("đặt lịch") || lower.Contains("hẹn khám") || lower.Contains("khám"))
        {
            var prevUserText = history.Where(h => h.Role == "user").SelectMany(h => h.Parts).LastOrDefault()?.Text?.ToLowerInvariant() ?? "";
            var prevSpecialty = DetectSpecialty(prevUserText);
            if (!string.IsNullOrEmpty(prevSpecialty))
            {
                relevantDoctors = await context.Doctors.AsNoTracking()
                    .Where(d => d.IsActive && d.Specialty.Contains(prevSpecialty))
                    .OrderByDescending(d => d.ExperienceYears)
                    .Take(3)
                    .ToListAsync();
                matchedSpecialty = prevSpecialty;
            }
            else
            {
                relevantDoctors = await context.Doctors.AsNoTracking()
                    .Where(d => d.IsActive)
                    .OrderByDescending(d => d.ExperienceYears)
                    .Take(3)
                    .ToListAsync();
            }
        }

        // Kiểm tra cấu hình nhà cung cấp AI đang được chọn.
        var apiKey = GetActiveApiKey();
        var provider = GetActiveProvider();
        var useAiProvider = !string.IsNullOrWhiteSpace(apiKey) && IsAiEnabled();

        if (useAiProvider)
        {
            try
            {
                var aiResult = provider == "Gemini"
                    ? await CallGeminiApiAsync(history, relevantDoctors, apiKey)
                    : await CallOpenAiCompatibleApiAsync(provider, history, relevantDoctors, apiKey);

                if (aiResult?.IsFunctionCall == true && aiResult.FunctionName == "create_appointment")
                {
                    response.Intent = "appointment_confirmation_required";
                    response.Source = $"{provider} AI";
                    response.Reply = "Để tránh tạo nhầm lịch, vui lòng bắt đầu bằng “Đặt lịch khám”. Tôi sẽ hỏi lần lượt chuyên khoa, bác sĩ, ngày giờ và lý do khám, sau đó chỉ tạo lịch khi bạn xác nhận.";
                    return response;
                }

                // NHIỆM VỤ 4: Xử lý Function Calling (Tự động đặt lịch khám)
                if (aiResult != null && aiResult.IsFunctionCall && aiResult.FunctionName == "create_appointment")
                {
                    var args = aiResult.FunctionArgs;
                    int reqDoctorId = 0;
                    if (args?["doctorId"] != null)
                    {
                        _ = int.TryParse(args["doctorId"]?.ToString(), out reqDoctorId);
                    }
                    string dateStr = args?["date"]?.ToString() ?? "";
                    string reason = args?["reason"]?.ToString() ?? "Khám bệnh theo tư vấn của Medika AI";
                    int reqPatientId = 0;
                    if (args?["patientId"] != null)
                    {
                        _ = int.TryParse(args["patientId"]?.ToString(), out reqPatientId);
                    }

                    // 1. Xác thực hoặc gán Bác sĩ phù hợp
                    var doctor = reqDoctorId > 0 ? await context.Doctors.FindAsync(reqDoctorId) : null;
                    if (doctor == null)
                    {
                        doctor = relevantDoctors.FirstOrDefault() ?? await context.Doctors.FirstOrDefaultAsync(d => d.IsActive);
                    }
                    var finalDoctorId = doctor?.Id ?? 1;

                    // 2. Phân tích thời gian hẹn khám
                    if (!DateTime.TryParse(dateStr, out var appointmentDate) || appointmentDate < DateTime.Now)
                    {
                        // Mặc định ngày mai lúc 09:00 nếu không parse được thời gian
                        appointmentDate = DateTime.Today.AddDays(1).AddHours(9);
                    }

                    // 3. Xác thực hoặc khởi tạo Hồ sơ Bệnh nhân
                    int finalPatientId;
                    if (userId.HasValue && userId.Value > 0)
                    {
                        var patient = await context.Patients.FirstOrDefaultAsync(p => p.UserId == userId.Value);
                        if (patient == null)
                        {
                            var user = await context.Users.FindAsync(userId.Value);
                            patient = new Patient
                            {
                                UserId = userId.Value,
                                FullName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : (user?.Username ?? "Bệnh nhân"),
                                DateOfBirth = new DateOnly(1990, 1, 1),
                                Phone = "0900000000",
                                Address = "33 Xô Viết Nghệ Tĩnh, phường Hòa Cường, thành phố Đà Nẵng",
                                MedicalHistory = "Tạo tự động qua Medika AI"
                            };
                            context.Patients.Add(patient);
                            await context.SaveChangesAsync();
                        }
                        finalPatientId = patient.Id;
                    }
                    else if (reqPatientId > 0 && await context.Patients.AnyAsync(p => p.Id == reqPatientId))
                    {
                        finalPatientId = reqPatientId;
                    }
                    else
                    {
                        var guestPatient = await context.Patients.FirstOrDefaultAsync(p => p.FullName == "Khách vãng lai (AI Booking)");
                        if (guestPatient == null)
                        {
                            guestPatient = new Patient
                            {
                                FullName = "Khách vãng lai (AI Booking)",
                                DateOfBirth = new DateOnly(1990, 1, 1),
                                Phone = "0335844656",
                                Address = "Đăng ký trực tuyến",
                                MedicalHistory = "Khách vãng lai đặt lịch qua Chatbot"
                            };
                            context.Patients.Add(guestPatient);
                            await context.SaveChangesAsync();
                        }
                        finalPatientId = guestPatient.Id;
                    }

                    // 4. Khởi tạo entity Appointment với trạng thái Pending và lưu thẳng vào AppDbContext
                    var appointment = new Appointment
                    {
                        PatientId = finalPatientId,
                        DoctorId = finalDoctorId,
                        UserId = userId,
                        AppointmentDate = appointmentDate,
                        Reason = reason.Trim(),
                        Status = AppointmentStatus.Pending,
                        CreatedAt = DateTime.Now
                    };

                    context.Appointments.Add(appointment);
                    await context.SaveChangesAsync();

                    // 5. Chuẩn bị câu trả lời xác nhận và cờ cập nhật UI
                    var docName = doctor?.FullName ?? $"Bác sĩ #{finalDoctorId}";
                    var specialty = doctor?.Specialty ?? "Đa khoa";
                    var room = doctor?.Room ?? "Khu khám bệnh Medika";

                    var confirmationReply = $"🎉 **XÁC NHẬN ĐẶT LỊCH KHÁM THÀNH CÔNG!**\n\n" +
                                            $"• **Mã lịch hẹn:** #{appointment.Id}\n" +
                                            $"• **Bác sĩ phụ trách:** {docName} ({specialty} - {room})\n" +
                                            $"• **Thời gian hẹn:** {appointmentDate:dd/MM/yyyy HH:mm}\n" +
                                            $"• **Lý do khám:** {reason}\n" +
                                            $"• **Trạng thái:** ⏳ Chờ xác nhận (Pending)\n\n" +
                                            $"Phòng khám Đa khoa Medika đã tiếp nhận lịch khám. Nhân viên chăm sóc khách hàng sẽ liên hệ xác nhận trong thời gian sớm nhất. Hệ thống đang tự động làm mới giao diện của bạn!";

                    history.Add(new GeminiChatMessage
                    {
                        Role = "model",
                        Parts = new List<GeminiChatPart> { new() { Text = confirmationReply } }
                    });

                    if (history.Count > 20) history = history.Skip(history.Count - 20).ToList();
                    memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });

                    response.Reply = confirmationReply;
                    response.Source = $"{provider} AI (Auto-Booking)";
                    response.Intent = "appointment_created";
                    response.ShouldRefreshUi = true;
                    response.CreatedAppointmentId = appointment.Id;
                    response.Doctors = doctor != null ? new List<ChatbotDoctorInfo> { ToDoctorInfo(doctor) } : [];
                    response.QuickActions = new List<ChatbotQuickAction>
                    {
                        new() { Label = "📋 Xem lịch hẹn của tôi", ActionType = "info", Value = "Lịch của tôi" },
                        new() { Label = "💬 Nhắn tin lễ tân hỗ trợ", ActionType = "support" }
                    };

                    return response;
                }

                // Xử lý phản hồi dạng Text thông thường
                if (aiResult != null && !string.IsNullOrWhiteSpace(aiResult.Text))
                {
                    var geminiReply = aiResult.Text;
                    history.Add(new GeminiChatMessage
                    {
                        Role = "model",
                        Parts = new List<GeminiChatPart> { new() { Text = geminiReply } }
                    });

                    if (history.Count > 20) history = history.Skip(history.Count - 20).ToList();
                    memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });

                    response.Reply = geminiReply;
                    response.Source = provider == "Gemini" ? "Google Gemini AI" : $"{provider} AI";
                    response.Intent = "ai_consultation";

                    // Gợi ý bác sĩ nếu có trong danh sách truy vấn phù hợp hoặc nhận diện chuyên khoa
                    if (relevantDoctors.Count > 0)
                    {
                        response.Doctors = relevantDoctors.Select(ToDoctorInfo).ToList();
                    }
                    else
                    {
                        var detected = DetectSpecialty(lower) ?? DetectSpecialty(geminiReply.ToLowerInvariant());
                        if (!string.IsNullOrEmpty(detected))
                        {
                            var docs = await context.Doctors.AsNoTracking()
                                .Where(d => d.IsActive && d.Specialty.Contains(detected))
                                .OrderByDescending(d => d.ExperienceYears)
                                .Take(3)
                                .ToListAsync();
                            response.Doctors = docs.Select(ToDoctorInfo).ToList();
                            matchedSpecialty = detected;
                        }
                    }

                    if (!string.IsNullOrEmpty(matchedSpecialty))
                    {
                        response.QuickActions.Add(new ChatbotQuickAction
                        {
                            Label = $"📅 Đặt lịch khám {matchedSpecialty}",
                            ActionType = "book",
                            Value = matchedSpecialty
                        });
                    }

                    response.QuickActions.Add(new ChatbotQuickAction { Label = "💬 Nhắn tin với nhân viên hỗ trợ", ActionType = "support" });
                    return response;
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                var statusCode = (int)ex.StatusCode.Value;
                logger.LogWarning("Nhà cung cấp AI {Provider} trả về HTTP {StatusCode}", provider, statusCode);
                response.Source = $"{provider} AI";
                response.Intent = "ai_provider_error";
                response.Reply = statusCode switch
                {
                    429 => $"Khóa API vẫn được lưu, nhưng {provider} đang từ chối yêu cầu do tài khoản đã chạm giới hạn tốc độ hoặc hạn mức sử dụng. Vui lòng chờ hạn mức được làm mới hoặc kiểm tra gói và hạn mức trên tài khoản {provider}, rồi thử lại.",
                    401 or 403 => $"Nhà cung cấp {provider} từ chối xác thực API. Vui lòng kiểm tra lại khóa API và quyền sử dụng của tài khoản trong phần quản trị.",
                    _ => $"Nhà cung cấp {provider} hiện không xử lý được yêu cầu (HTTP {statusCode}). Vui lòng thử lại sau."
                };
                return response;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ChatbotService ProcessMessage EXCEPTION]: {ex.Message}");
                Console.ResetColor();
                logger.LogError(ex, "Lỗi ngoại lệ khi gọi nhà cung cấp AI {Provider}", provider);
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[ChatbotService] AI provider {provider} chưa sẵn sàng (HasKey: {!string.IsNullOrWhiteSpace(apiKey)}, IsEnabled: {IsAiEnabled()}). Tự động dùng fallback nội bộ Medika.");
            Console.ResetColor();
        }

        // 2. Fallback xử lý thông minh nội bộ của Medika
        var fallbackResponse = await ProcessInternalFallbackAsync(raw, lower, userId, relevantDoctors);

        if (!string.IsNullOrWhiteSpace(fallbackResponse.Reply))
        {
            history.Add(new GeminiChatMessage
            {
                Role = "model",
                Parts = new List<GeminiChatPart> { new() { Text = fallbackResponse.Reply } }
            });

            if (history.Count > 20) history = history.Skip(history.Count - 20).ToList();
            memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
        }

        return fallbackResponse;
    }

    private static bool IsAppointmentRequest(string lower)
    {
        var asksToBook = lower.Contains("đặt lịch") ||
                         lower.Contains("đặt cho tôi lịch") ||
                         lower.Contains("đặt hẹn") ||
                         lower.Contains("đăng ký khám") ||
                         lower.Contains("muốn khám") ||
                         lower.Contains("hẹn khám");
        return asksToBook && !lower.Contains("hướng dẫn") && !lower.Contains("quy trình");
    }

    private async Task<ChatbotMessageResponse> HandleBookingFlowAsync(
        string raw,
        string lower,
        string cacheKey,
        ChatbotBookingDraft draft,
        int? userId)
    {
        var response = new ChatbotMessageResponse
        {
            Source = "Medika Booking Assistant",
            Intent = "appointment_booking"
        };

        if (lower is "hủy" or "huỷ" or "hủy đặt lịch" or "huỷ đặt lịch" or "bỏ đặt lịch")
        {
            memoryCache.Remove(cacheKey);
            response.Reply = "Đã hủy quá trình đặt lịch. Khi cần, bạn có thể nhắn “Đặt lịch khám” để bắt đầu lại.";
            return response;
        }

        if (draft.DateNeedsConfirmation)
        {
            if (lower.Contains("giữ") || lower.Contains("ngày 13") || lower.Contains("thứ sáu") || lower.Contains("thứ 6"))
            {
                draft.DateNeedsConfirmation = false;
            }
            else if ((lower.Contains("đổi") || lower.Contains("chuyển") || lower.Contains("thứ bảy") || lower.Contains("thứ 7")) &&
                     draft.AlternateDate.HasValue)
            {
                draft.AppointmentDate = draft.AlternateDate.Value;
                draft.DateNeedsConfirmation = false;
            }
            else
            {
                response.Reply = GetWeekdayConflictPrompt(draft);
                memoryCache.Set(cacheKey, draft, TimeSpan.FromMinutes(30));
                return response;
            }
        }

        var detectedSpecialty = DetectSpecialty(lower);
        if (!string.IsNullOrWhiteSpace(detectedSpecialty) &&
            (string.IsNullOrWhiteSpace(draft.Specialty) || (!draft.AwaitingReason && !draft.AwaitingConfirmation)))
        {
            draft.Specialty = detectedSpecialty;
            draft.DoctorId = null;
        }

        if (TryExtractAppointmentDate(raw, draft.AppointmentDate, out var appointmentDate))
        {
            draft.AppointmentDate = appointmentDate;
            var requestedWeekday = DetectRequestedWeekday(lower);
            if (requestedWeekday.HasValue && appointmentDate.DayOfWeek != requestedWeekday.Value)
            {
                draft.DateNeedsConfirmation = true;
                draft.AlternateDate = GetNextDateOnWeekday(appointmentDate, requestedWeekday.Value);
                draft.RequestedWeekday = requestedWeekday.Value;
                response.Reply = GetWeekdayConflictPrompt(draft);
                memoryCache.Set(cacheKey, draft, TimeSpan.FromMinutes(30));
                return response;
            }
        }

        var explicitDoctorMatch = Regex.Match(lower, @"(?:bác sĩ\s*(?:mã|id|#)?\s*)(\d+)");
        if (explicitDoctorMatch.Success && int.TryParse(explicitDoctorMatch.Groups[1].Value, out var explicitDoctorId))
        {
            var explicitlySelectedDoctor = await context.Doctors.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == explicitDoctorId && d.IsActive);
            if (explicitlySelectedDoctor != null)
            {
                draft.Specialty ??= explicitlySelectedDoctor.Specialty;
                draft.DoctorId = explicitlySelectedDoctor.Id;
            }
        }

        List<Doctor> doctors = !string.IsNullOrWhiteSpace(draft.Specialty)
            ? await context.Doctors.AsNoTracking()
                .Where(d => d.IsActive && d.Specialty.Contains(draft.Specialty))
                .OrderByDescending(d => d.ExperienceYears)
                .ToListAsync()
            : new List<Doctor>();

        if (draft.DoctorId.HasValue)
        {
            var selectedDoctor = doctors.FirstOrDefault(d => d.Id == draft.DoctorId.Value);
            if (selectedDoctor == null)
            {
                selectedDoctor = doctors.FirstOrDefault(d =>
                    lower.Contains(d.FullName.ToLowerInvariant(), StringComparison.Ordinal));
                if (selectedDoctor != null)
                {
                    draft.DoctorId = selectedDoctor.Id;
                }
            }

            if (selectedDoctor == null)
            {
                draft.DoctorId = null;
            }
        }
        else if (!string.IsNullOrWhiteSpace(draft.Specialty))
        {
            var selectedByName = doctors.FirstOrDefault(d =>
                lower.Contains(d.FullName.ToLowerInvariant(), StringComparison.Ordinal));
            if (selectedByName != null)
            {
                draft.DoctorId = selectedByName.Id;
            }
            else if (draft.AwaitingDoctor)
            {
                var choiceMatch = Regex.Match(lower.Trim(), @"^(?:tôi chọn\s*)?(\d+)$");
                if (choiceMatch.Success &&
                    int.TryParse(choiceMatch.Groups[1].Value, out var choice) &&
                    choice >= 1 && choice <= doctors.Count)
                {
                    draft.DoctorId = doctors[choice - 1].Id;
                }
            }
        }

        if (draft.AwaitingReason && !draft.AwaitingConfirmation &&
            !TryExtractAppointmentDate(raw, null, out _) &&
            !string.IsNullOrWhiteSpace(raw))
        {
            draft.Reason = Regex.Replace(raw, @"^(lý do khám là|tôi đến khám vì|lý do là)\s*", "", RegexOptions.IgnoreCase).Trim();
            draft.AwaitingReason = false;
        }

        if (draft.DateNeedsConfirmation)
        {
            response.Reply = GetWeekdayConflictPrompt(draft);
            memoryCache.Set(cacheKey, draft, TimeSpan.FromMinutes(30));
            return response;
        }

        if (string.IsNullOrWhiteSpace(draft.Specialty))
        {
            draft.AwaitingDoctor = false;
            response.Reply = "Bạn muốn khám chuyên khoa nào? Ví dụ: Tai Mũi Họng, Tim mạch, Da liễu hoặc Nội tổng quát.";
        }
        else if (doctors.Count == 0)
        {
            draft.AwaitingDoctor = false;
            response.Reply = $"Hiện chưa có bác sĩ đang nhận lịch ở chuyên khoa {draft.Specialty}. Bạn muốn chọn chuyên khoa khác không?";
        }
        else if (!draft.DoctorId.HasValue)
        {
            draft.AwaitingDoctor = true;
            response.Doctors = doctors.Select(ToDoctorInfo).ToList();
            response.Reply = $"Bạn chọn bác sĩ nào thuộc chuyên khoa {draft.Specialty}? Nhấn “Chọn bác sĩ” bên dưới hoặc nhập tên bác sĩ.";
        }
        else if (!draft.AppointmentDate.HasValue || draft.AppointmentDate.Value.TimeOfDay == TimeSpan.Zero)
        {
            draft.AwaitingDoctor = false;
            response.Reply = "Bạn muốn khám vào ngày nào và lúc mấy giờ? Ví dụ: “ngày 13/11/2026 lúc 7 giờ sáng”.";
        }
        else if (draft.AppointmentDate.Value <= DateTime.Now)
        {
            draft.AppointmentDate = null;
            draft.AwaitingDoctor = false;
            response.Reply = "Thời gian bạn chọn đã qua. Vui lòng cho tôi ngày và giờ khám trong tương lai.";
        }
        else if (string.IsNullOrWhiteSpace(draft.Reason))
        {
            draft.AwaitingDoctor = false;
            draft.AwaitingReason = true;
            response.Reply = "Bạn đến khám vì triệu chứng hoặc lý do gì? Hãy mô tả ngắn gọn để tôi ghi vào yêu cầu đặt lịch.";
        }
        else if (draft.AwaitingConfirmation && IsConfirmation(lower))
        {
            var doctor = doctors.First(d => d.Id == draft.DoctorId!.Value);
            var patient = userId.HasValue
                ? await context.Patients.FirstOrDefaultAsync(p => p.UserId == userId.Value)
                : null;

            if (userId.HasValue && patient == null)
            {
                var user = await context.Users.FindAsync(userId.Value);
                patient = new Patient
                {
                    UserId = userId.Value,
                    FullName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : (user?.Username ?? "Bệnh nhân"),
                    DateOfBirth = new DateOnly(1990, 1, 1),
                    Phone = "0900000000",
                    Address = "33 Xô Viết Nghệ Tĩnh, phường Hòa Cường, thành phố Đà Nẵng",
                    MedicalHistory = "Tạo tự động qua Medika AI"
                };
                context.Patients.Add(patient);
                await context.SaveChangesAsync();
            }
            else if (patient == null)
            {
                patient = await context.Patients.FirstOrDefaultAsync(p => p.FullName == "Khách vãng lai (AI Booking)");
                if (patient == null)
                {
                    patient = new Patient
                    {
                        FullName = "Khách vãng lai (AI Booking)",
                        DateOfBirth = new DateOnly(1990, 1, 1),
                        Phone = "0335844656",
                        Address = "Đăng ký trực tuyến",
                        MedicalHistory = "Khách vãng lai đặt lịch qua Chatbot"
                    };
                    context.Patients.Add(patient);
                    await context.SaveChangesAsync();
                }
            }

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                UserId = userId,
                AppointmentDate = draft.AppointmentDate.Value,
                BookingSource = AppointmentBookingSource.Online,
                Reason = draft.Reason,
                Status = AppointmentStatus.Pending,
                CreatedAt = DateTime.Now
            };
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();
            memoryCache.Remove(cacheKey);

            response.Intent = "appointment_created";
            response.ShouldRefreshUi = true;
            response.CreatedAppointmentId = appointment.Id;
            response.Doctors = [ToDoctorInfo(doctor)];
            response.Reply = $"Đã gửi yêu cầu đặt lịch khám thành công.\n\n• Bác sĩ: {doctor.FullName} ({doctor.Specialty})\n• Thời gian: {GetVietnameseWeekday(draft.AppointmentDate.Value.DayOfWeek)}, {draft.AppointmentDate.Value:dd/MM/yyyy HH:mm}\n• Lý do khám: {draft.Reason}\n• Trạng thái: Chờ phòng khám xác nhận.";
            return response;
        }
        else if (draft.AwaitingConfirmation)
        {
            response.Reply = $"Thông tin đặt lịch hiện tại:\n\n• Chuyên khoa: {draft.Specialty}\n• Bác sĩ: {doctors.First(d => d.Id == draft.DoctorId!.Value).FullName}\n• Thời gian: {GetVietnameseWeekday(draft.AppointmentDate.Value.DayOfWeek)}, {draft.AppointmentDate.Value:dd/MM/yyyy HH:mm}\n• Lý do khám: {draft.Reason}\n\nBạn xác nhận đặt lịch với thông tin này chứ? Hãy trả lời “Có, xác nhận” hoặc nói rõ mục muốn chỉnh sửa.";
        }
        else
        {
            draft.AwaitingConfirmation = true;
            var doctor = doctors.First(d => d.Id == draft.DoctorId!.Value);
            response.Reply = $"Vui lòng kiểm tra thông tin trước khi tôi gửi yêu cầu đặt lịch:\n\n• Chuyên khoa: {draft.Specialty}\n• Bác sĩ: {doctor.FullName}\n• Thời gian: {GetVietnameseWeekday(draft.AppointmentDate.Value.DayOfWeek)}, {draft.AppointmentDate.Value:dd/MM/yyyy HH:mm}\n• Lý do khám: {draft.Reason}\n\nBạn xác nhận đặt lịch với thông tin này chứ? Hãy trả lời “Có, xác nhận” hoặc nói rõ mục muốn chỉnh sửa.";
        }

        memoryCache.Set(cacheKey, draft, TimeSpan.FromMinutes(30));
        return response;
    }

    private static bool TryExtractAppointmentDate(string input, DateTime? existing, out DateTime result)
    {
        var dateMatch = Regex.Match(input,
            @"(?:ngày\s*)?(\d{1,2})[/-](\d{1,2})[/-](\d{4})|ngày\s+(\d{1,2})\s+tháng\s+(\d{1,2})(?:\s+năm\s+(\d{4}))?",
            RegexOptions.IgnoreCase);
        var date = existing?.Date;
        if (dateMatch.Success)
        {
            var day = int.Parse(dateMatch.Groups[1].Success ? dateMatch.Groups[1].Value : dateMatch.Groups[4].Value);
            var month = int.Parse(dateMatch.Groups[2].Success ? dateMatch.Groups[2].Value : dateMatch.Groups[5].Value);
            var yearGroup = dateMatch.Groups[3].Success ? dateMatch.Groups[3] : dateMatch.Groups[6];
            var year = yearGroup.Success ? int.Parse(yearGroup.Value) : DateTime.Today.Year;
            try
            {
                date = new DateTime(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                result = default;
                return false;
            }
        }

        var timeMatch = Regex.Match(input,
            @"(?:lúc\s*)?(\d{1,2})\s*giờ\s*(\d{2})?\s*(sáng|chiều|tối)?|(\d{1,2})[:h](\d{2})",
            RegexOptions.IgnoreCase);
        if (timeMatch.Success)
        {
            var hour = int.Parse(timeMatch.Groups[1].Success ? timeMatch.Groups[1].Value : timeMatch.Groups[4].Value);
            var minuteGroup = timeMatch.Groups[2].Success ? timeMatch.Groups[2] : timeMatch.Groups[5];
            var minute = minuteGroup.Success ? int.Parse(minuteGroup.Value) : 0;
            var period = timeMatch.Groups[3].Value.ToLowerInvariant();
            if (period is "chiều" or "tối" && hour < 12) hour += 12;
            if (period == "sáng" && hour == 12) hour = 0;
            if (hour > 23 || minute > 59 || !date.HasValue)
            {
                result = default;
                return false;
            }
            result = date.Value.AddHours(hour).AddMinutes(minute);
            return true;
        }

        if (dateMatch.Success && date.HasValue)
        {
            result = date.Value;
            return true;
        }

        result = default;
        return false;
    }

    private static DayOfWeek? DetectRequestedWeekday(string lower)
    {
        var match = Regex.Match(lower, @"thứ\s*(hai|ba|tư|năm|sáu|bảy|2|3|4|5|6|7)");
        if (!match.Success) return null;
        return match.Groups[1].Value switch
        {
            "hai" or "2" => DayOfWeek.Monday,
            "ba" or "3" => DayOfWeek.Tuesday,
            "tư" or "4" => DayOfWeek.Wednesday,
            "năm" or "5" => DayOfWeek.Thursday,
            "sáu" or "6" => DayOfWeek.Friday,
            "bảy" or "7" => DayOfWeek.Saturday,
            _ => null
        };
    }

    private static DateTime GetNextDateOnWeekday(DateTime date, DayOfWeek weekday)
    {
        var daysUntil = ((int)weekday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(daysUntil == 0 ? 7 : daysUntil);
    }

    private static string GetWeekdayConflictPrompt(ChatbotBookingDraft draft)
    {
        var proposed = draft.AppointmentDate!.Value;
        var alternative = draft.AlternateDate!.Value;
        var time = proposed.ToString("HH:mm");
        return $"Lưu ý: ngày {proposed:dd/MM/yyyy} là {GetVietnameseWeekday(proposed.DayOfWeek)}, không phải {GetVietnameseWeekday(draft.RequestedWeekday!.Value)}. Bạn muốn giữ {GetVietnameseWeekday(proposed.DayOfWeek)} {proposed:dd/MM/yyyy} lúc {time}, hay chuyển sang {GetVietnameseWeekday(alternative.DayOfWeek)} {alternative:dd/MM/yyyy} lúc {time}?";
    }

    private static bool IsConfirmation(string lower) =>
        Regex.IsMatch(lower.Trim(), @"^(có([, ]+(tôi )?xác nhận)?|đồng ý|xác nhận|đúng|ok|oke|được)( nhé| ạ)?[.! ]*$");

    private static string GetVietnameseWeekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => string.Empty
    };

    private sealed class ChatbotBookingDraft
    {
        public string? Specialty { get; set; }
        public int? DoctorId { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public string? Reason { get; set; }
        public bool AwaitingDoctor { get; set; }
        public bool AwaitingReason { get; set; }
        public bool AwaitingConfirmation { get; set; }
        public bool DateNeedsConfirmation { get; set; }
        public DayOfWeek? RequestedWeekday { get; set; }
        public DateTime? AlternateDate { get; set; }
    }

    private async Task<GeminiCallResult?> CallOpenAiCompatibleApiAsync(
        string provider,
        List<GeminiChatMessage> history,
        List<Doctor> relevantDoctors,
        string apiKey)
    {
        var systemPrompt = $@"Bạn là Medika AI - Trợ lý Lễ tân & Điều phối (Triage) thông minh của Phòng khám Đa khoa Quốc tế Medika.
NGUYÊN TẮC BẮT BUỘC & KHÓA AN TOÀN Y TẾ:
1. Bạn là trợ lý y tế của phòng khám: hỗ trợ thông tin phòng khám, đặt lịch, định hướng chuyên khoa và trả lời kiến thức sức khỏe/thuốc phổ thông một cách thận trọng.
2. TUYỆT ĐỐI KHÔNG ĐƯỢC CHẨN ĐOÁN BỆNH (không kết luận hoặc khẳng định bệnh nhân mắc bệnh gì).
3. Với câu hỏi kiến thức thuốc (ví dụ “Paracetamol là thuốc gì?”), hãy trả lời trực tiếp thuốc thường dùng để làm gì và nêu lưu ý/rủi ro phổ biến ở mức khái quát; không từ chối chỉ vì câu hỏi nhắc đến thuốc. Không kê đơn, không chọn thuốc điều trị cho cá nhân, không đưa liều dùng cá nhân hoặc thay đổi điều trị. Nhắc người dùng đọc đúng nhãn/tờ hướng dẫn và hỏi bác sĩ/dược sĩ nếu có bệnh nền, đang dùng thuốc khác, mang thai, hỏi cho trẻ em hoặc chưa chắc cách dùng. Không khẳng định thuốc an toàn cho mọi người. Nếu nghi quá liều, phản ứng nghiêm trọng hoặc có dấu hiệu cấp cứu, hướng dẫn gọi 115/đến cơ sở y tế ngay.
4. CẢNH BÁO CẤP CỨU: Chỉ đưa cảnh báo cấp cứu khi người dùng mô tả đang có dấu hiệu nghiêm trọng rõ ràng như đau/tức ngực, khó thở nặng, bất tỉnh/hôn mê, co giật, dấu hiệu đột quỵ hoặc nôn ra máu. Không đưa cảnh báo cấp cứu chỉ vì người dùng hỏi về cảm lạnh, ho, sổ mũi, sốt nhẹ hoặc bệnh thông thường; cũng không coi câu hỏi giả định, phủ định hay nhắc đến từ khóa là người dùng đang gặp cấp cứu. Nếu không có dấu hiệu nghiêm trọng rõ ràng, hãy trả lời bình thường và khuyên đi khám khi phù hợp. Khi thực sự có dấu hiệu cấp cứu, câu đầu tiên trong phản hồi BẮT BUỘC PHẢI IN ĐẬM:
""**CẢNH BÁO: Đây có thể là tình trạng khẩn cấp, vui lòng gọi 115 hoặc đến cơ sở y tế gần nhất.**""
Sau đó hướng dẫn gọi ngay 115 hoặc Medika 0335844656.
5. Trả lời bằng tiếng Việt lịch sự, ân cần, định dạng Markdown rõ ràng.
6. Địa chỉ phòng khám: 33 Xô Viết Nghệ Tĩnh, phường Hòa Cường, thành phố Đà Nẵng; mở cửa 07:00-20:00 hằng ngày, cấp cứu 24/7, số điện thoại 0335844656. Medika tiếp nhận BHYT toàn quốc.
7. Ngày giờ hệ thống hiện tại: {DateTime.Now:yyyy-MM-dd (dddd, HH:mm)}.
8. Khi người dùng xác nhận đặt lịch khám, chỉ gọi create_appointment sau khi đã có bác sĩ và thời gian rõ ràng.";

        if (relevantDoctors.Count > 0)
        {
            var doctors = string.Join("\n", relevantDoctors.Select(d =>
                $"- ID: {d.Id} | {d.FullName} | {d.Specialty} | {d.Degree} | {d.ExperienceYears} năm kinh nghiệm | Phòng {d.Room}"));
            systemPrompt += $"\n\nBác sĩ phù hợp hiện có:\n{doctors}\nChỉ dùng ID trong danh sách này khi gọi create_appointment.";
        }

        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(item => new
        {
            role = item.Role == "model" ? "assistant" : "user",
            content = string.Join("\n", item.Parts.Select(part => part.Text))
        }));

        var tool = new
        {
            type = "function",
            function = new
            {
                name = "create_appointment",
                description = "Tạo lịch khám khi bệnh nhân đã xác nhận đặt lịch và cung cấp bác sĩ, thời gian.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        patientId = new { type = "integer", description = "ID bệnh nhân; dùng 0 nếu đang đăng nhập hoặc khách" },
                        doctorId = new { type = "integer", description = "ID bác sĩ đã chọn" },
                        date = new { type = "string", description = "Ngày giờ hẹn định dạng ISO hoặc YYYY-MM-DD HH:mm" },
                        reason = new { type = "string", description = "Lý do khám" }
                    },
                    required = new[] { "doctorId", "date", "reason" }
                }
            }
        };

        var accountId = dynamicAccountId ?? configuration["Cloudflare:AccountId"];
        var endpoint = provider == "Cloudflare"
            ? $"https://api.cloudflare.com/client/v4/accounts/{Uri.EscapeDataString(accountId ?? string.Empty)}/ai/v1/chat/completions"
            : GetChatCompletionsEndpoint(provider);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = GetActiveModel(),
            messages,
            tools = new[] { tool },
            tool_choice = "auto",
            temperature = 0.3,
            max_tokens = 800
        }), Encoding.UTF8, "application/json");

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("[{Provider} Chat Error] Status: {StatusCode}, Body: {Body}", provider, (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"The {provider} chat request failed with HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        try
        {
            var message = JsonNode.Parse(body)?["choices"]?[0]?["message"];
            var toolCall = message?["tool_calls"]?[0]?["function"];
            if (toolCall != null)
            {
                var argsText = toolCall["arguments"]?.ToString();
                return new GeminiCallResult
                {
                    IsFunctionCall = true,
                    FunctionName = toolCall["name"]?.ToString(),
                    FunctionArgs = string.IsNullOrWhiteSpace(argsText) ? null : JsonNode.Parse(argsText)
                };
            }

            return new GeminiCallResult { Text = message?["content"]?.ToString() };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Provider}] Không đọc được phản hồi chat completions", provider);
            return null;
        }
    }

    private async Task<GeminiCallResult?> CallGeminiApiAsync(List<GeminiChatMessage> history, List<Doctor> relevantDoctors, string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        var model = GetActiveModel();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        // Tối ưu hóa Context Window: Hệ thống không còn nhồi nhét toàn bộ bác sĩ vào prompt mặc định
        var systemPrompt = $@"Bạn là Medika AI - Trợ lý Lễ tân & Điều phối (Triage) thông minh của Phòng khám Đa khoa Quốc tế Medika.
NGUYÊN TẮC BẮT BUỘC & KHÓA AN TOÀN Y TẾ:
1. Bạn là trợ lý y tế của phòng khám: hỗ trợ thông tin phòng khám, đặt lịch, định hướng chuyên khoa và trả lời kiến thức sức khỏe/thuốc phổ thông một cách thận trọng.
2. TUYỆT ĐỐI KHÔNG ĐƯỢC CHẨN ĐOÁN BỆNH (không kết luận hoặc khẳng định bệnh nhân mắc bệnh gì).
3. Với câu hỏi kiến thức thuốc (ví dụ “Paracetamol là thuốc gì?”), hãy trả lời trực tiếp thuốc thường dùng để làm gì và nêu lưu ý/rủi ro phổ biến ở mức khái quát; không từ chối chỉ vì câu hỏi nhắc đến thuốc. Không kê đơn, không chọn thuốc điều trị cho cá nhân, không đưa liều dùng cá nhân hoặc thay đổi điều trị. Nhắc người dùng đọc đúng nhãn/tờ hướng dẫn và hỏi bác sĩ/dược sĩ nếu có bệnh nền, đang dùng thuốc khác, mang thai, hỏi cho trẻ em hoặc chưa chắc cách dùng. Không khẳng định thuốc an toàn cho mọi người. Nếu nghi quá liều, phản ứng nghiêm trọng hoặc có dấu hiệu cấp cứu, hướng dẫn gọi 115/đến cơ sở y tế ngay.
4. KHÓA AN TOÀN CẤP CỨU: Chỉ đưa cảnh báo cấp cứu khi người dùng mô tả đang có dấu hiệu nghiêm trọng rõ ràng như đau/tức ngực, khó thở nặng, bất tỉnh/hôn mê, co giật, dấu hiệu đột quỵ hoặc nôn ra máu. Không đưa cảnh báo cấp cứu chỉ vì người dùng hỏi về cảm lạnh, ho, sổ mũi, sốt nhẹ hoặc bệnh thông thường; cũng không coi câu hỏi giả định, phủ định hay nhắc đến từ khóa là người dùng đang gặp cấp cứu. Nếu không có dấu hiệu nghiêm trọng rõ ràng, hãy trả lời bình thường và khuyên đi khám khi phù hợp. Khi thực sự có dấu hiệu cấp cứu, câu đầu tiên trong phản hồi BẮT BUỘC PHẢI IN ĐẬM:
""**CẢNH BÁO: Đây có thể là tình trạng khẩn cấp, vui lòng gọi 115 hoặc đến cơ sở y tế gần nhất.**""
Sau đó khuyên bệnh nhân gọi ngay 115 hoặc Medika 0335844656.
5. Thông tin phòng khám: 33 Xô Viết Nghệ Tĩnh, phường Hòa Cường, thành phố Đà Nẵng; mở cửa 07:00-20:00 hằng ngày, cấp cứu 24/7, số điện thoại 0335844656. Medika tiếp nhận BHYT toàn quốc.
6. Các chuyên khoa tại phòng khám: Tim mạch, Tiêu hóa - Gan mật, Nhi khoa, Sản phụ khoa, Da liễu, Cơ xương khớp, Tai Mũi Họng, Mắt, Nội tổng quát.
7. Ngày giờ hiện tại của hệ thống: {DateTime.Now:yyyy-MM-dd (dddd, HH:mm)}.
8. Ghi nhớ ngữ cảnh các câu hỏi và câu trả lời trước đó trong cuộc hội thoại để phản hồi tự nhiên, liền mạch và chính xác.
9. Khi người dùng đồng ý chốt thời gian và bác sĩ, hãy gọi hàm create_appointment thay vì trả lời text thông thường.";

        // Nếu người dùng hỏi về chuyên khoa hoặc tìm bác sĩ, chèn 2-3 bác sĩ phù hợp nhất vào prompt
        if (relevantDoctors.Count > 0)
        {
            var doctorLines = string.Join("\n", relevantDoctors.Select(d =>
                $"- Bác sĩ ID: {d.Id} | Họ tên: {d.FullName} | Chuyên khoa: {d.Specialty} | Học vị: {d.Degree} | Kinh nghiệm: {d.ExperienceYears} năm | Phòng: {d.Room}"));
            systemPrompt += $"\n\n[DANH SÁCH BÁC SĨ PHÙ HỢP HIỆN CÓ ĐỂ TƯ VẤN & ĐẶT LỊCH]:\n{doctorLines}\n(Gợi ý bệnh nhân chọn bác sĩ và sử dụng đúng ID của bác sĩ này khi gọi hàm create_appointment).";
        }

        // Khai báo tool create_appointment cho Function Calling
        var tools = new object[]
        {
            new
            {
                function_declarations = new object[]
                {
                    new
                    {
                        name = "create_appointment",
                        description = "Tạo lịch hẹn khám bệnh mới cho bệnh nhân với bác sĩ tại phòng khám Medika khi người dùng chốt thời gian hoặc đồng ý đặt lịch.",
                        parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                patientId = new
                                {
                                    type = "INTEGER",
                                    description = "Mã ID hồ sơ bệnh nhân (truyền 0 nếu hệ thống tự động gán theo tài khoản đang đăng nhập)"
                                },
                                doctorId = new
                                {
                                    type = "INTEGER",
                                    description = "Mã ID của bác sĩ được chọn khám (lấy từ danh sách bác sĩ phù hợp được cung cấp)"
                                },
                                date = new
                                {
                                    type = "STRING",
                                    description = "Thời gian hẹn khám định dạng chuẩn ISO hoặc YYYY-MM-DD HH:mm (ví dụ: '2026-10-26 09:00')"
                                },
                                reason = new
                                {
                                    type = "STRING",
                                    description = "Lý do khám hoặc triệu chứng bệnh nhân mô tả"
                                }
                            },
                            required = new[] { "doctorId", "date", "reason" }
                        }
                    }
                }
            }
        };

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = history.Select(h => new
            {
                role = h.Role,
                parts = h.Parts.Select(p => new { text = p.Text }).ToArray()
            }).ToArray(),
            tools = tools,
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 800
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, httpContent);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var v1Url = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";
            var v1Res = await client.PostAsync(v1Url, httpContent);
            if (v1Res.IsSuccessStatusCode)
            {
                var v1String = await v1Res.Content.ReadAsStringAsync();
                return ParseGeminiResponse(v1String);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Gemini Call ERROR] StatusCode: {statusCode} ({response.ReasonPhrase})");
            Console.WriteLine($"[Gemini Call ERROR] URL: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent");
            Console.WriteLine($"[Gemini Call ERROR] Response Body:\n{errorBody}\n");
            Console.ResetColor();

            logger.LogError("[Gemini Call Error] Status: {StatusCode}, Body: {Body}", statusCode, errorBody);

            // Dự phòng nếu model endpoint không hỗ trợ system_instruction hoặc tools, gộp systemPrompt vào tin nhắn đầu
            var fallbackContents = history.Select((h, idx) => new
            {
                role = h.Role,
                parts = idx == 0 && h.Role == "user"
                    ? new[] { new { text = $"{systemPrompt}\n\n{h.Parts.FirstOrDefault()?.Text}" } }
                    : h.Parts.Select(p => new { text = p.Text }).ToArray()
            }).ToArray();

            var fallbackBody = new
            {
                contents = fallbackContents,
                generationConfig = new
                {
                    temperature = 0.3,
                    maxOutputTokens = 800
                }
            };

            var fallbackRes = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(fallbackBody), Encoding.UTF8, "application/json"));
            if (!fallbackRes.IsSuccessStatusCode)
            {
                var fallbackErr = await fallbackRes.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Gemini Fallback Call ERROR] StatusCode: {(int)fallbackRes.StatusCode} ({fallbackRes.ReasonPhrase})");
                Console.WriteLine($"[Gemini Fallback Call ERROR] Response Body:\n{fallbackErr}\n");
                Console.ResetColor();

                logger.LogError("[Gemini Fallback Call Error] Status: {StatusCode}, Body: {Body}", (int)fallbackRes.StatusCode, fallbackErr);
                return null;
            }

            var fallbackStr = await fallbackRes.Content.ReadAsStringAsync();
            return ParseGeminiResponse(fallbackStr);
        }

        var resString = await response.Content.ReadAsStringAsync();
        return ParseGeminiResponse(resString);
    }

    private static GeminiCallResult? ParseGeminiResponse(string jsonResponse)
    {
        try
        {
            var doc = JsonNode.Parse(jsonResponse);
            var parts = doc?["candidates"]?[0]?["content"]?["parts"]?.AsArray();
            if (parts == null || parts.Count == 0) return null;

            foreach (var part in parts)
            {
                if (part?["functionCall"] != null)
                {
                    var fc = part["functionCall"];
                    return new GeminiCallResult
                    {
                        IsFunctionCall = true,
                        FunctionName = fc?["name"]?.ToString(),
                        FunctionArgs = fc?["args"]
                    };
                }
            }

            var textPart = parts.FirstOrDefault(p => p?["text"] != null);
            var text = textPart?["text"]?.ToString();
            return new GeminiCallResult { Text = text };
        }
        catch
        {
            return null;
        }
    }

    private async Task<ChatbotMessageResponse> ProcessInternalFallbackAsync(string raw, string lower, int? userId, List<Doctor> doctors)
    {
        var response = new ChatbotMessageResponse
        {
            Source = "Medika Assistant"
        };

        // Chào hỏi
        if (lower is "chào" or "xin chào" or "hi" or "hello" or "alo" || lower.StartsWith("chào bạn") || lower.StartsWith("chào bác sĩ"))
        {
            response.Intent = "greeting";
            response.Reply = "Xin chào! Tôi là Trợ lý Y tế của Phòng khám Đa khoa Medika 🏥.\n\n" +
                             "Tôi có thể hỗ trợ bạn:\n" +
                             "• Tư vấn chuyên khoa và tìm kiếm bác sĩ phù hợp\n" +
                             "• Hướng dẫn đặt lịch khám và kiểm tra lịch hẹn\n" +
                             "• Giải đáp thông tin bảo hiểm y tế (BHYT) & giờ làm việc\n\n" +
                             "Bạn đang gặp vấn đề sức khỏe nào hoặc cần hỗ trợ gì hôm nay?";
            response.QuickActions = GetSuggestedPrompts();
            return response;
        }

        // 1. Tìm chuyên khoa / Triệu chứng bệnh lý (Ưu tiên cao nhất khi người dùng nói về bệnh)
        var matchedSpecialty = DetectSpecialty(lower);
        if (!string.IsNullOrEmpty(matchedSpecialty))
        {
            response.Intent = "specialty_recommendation";
            var docs = doctors.Where(d => d.Specialty.Contains(matchedSpecialty, StringComparison.OrdinalIgnoreCase)).ToList();
            if (docs.Count == 0)
            {
                docs = await context.Doctors.AsNoTracking()
                    .Where(d => d.IsActive && d.Specialty.Contains(matchedSpecialty))
                    .OrderByDescending(d => d.ExperienceYears)
                    .Take(3)
                    .ToListAsync();
            }

            response.Doctors = docs.Select(ToDoctorInfo).ToList();
            response.Reply = $"Với nhu cầu hoặc triệu chứng bạn mô tả, chuyên khoa phù hợp nhất là: **Khoa {matchedSpecialty}**.\n\n" +
                             (docs.Any()
                                 ? $"Hiện phòng khám có {docs.Count} bác sĩ chuyên khoa này sẵn sàng tiếp nhận khám:"
                                 : "Hiện bạn có thể đặt lịch khám với bác sĩ chuyên khoa này tại quầy hoặc chọn giờ khám.");

            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = $"📅 Đặt lịch khám {matchedSpecialty}", ActionType = "book", Value = matchedSpecialty },
                new() { Label = "💬 Nhắn tin với nhân viên hỗ trợ", ActionType = "support" }
            };
            return response;
        }

        // 2. Giờ làm việc & Địa chỉ phòng khám
        if (lower.Contains("giờ làm việc") || lower.Contains("mấy giờ") || lower.Contains("địa chỉ") || 
            lower.Contains("thời gian khám") || lower.Contains("ở đâu") || lower.Contains("vị trí phòng khám"))
        {
            response.Intent = "clinic_info";
            response.Reply = "🕒 THỜI GIAN LÀM VIỆC PHÒNG KHÁM MEDIKA:\n" +
                             "• Thứ Hai - Chủ Nhật: 07:00 - 20:00 (làm việc thông trưa)\n" +
                             "• Tiếp nhận cấp cứu & trực khám: 24/7\n\n" +
                             "📍 ĐỊA CHỈ: 33 Xô Viết Nghệ Tĩnh, phường Hòa Cường, thành phố Đà Nẵng.\n" +
                             "📞 Số điện thoại: 0335844656.";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám ngay", ActionType = "book" },
                new() { Label = "👨‍⚕️ Xem danh sách bác sĩ", ActionType = "specialty", Value = "" }
            };
            return response;
        }

        // 3. BHYT
        if (lower.Contains("bhyt") || lower.Contains("bảo hiểm") || lower.Contains("thẻ bảo hiểm") || lower.Contains("bảo lãnh"))
        {
            response.Intent = "insurance_info";
            response.Reply = "💳 CHÍNH SÁCH BẢO HIỂM Y TẾ (BHYT) TẠI MEDIKA:\n\n" +
                             "• Medika tiếp nhận khám chữa bệnh BHYT cho tất cả người dân trên toàn quốc, không phân biệt nơi đăng ký ban đầu.\n" +
                             "• Mức thanh toán: Đúng tuyến hưởng 80% - 100% chi phí theo danh mục BHYT quy định.\n" +
                             "• Hỗ trợ bảo lãnh viện phí trực tiếp với hơn 25 công ty bảo hiểm tư nhân (Bảo Việt, Prudential, Manulife, AIA...).\n\n" +
                             "Khi đi khám, quý khách chỉ cần mang theo CCCD gắn chip hoặc thẻ BHYT (hoặc ứng dụng VssID).";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám BHYT", ActionType = "book" }
            };
            return response;
        }

        // Bác sĩ
        if (lower.Contains("bác sĩ") || lower.Contains("danh sách bác sĩ") || lower.Contains("chuyên khoa nào"))
        {
            response.Intent = "doctors_list";
            var docs = doctors.Any() ? doctors : await context.Doctors.AsNoTracking().Where(d => d.IsActive).OrderByDescending(d => d.ExperienceYears).Take(3).ToListAsync();
            response.Doctors = docs.Take(3).Select(ToDoctorInfo).ToList();
            response.Reply = "Dưới đây là một số bác sĩ chuyên khoa tiêu biểu tại Medika. Bạn có thể nhấn vào để đặt lịch:";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám ngay", ActionType = "book" },
                new() { Label = "💬 Hỗ trợ trực tuyến", ActionType = "support" }
            };
            return response;
        }

        // Quy trình đặt lịch
        if (lower.Contains("đặt lịch") || lower.Contains("hẹn khám") || lower.Contains("đăng ký khám"))
        {
            response.Intent = "booking_guide";
            response.Reply = "📝 QUY TRÌNH ĐẶT LỊCH KHÁM TẠI MEDIKA:\n\n" +
                             "1. Chọn Chuyên khoa bạn cần khám.\n" +
                             "2. Chọn Bác sĩ mong muốn (hoặc để hệ thống chỉ định).\n" +
                             "3. Chọn Ngày & Giờ khám phù hợp.\n" +
                             "4. Nhập lý do khám và xác nhận.\n\n" +
                             "Sau khi đặt, Lễ tân sẽ xác nhận ngay. Đến ngày khám bạn chỉ cần check-in tại quầy để lấy Số thứ tự vào phòng khám!";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "👉 Mở form đặt lịch ngay", ActionType = "book" },
                new() { Label = "💬 Chat với nhân viên hỗ trợ", ActionType = "support" }
            };
            return response;
        }

        // Kiểm tra lịch cá nhân
        if (lower.Contains("lịch của tôi") || lower.Contains("kiểm tra lịch") || lower.Contains("đã đặt"))
        {
            response.Intent = "check_my_appointments";
            if (userId.HasValue && userId.Value > 0)
            {
                var upcoming = await context.Appointments
                    .Where(a => a.UserId == userId.Value && a.AppointmentDate >= DateTime.Today.AddDays(-1))
                    .OrderBy(a => a.AppointmentDate)
                    .Take(3)
                    .ToListAsync();

                if (upcoming.Any())
                {
                    var details = string.Join("\n", upcoming.Select(a =>
                        $"• Ngày: {a.AppointmentDate:dd/MM/yyyy HH:mm} - Lý do: {a.Reason} (Trạng thái: {a.Status})"));
                    response.Reply = $"Lịch khám gần đây của bạn:\n{details}\n\nBạn có thể quản lý hoặc đổi lịch ở mục 'Lịch hẹn của tôi'.";
                }
                else
                {
                    response.Reply = "Bạn hiện chưa có lịch khám nào sắp tới. Bạn có muốn đặt lịch khám mới không?";
                }
            }
            else
            {
                response.Reply = "Vui lòng đăng nhập tài khoản bệnh nhân để tôi có thể tra cứu lịch khám chính xác cho bạn nhé!";
            }
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám mới", ActionType = "book" }
            };
            return response;
        }

        // Mặc định
        response.Intent = "general_fallback";
        response.Reply = "Cảm ơn bạn đã đặt câu hỏi cho Medika AI! 💡\n\n" +
                         "Tôi có thể hỗ trợ bạn:\n" +
                         "• Tư vấn triệu chứng bệnh và đề xuất chuyên khoa phù hợp (Tim mạch, Tiêu hóa, Nhi, Sản, TMH...)\n" +
                         "• Giới thiệu bác sĩ chuyên khoa & hướng dẫn đặt lịch khám trực tuyến\n" +
                         "• Giải đáp thông tin quyền lợi BHYT, chuẩn bị trước khi khám và thời gian làm việc.\n\n" +
                         "*(Lưu ý: Quản trị viên có thể kết nối Google Gemini API Key trong trang Admin để mở khóa toàn bộ khả năng tư vấn sức khỏe AI chuyên sâu).*";
        response.QuickActions = GetSuggestedPrompts();
        return response;
    }

    public List<ChatbotQuickAction> GetSuggestedPrompts()
    {
        return new List<ChatbotQuickAction>
        {
            new() { Label = "📅 Hướng dẫn đặt lịch", ActionType = "info", Value = "Hướng dẫn đặt lịch khám" },
            new() { Label = "🩺 Tư vấn chuyên khoa", ActionType = "info", Value = "Tôi muốn tìm chuyên khoa phù hợp" },
            new() { Label = "🕒 Giờ làm việc & Địa chỉ", ActionType = "info", Value = "Giờ làm việc và địa chỉ phòng khám" },
            new() { Label = "💳 Thủ tục BHYT", ActionType = "info", Value = "Quy định khám chữa bệnh bằng BHYT" },
            new() { Label = "💬 Gặp nhân viên hỗ trợ", ActionType = "support" }
        };
    }

    private static ChatbotDoctorInfo ToDoctorInfo(Doctor d) => new()
    {
        Id = d.Id,
        FullName = d.FullName,
        Specialty = d.Specialty,
        Room = d.Room,
        Phone = d.Phone,
        Degree = d.Degree,
        ExperienceYears = d.ExperienceYears
    };

    private static string? DetectSpecialty(string input)
    {
        if (input.Contains("tim") || input.Contains("huyết áp") || input.Contains("đau ngực") || input.Contains("hồi hộp") || input.Contains("mạch"))
            return "Tim mạch";

        if (input.Contains("dạ dày") || input.Contains("tiêu hóa") || input.Contains("gan") || input.Contains("mật") || 
            input.Contains("ợ chua") || input.Contains("đau bụng") || input.Contains("đầy hơi") || input.Contains("trĩ"))
            return "Tiêu hóa - Gan mật";

        if (input.Contains("trẻ") || input.Contains("nhi") || input.Contains("em bé") || input.Contains("bé") || input.Contains("tiêm chủng"))
            return "Nhi khoa";

        if (input.Contains("phụ khoa") || input.Contains("thai") || input.Contains("sản") || input.Contains("kinh nguyệt") || input.Contains("sinh"))
            return "Sản phụ khoa";

        if (input.Contains("da") || input.Contains("mụn") || input.Contains("ngứa") || input.Contains("dị ứng") || input.Contains("mề đay") || input.Contains("nấm"))
            return "Da liễu";

        if (input.Contains("xương") || input.Contains("khớp") || input.Contains("thoái hóa") || input.Contains("cột sống") || input.Contains("mỏi gối") || input.Contains("vai gáy"))
            return "Cơ xương khớp";

        if (input.Contains("tai") || input.Contains("mũi") || input.Contains("họng") || input.Contains("viêm xoang") || input.Contains("ho lâu ngày") || input.Contains("ù tai"))
            return "Tai Mũi Họng";

        if (input.Contains("mắt") || input.Contains("nhìn mờ") || input.Contains("cận thị") || input.Contains("đỏ mắt") || input.Contains("cộm"))
            return "Mắt";

        if (input.Contains("tổng quát") || input.Contains("kiểm tra sức khỏe") || input.Contains("mệt mỏi") || input.Contains("khám sức khỏe"))
            return "Nội tổng quát";

        return null;
    }
}

public class GeminiChatMessage
{
    public string Role { get; set; } = "user";
    public List<GeminiChatPart> Parts { get; set; } = new();
}

public class GeminiChatPart
{
    public string Text { get; set; } = string.Empty;
}

public class GeminiCallResult
{
    public string? Text { get; set; }
    public bool IsFunctionCall { get; set; }
    public string? FunctionName { get; set; }
    public JsonNode? FunctionArgs { get; set; }
}

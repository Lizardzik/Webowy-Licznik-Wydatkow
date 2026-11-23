using ExpenseTracker.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ExpenseTracker.Services
{
    public class ReceiptScannerService
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiSettings _settings;

        public ReceiptScannerService(HttpClient httpClient, IOptions<GeminiSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<ScanResultDto> ExtractTransactionsAsync(
            byte[] imageBytes,
            string mimeType,
            List<Category> availableCategories)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("Brak klucza API Gemini w pliku appsettings.Secrets.json.");
            }

            string serializedPayload = BuildRequestPayload(imageBytes, mimeType, availableCategories);
            IReadOnlyList<string> models = _settings.GetEffectiveModels();
            Exception? lastTransientException = null;

            foreach (string modelName in models)
            {
                HttpResponseMessage response = await SendModelRequestAsync(modelName, serializedPayload);

                using (response)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        throw new InvalidOperationException("Nieprawidłowy klucz API Gemini lub brak uprawnień do usługi.");
                    }

                    if (IsTransientError(response.StatusCode))
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        lastTransientException = new HttpRequestException(
                            $"Model {modelName} jest chwilowo niedostępny ({(int)response.StatusCode}): {errorBody}");
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        throw new InvalidOperationException(
                            $"Model {modelName} odrzucił żądanie ({(int)response.StatusCode}): {errorBody}");
                    }

                    string responseJson = await response.Content.ReadAsStringAsync();
                    List<ScannedTransactionDto> transactions = ParseGeminiResponse(responseJson, availableCategories);

                    return new ScanResultDto
                    {
                        Transactions = transactions,
                        UsedModel = modelName
                    };
                }
            }

            throw new InvalidOperationException(
                "Wszystkie skonfigurowane modele Gemini osiągnęły limit zapytań lub są przeciążone.",
                lastTransientException);
        }

        private async Task<HttpResponseMessage> SendModelRequestAsync(string modelName, string serializedPayload)
        {
            string endpointUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
            request.Headers.Add("x-goog-api-key", _settings.ApiKey);
            request.Content = new StringContent(serializedPayload, Encoding.UTF8, "application/json");

            return await _httpClient.SendAsync(request);
        }

        private static bool IsTransientError(HttpStatusCode statusCode)
        {
            int code = (int)statusCode;
            return statusCode == HttpStatusCode.TooManyRequests || (code >= 500 && code <= 599);
        }

        private static string BuildRequestPayload(
            byte[] imageBytes,
            string mimeType,
            List<Category> availableCategories)
        {
            string base64Image = Convert.ToBase64String(imageBytes);
            string todayDate = DateTime.Today.ToString("yyyy-MM-dd");
            string categoriesDescription = string.Join(", ", availableCategories.Select(c =>
                $"Id={c.CategoryId} ({c.Title}, typ={c.Type})"));

            string prompt =
                "Przeanalizuj załączony obraz paragonu lub zrzutu ekranu z aplikacji bankowej. " +
                "Wyodrębnij wszystkie widoczne transakcje finansowe. " +
                $"Dzisiejsza data to {todayDate}. " +
                $"Dostępne kategorie użytkownika: [{categoriesDescription}]. " +
                "Dla każdej transakcji dopasuj najbardziej pasujące CategoryId z powyższej listy. " +
                "Kwotę Amount podaj jako dokładną liczbę dziesiętną większą od zera z maksymalnie dwoma miejscami po przecinku (np. 18.99), używając kropki jako separatora dziesiętnego. " +
                $"Datę Date podaj w formacie YYYY-MM-DD (jeśli na obrazie widnieje określenie typu Dzisiaj lub Wczoraj, przelicz je względem daty {todayDate}, a jeśli brakuje roku, użyj bieżącego roku). " +
                "W polu Note wpisz krótką nazwę sklepu lub tytuł przelewu po polsku. " +
                "W polu Type wpisz Expense dla wydatku lub Income dla przychodu. " +
                "Zwróć wyłącznie poprawną tablicę JSON obiektów o polach: CategoryId, Amount, Date, Note, Type.";

            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            return JsonSerializer.Serialize(requestPayload);
        }

        private static List<ScannedTransactionDto> ParseGeminiResponse(
            string responseJson,
            List<Category> availableCategories)
        {
            using JsonDocument document = JsonDocument.Parse(responseJson);
            string? rawText = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new List<ScannedTransactionDto>();
            }

            string cleanedJson = CleanMarkdownFences(rawText);
            var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<ScannedTransactionDto> extractedTransactions =
                JsonSerializer.Deserialize<List<ScannedTransactionDto>>(cleanedJson, serializerOptions)
                ?? new List<ScannedTransactionDto>();

            int fallbackCategoryId = availableCategories.FirstOrDefault()?.CategoryId ?? 0;

            foreach (ScannedTransactionDto item in extractedTransactions)
            {
                if (!availableCategories.Any(c => c.CategoryId == item.CategoryId))
                {
                    item.CategoryId = fallbackCategoryId;
                }

                if (item.Amount <= 0)
                {
                    item.Amount = 0.01m;
                }
                else
                {
                    item.Amount = Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero);
                }

                if (item.Date == default)
                {
                    item.Date = DateTime.Today;
                }
            }

            return extractedTransactions;
        }

        private static string CleanMarkdownFences(string text)
        {
            string trimmed = text.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(7);
            }
            else if (trimmed.StartsWith("```"))
            {
                trimmed = trimmed.Substring(3);
            }

            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }

            return trimmed.Trim();
        }
    }
}
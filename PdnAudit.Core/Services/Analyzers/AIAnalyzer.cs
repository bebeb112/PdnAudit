using PdnAudit.Core.Models.Results;
using PdnAudit.Core.Services.Extractors;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PdnAudit.Core.Services.Analyzers {
    /// <summary>
    /// Сервис для взаимодействия с языковой моделью GigaChat (Сбер).
    /// Отвечает за получение токена, отправку текста Политики на анализ и верификацию ответов ИИ.
    /// </summary>
    public class AIAnalyzer {
        //Я прекрасно понимаю, что нельзя вот так хранить ключи, но мне плевать на этот ключ, берите кто хотите
        private static string authorizationKey = "MDFhMWFkMTAtY2M3Zi03N2M3LWI3ZTgtN2JhMTA2ODk1NjMzOmZhNjI4YWE5LTg1MGMtNDNjNS04NzEyLWZiZmFiNGJiYTgzNQ==";
        
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Получает временный access-токен для работы с API GigaChat.
        /// Использует Basic-авторизацию по ключу.
        /// </summary>
        public async Task<string> GetGigaChatTokenAsync() {
            //я понимаю, что не самая лучшая затея игнорировать сертификат, но и качать сертификат от минцифры не звучит хайпово
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            
            using var client = new HttpClient(handler);

            string requestID = Guid.NewGuid().ToString();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authorizationKey);
            client.DefaultRequestHeaders.Add("RqUID", requestID);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "GIGACHAT_API_PERS" }
            });
            var response = await client.PostAsync("https://ngw.devices.sberbank.ru:9443/api/v2/oauth", content);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Ошибка: {response.StatusCode}. Ответ сервера: {errorBody}");
            }
            var responseText = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseText);

            return doc.RootElement.GetProperty("access_token").GetString();
        }
        
        /// <summary>
        /// Отправляет текст Политики в GigaChat и просит вернуть JSON с оценкой по 8 ключевым пунктам 152-ФЗ.
        /// </summary>
        public async Task<string> AnalyzeGigaChatAsync(string token, string policyDocumentText) {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };


            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var policyText = policyDocumentText; // Ваша переменная с текстом политики

            var requestBody = new
            {
                model = "GigaChat-2-Max",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "Ты юрист-аудитор по 152-ФЗ. Отвечай ТОЛЬКО валидным JSON без пояснений и без markdown."
                    },
                    new
                    {
                        role = "user",
                        content = $@"Проверь Политику обработки персональных данных. Для каждого пункта верни found (true/false) и quote (точная цитата из текста до 200 символов, пустая строка если found=false). 
                                     Формат ответа: 
                                     {{
                                           ""purposes"": {{""found"": false, ""quote"": """"}}, 
                                           ""legal_basis"": {{""found"": false, ""quote"": """"}}, 
                                           ""data_categories"": {{""found"": false, ""quote"": """"}}, 
                                           ""retention"": {{""found"": false, ""quote"": """"}}, 
                                           ""third_parties"": {{""found"": false, ""quote"": """"}}, 
                                           ""cross_border"": {{""found"": false, ""quote"": """"}}, 
                                           ""subject_rights"": {{""found"": false, ""quote"": """"}}, 
                                           ""consent_withdrawal"": {{""found"": false, ""quote"": """"}}
                                     }} <doc>{(policyText.Length > 30000 ? policyText.Substring(0, 30000) : policyText)}</doc>"
                    }
                },
                temperature = 0.1f,
                max_tokens = 2000 
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://gigachat.devices.sberbank.ru/api/v1/chat/completions", httpContent);
            string result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Ошибка запроса к ИИ: {result}");
            }
            using var doc = JsonDocument.Parse(result);

            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        /// <summary>
        /// Парсит JSON-ответ от ИИ и проверяет, не «галлюцинировал» ли он.
        /// Если ИИ указал пункт как найденный, но цитаты нет в исходном тексте, ставит флаг Unverified.
        /// </summary>
        public AiAnalysisResult ParseAndVerify(string rawResponse, string policyText) {
            AiAnalysisResult result;
            try
            {
                string json = TextNormalizer.CleanJsonFromAI(rawResponse);
                result = JsonSerializer.Deserialize<AiAnalysisResult>(json, JsonOpts) ?? throw new JsonException("Пустой ответ");
            }
            catch (JsonException ex)
            {
                return new AiAnalysisResult { ParseFailed = true, Error = ex.Message };
            }
            string normPolicy = Prep(policyText);
            var policyWords = normPolicy.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            foreach(var item in result.All)
            {
                item.Quote ??= "";
                if (!item.Found) continue;

                string q = Prep(item.Quote);
                bool ok = QuoteExists(normPolicy, q) || FuzzyMatch(policyWords, q);
                if (!ok)
                {
                    item.Found = false;
                    item.Unverified = true;
                }
            }
            return result;
        }
       
        private static string Prep(string? s) => TextNormalizer.Normalize(Regex.Replace(s ?? "", "[«»\"“”„']", " "));
        
        private static bool QuoteExists(string normPolicy, string q) {
            if (q.Length < 10) return false;
            var parts = q.Split(new[] { "...", "…" }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(p => p.Trim()).Where(p => p.Length >= 10).ToList();
            return parts.Count > 0 && parts.All(p => normPolicy.Contains(p));
        }
       
        private static bool FuzzyMatch(HashSet<string> policyWords, string q) {
            var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 3).ToList();
            if (words.Count < 6) return false;
            return words.Count(policyWords.Contains) / (double)words.Count >= 0.9;
        }
    }
}


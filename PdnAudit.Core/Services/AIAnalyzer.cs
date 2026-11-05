using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PdnAudit.Core.Models;

namespace PdnAudit.Core.Services {
    public class AIAnalyzer {
        private static string authorizationKey = "MDFhMWFkMTAtY2M3Zi03N2M3LWI3ZTgtN2JhMTA2ODk1NjMzOmU2MWY1NzI1LTdlNWQtNDA4Mi04ZTc5LWY2ZDFkOGQzZjliOA==";
       
        
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
        public async Task<string> AnalyzeGigaChatAsync(string token, string policyDocumentText) {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };


            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var requestBody = new
            {
                model = "GigaChat-2-Max",
                messages = new[]
                {
                    new
                    {
                        role = "user", content = $"Проанализируй текст политики на соответствие 152-ФЗ. Верни JSON: есть ли цели, сроки, передача третьим лицам. Текст: {policyDocumentText}"
                    }

                },
                temperature = 0.1f,
                max_tokens = 1000
            };
            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://gigachat.devices.sberbank.ru/api/v1/chat/completions", httpContent);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Ошибка запроса к ИИ: {await response.Content.ReadAsStringAsync()}");
            }
            var result = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(result);

            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        public AiAnalysisResult ParseAiResponse(string cleanJson) {
            var result = new AiAnalysisResult();

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                // Проверяем цели (если массив не пустой - значит true)
                if (root.TryGetProperty("цели", out var purposes))
                    result.HasPurposes = purposes.ValueKind != System.Text.Json.JsonValueKind.Null;

                // Проверяем сроки (если объект не пустой - значит true)
                if (root.TryGetProperty("сроки", out var retention))
                    result.HasRetention = retention.ValueKind != System.Text.Json.JsonValueKind.Null;

                // Проверяем передачу третьим лицам
                if (root.TryGetProperty("передача_третьим_лицам", out var thirdParty))
                    result.HasThirdParty = thirdParty.GetBoolean();
            }
            catch (Exception ex)
            {
                // Если ИИ вернул что-то не то, мы просто фиксируем ошибку
                Console.WriteLine($"Ошибка парсинга JSON: {ex.Message}");
            }

            return result;
        }

    }
}


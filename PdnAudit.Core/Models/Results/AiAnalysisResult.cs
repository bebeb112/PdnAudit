using System.Text.Json.Serialization;

namespace PdnAudit.Core.Models.Results {
    /// <summary>
    /// Модель результата анализа ИИ текста Политики обработки ПДн.
    /// Содержит оценку по каждому обязательному пункту 152-ФЗ (цели, сроки, передача третьим лицам и т.д.).
    /// </summary>
    public class AiAnalysisResult {
        // Цели обработки персональных данных
        public AiItem Purposes { get; set; } = new();

        // Правовые основания обработки
        public AiItem LegalBasis { get; set; } = new();

        // Перечень категорий обрабатываемых данных.
        public AiItem DataCategories { get; set; } = new();

        // Сроки хранения персональных данных
        public AiItem Retention { get; set; } = new();

        // Условия передачи данных третьим лицам
        public AiItem ThirdParties { get; set; } = new();

        // Признаки трансграничной передачи данных
        public AiItem CrossBorder { get; set; } = new();

        // Права субъекта персональных данных
        public AiItem SubjectRights { get; set; } = new();

        // Порядок отзыва согласия на обработку.
        public AiItem ConsentWithdrawal { get; set; } = new();

        // Флаг, указывающий, что ИИ вернул невалидный ответ (JSON не распарсился)
        [JsonIgnore] public bool ParseFailed { get; set; }

        // Текст ошибки, если анализ не удался
        [JsonIgnore] public string? Error { get; set; }

        // Все пункты одним списком
        [JsonIgnore]
        public IEnumerable<AiItem> All => new[] {
            Purposes, LegalBasis, DataCategories, Retention,
            ThirdParties, CrossBorder, SubjectRights, ConsentWithdrawal };

    }
    /// <summary>
    /// Результат проверки одного конкретного пункта Политики.
    /// Хранит статус наличия, цитату из текста и флаг необходимости ручной проверки.
    /// </summary>
    public class AiItem {
        // Указан ли данный пункт в тексте Политики
        public bool Found { get; set; }

        // Цитата из текста, подтверждающая наличие пункта
        public string Quote { get; set; } = "";

        // Флаг, указывающий, что цитату не удалось найти в исходном тексте
        [JsonIgnore] public bool Unverified { get; set; }
    }
}

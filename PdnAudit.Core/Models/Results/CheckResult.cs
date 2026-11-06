namespace PdnAudit.Core.Models.Results {
    /// <summary>
    /// Универсальная модель результата одной проверки на соответствие 152-ФЗ.
    /// Используется всеми правилами (Формы, Оператор, Cookies, ИИ) для формирования единого отчета.
    /// </summary>
    public class CheckResult {
        // Уровень критичности нарушения
        public string Severity { get; set; }

        // Краткое описание найденного нарушения или успеха
        public string Title { get; set; }

        // Уровень критичности нарушения
        public string LawReference { get; set; }

        // Рекомендация по устранению нарушения
        public string Recommendation { get; set; }

        // Категория проверки
        public string Category { get; set; } = "";

        // Дополнительные детали: где найдено, какие сервисы
        public string Details { get; set; } = "";

        /// <summary>
        /// Метод для быстрого создания результата проверки.
        /// </summary>
        public static CheckResult Create(string severity, string category, string title,
            string law, string recommendation, string details = "") => new()
            {
                Severity = severity,
                Category = category,
                Title = title,
                LawReference = law,
                Recommendation = recommendation,
                Details = details
            };
    }
    // Константы уровней критичности
    public static class Severity {
        public const string Critical = "Критично";
        public const string Warning = "Замечание";
        public const string Passed = "Пройдено";
        public const string Info = "Информация";   
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models.Results {
    public class CheckResult {
        public string Severity { get; set; }
        public string Title { get; set; }
        public string LawReference { get; set; }
        public string Recommendation { get; set; }
        public string Category { get; set; } = "";   // "Формы", "Оператор", "Cookies и аналитика"
        public string Details { get; set; } = "";    // где найдено, какие сервисы, что именно

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

    public static class Severity {
        public const string Critical = "Критично";
        public const string Warning = "Замечание";
        public const string Passed = "Пройдено";
        public const string Info = "Информация";   // в отчёте можно показывать внутри «Замечаний»
    }
}


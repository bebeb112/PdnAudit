using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    /// <summary>
    /// Модель, представляющая одну строку в визуальном отчете (для привязки к UI).
    /// Содержит краткое описание нарушения и рекомендацию по его устранению.
    /// </summary>
    public class ReportLine {
        // Краткое описание проверки или нарушения
        public string Title { get; set; }

        // Уровень критичности (Критично, Замечание, Пройдено)
        public string Severity { get; set; }

        // Цвет для визуальной индикации
        public string Color { get; set; }

        // Рекомендация по устранению нарушения (если применимо)
        public string Recommendation { get; set; }
    }
}

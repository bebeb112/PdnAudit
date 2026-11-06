using PdnAudit.Core.Models.Results;

namespace PdnAudit.Core.Services.Rules {
    /// <summary>
    /// Правила оценки результатов интеллектуального анализа Политики обработки ПДн.
    /// Преобразует ответы ИИ в структурированные результаты проверки с указанием норм закона и рекомендаций.
    /// </summary>
    public class AiAnalysisRules {

        const string Cat = "Анализ Политики (ИИ)";

        /// <summary>
        /// Проверяет результаты ИИ и формирует список нарушений/успехов.
        /// </summary>
        public static List<CheckResult> Check(AiAnalysisResult? aiResult, bool hasForeignTrackers) {
            var results = new List<CheckResult>();

            if (aiResult == null || aiResult.ParseFailed)
            {
                results.Add(CheckResult.Create(Severity.Info, Cat, "Анализ текста Политики с помощью ИИ не выполнен", "", ""));
                return results;
            }

            void Required(AiItem item, string name, string law, string rec, string sev = Severity.Warning) {
                if (item.Found)
                    results.Add(CheckResult.Create(Severity.Passed, Cat, $"В Политике указано: {name}", law, "", Note(item)));
                else
                    results.Add(CheckResult.Create(sev, Cat, $"В Политике не найдено: {name}", law, rec, Note(item)));
            }

            Required(aiResult.Purposes, "цели обработки", "ст. 5, 18.1 152-ФЗ",
            "Указать конкретные и законные цели обработки данных", Severity.Critical);
            Required(aiResult.LegalBasis, "правовые основания обработки", "ст. 6, 18.1 152-ФЗ",
                "Указать основания обработки (согласие, договор, закон)");
            Required(aiResult.DataCategories, "перечень обрабатываемых данных", "ст. 5, 18.1 152-ФЗ",
                "Перечислить категории или состав обрабатываемых данных");
            Required(aiResult.Retention, "срок хранения данных", "ст. 5, 21 152-ФЗ",
                "Указать срок хранения или условия его определения");
            Required(aiResult.SubjectRights, "права субъекта и порядок обращения", "ст. 14, 18.1 152-ФЗ",
                "Описать права субъекта и как направить запрос оператору");
            Required(aiResult.ConsentWithdrawal, "порядок отзыва согласия", "ст. 9 152-ФЗ",
                "Описать, как отозвать согласие");

            if (aiResult.ThirdParties.Found)
            {
                results.Add(CheckResult.Create(Severity.Info, Cat, "В Политике обработки данных описана передача данных третьим лицам", "ст. 6, 9 152-ФЗ", "Убедиться, что перечень получателей актуален", Note(aiResult.ThirdParties)));
            }
            else
            {
                results.Add(CheckResult.Create(Severity.Info, Cat, "Условия передачи данных третьим лицам в Политике не найдены", "ст. 6, 9 152-ФЗ", "Если данные передаются (CRM, рассылки, аналитика), описать получателей и условия", Note(aiResult.ThirdParties)));
            }

            if (aiResult.CrossBorder.Found)
            {
                results.Add(CheckResult.Create(Severity.Info, Cat, "В Политике упомянута трансграничная передача данных", "ст. 12 152-ФЗ", "Убедиться, что оператор уведомил Роскомнадзор", Note(aiResult.CrossBorder)));
            }
            else if (hasForeignTrackers)
            {
                results.Add(CheckResult.Create(Severity.Warning, Cat, "Сайт использует зарубежные сервисы, а в Политике трансграничная передача не упомянута", "ст. 12, 18.1 152-ФЗ", "Указать в Политике факт и условия трансграничной передачи или отказаться от зарубежных сервисов", Note(aiResult.CrossBorder)));
            }

            return results;
        }

        /// <summary>
        /// Формирует текстовую сноску с цитатой или пометкой о необходимости ручной проверки.
        /// </summary>
        private static string Note(AiItem item) {
            if (item.Found)
            {
                return $"Цитата: '{item.Quote}'";
            }
            else if (item.Unverified)
            {
                return "ИИ указал на наличие пункта, но цитата в тексте не подтверждена. Необходимо проверить вручную";
            }
            else
            {
                return "Автоматически не обнаружено";
            }
        }
        
    }
}

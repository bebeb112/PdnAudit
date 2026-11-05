using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services.Rules {
    public class AiAnalysisRules {
        public static List<CheckResult> Check(AiAnalysisResult aiResult) {
            var results = new List<CheckResult>();

            if (aiResult == null)
            {
                results.Add(new CheckResult
                {
                    Severity = Severity.Info,
                    Title = "Анализ текста Политики с помощью ИИ не выполнен"
                });
                return results;
            }

            // 1. Проверка целей обработки
            if (!aiResult.HasPurposes)
            {
                results.Add(new CheckResult
                {
                    Severity = Severity.Warning,
                    Title = "ИИ: В тексте Политики не указаны цели обработки ПДн"
                });
            }
            else
            {
                results.Add(new CheckResult
                {
                    Severity = Severity.Passed,
                    Title = "ИИ: Цели обработки ПДн указаны"
                });
            }

            // 2. Проверка сроков хранения
            if (!aiResult.HasRetention)
            {
                results.Add(new CheckResult
                {
                    Severity = Severity.Warning,
                    Title = "ИИ: В тексте Политики не указаны сроки хранения ПДн"
                });
            }
            else
            {
                results.Add(new CheckResult
                {
                    Severity = Severity.Passed,
                    Title = "ИИ: Сроки хранения ПДн указаны"
                });
            }

            // 3. Проверка передачи третьим лицам
            if (aiResult.HasThirdParty)
            {
                results.Add(new CheckResult
                {
                    Severity = Severity.Info,
                    Title = "ИИ: В Политике указана передача данных третьим лицам"
                });
            }

            return results;
        }
    }
}

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
            const string Cat = "Анализ Политики (ИИ)";

            if (aiResult == null)
            {
                results.Add(CheckResult.Create(Severity.Info, Cat, "Анализ текста Политики с помощью ИИ не выполнен", "", ""));
                return results;
            }

            if (!aiResult.HasPurposes)
                results.Add(CheckResult.Create(Severity.Warning, Cat, "ИИ: В тексте Политики не указаны цели обработки ПДн", "ст. 5, 18.1 152-ФЗ", "Указать конкретные цели сбора данных"));
            else
                results.Add(CheckResult.Create(Severity.Passed, Cat, "ИИ: Цели обработки ПДн указаны", "", ""));

            if (!aiResult.HasRetention)
                results.Add(CheckResult.Create(Severity.Warning, Cat, "ИИ: В тексте Политики не указаны сроки хранения ПДн", "ст. 5, 21 152-ФЗ", "Прописать сроки хранения или условия их определения"));
            else
                results.Add(CheckResult.Create(Severity.Passed, Cat, "ИИ: Сроки хранения ПДн указаны", "", ""));

            if (aiResult.HasThirdParty)
                results.Add(CheckResult.Create(Severity.Info, Cat, "ИИ: В Политике указана передача данных третьим лицам", "ст. 6 152-ФЗ", "Убедиться, что перечень третьих лиц актуален"));

            return results;
        }
    }
}

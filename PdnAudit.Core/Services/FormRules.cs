using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services {
    public class FormRules {
        public static List<CheckResult> CheckForms(List<FormInfo> forms) {
            List<CheckResult> results = new List<CheckResult>();
            foreach (var form in forms)
            {
                if (!form.HasPdnFields) continue;
                if(!form.HasConsentCheckbox && !form.HasImplicitConsentText)
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Критично",
                        Title = "Отсутствует согласие на обработку ПДн",
                        LawReference = "ст. 9 152-ФЗ",
                        Recommendation = "Добавить чекбокс согласия или текст рядом с кнопкой"
                    });
                }
                if (form.HasConsentCheckbox && form.ConsentCheckedByDefault)
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Критично",
                        Title = "Чекбокс согласия отмечен по умолчанию",
                        LawReference = "ст. 9 152-ФЗ",
                        Recommendation = "Убрать атрибут checked у чекбокса"
                    });
                }
                if (!form.NearbyPrivacyLink)
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Замечание",
                        Title = "Рядом с формой нет ссылки на Политику обработки персональных данных",
                        LawReference = "ст. 18.1, ч.2 152-ФЗ",
                        Recommendation = "Разместить ссылку на Политику прямо в форме"
                    });
                }
                if (!form.NearbyConsentLink)
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Замечание",
                        Title = "Согласие выражено только текстом без чекбокса",
                        LawReference = "ст. 9 152-ФЗ",
                        Recommendation = "Добавить чекбокс, чтобы согласие было конкретным и осознанным"
                    });
                }
                if (IsExternal(form))
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Замечание",
                        Title = "Action ведет на другой домен",
                        LawReference = "ст. 6,9 152-ФЗ",
                        Recommendation = "Добавить чекбокс, чтобы согласие было конкретным и осознанным"
                    });
                }
                
            }
            return results;

        }
        private static bool IsExternal(FormInfo form) {
            if (!string.IsNullOrWhiteSpace(form.ActionUrl))
            {
                var actionHost = new Uri(form.ActionUrl).Host;
                var currentHost = new Uri(form.BasePageUrl).Host;

                if (!actionHost.Contains(currentHost)) return true;
            }
            return false;
        }
        
    }
}

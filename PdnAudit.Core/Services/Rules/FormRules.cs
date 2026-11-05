using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services.Rules {
    public class FormRules {
        public static List<CheckResult> CheckForms(List<FormInfo> forms) {

            List<CheckResult> results = new List<CheckResult>();

            foreach (var form in forms)
            {
                if (!form.HasPdnFields) continue;

                bool isLogin = form.FormType == "Авторизация";
                if(!isLogin && !form.HasConsentCheckbox && !form.HasImplicitConsentText)
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
                if (!isLogin && !form.NearbyPrivacyLink)
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Замечание",
                        Title = "Рядом с формой нет ссылки на Политику обработки персональных данных",
                        LawReference = "ст. 18.1, ч.2 152-ФЗ",
                        Recommendation = "Разместить ссылку на Политику прямо в форме"
                    });
                }
                if (!isLogin && !form.HasConsentCheckbox && form.HasImplicitConsentText)
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Замечание",
                        Title = "Согласие выражено только текстом без чекбокса",
                        LawReference = "ст. 9 152-ФЗ",
                        Recommendation = "Добавить чекбокс, чтобы согласие было конкретным и осознанным"
                    });
                }
                if (!isLogin && (form.HasConsentCheckbox || form.HasImplicitConsentText) && !form.NearbyConsentLink)
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Замечание",
                        Title = "Рядом с формой нет ссылки на текст согласия",
                        LawReference = "ст. 9 152-ФЗ",
                        Recommendation = "Добавить ссылку на документ согласия"
                    });
                }
                if (IsExternal(form))
                {
                    results.Add(new CheckResult
                    {
                        Severity = "Замечание",
                        Title = "Action ведет на другой домен",
                        LawReference = "ст. 6,9 152-ФЗ",
                        Recommendation = "Убедиться, что получатель данных указан в Политике и согласии, а передача возможна с согласия субъекта"
                    });
                }
                
            }
            return results;

        }
        //Разобрать че это за фигня
        private static bool IsExternal(FormInfo form) {
            if (!Uri.TryCreate(form.ActionUrl, UriKind.Absolute, out var action)) return false;
            if (!Uri.TryCreate(form.BasePageUrl, UriKind.Absolute, out var current)) return false;

            static string Root(string h) => h.StartsWith("www.") ? h[4..] : h;
            var a = Root(action.Host);
            var c = Root(current.Host);

            return !(a == c || a.EndsWith("." + c) || c.EndsWith("." + a));
        }
        
    }
}

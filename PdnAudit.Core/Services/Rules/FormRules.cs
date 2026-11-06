using PdnAudit.Core.Models.Results;
using PdnAudit.Core.Models.Scanning;

namespace PdnAudit.Core.Services.Rules {
    /// <summary>
    /// Правила проверки форм на соответствие 152-ФЗ.
    /// Анализирует наличие чекбоксов согласия, ссылок на Политику и проверяет, куда отправляются данные.
    /// </summary>
    public class FormRules {

        /// <summary>
        /// Проверяет список найденных форм и возвращает список нарушений/успехов.
        /// </summary>
        public static List<CheckResult> CheckForms(List<FormInfo> forms) {
            var results = new List<CheckResult>();
            const string Cat = "Формы и сбор данных";

            foreach (var form in forms)
            {
                if (!form.HasPdnFields) continue;
                bool isLogin = form.FormType == "Авторизация";

                if (!isLogin && !form.HasConsentCheckbox && !form.HasImplicitConsentText)
                {
                    results.Add(CheckResult.Create(Severity.Critical, Cat,
                        "Отсутствует согласие на обработку ПДн",
                        "ст. 9 152-ФЗ",
                        "Добавить чекбокс согласия или текст рядом с кнопкой"));
                }
                if (form.HasConsentCheckbox && form.ConsentCheckedByDefault)
                {
                    results.Add(CheckResult.Create(Severity.Critical, Cat,
                        "Чекбокс согласия отмечен по умолчанию",
                        "ст. 9 152-ФЗ",
                        "Убрать атрибут checked у чекбокса"));
                }
                if (!isLogin && !form.NearbyPrivacyLink)
                {
                    results.Add(CheckResult.Create(Severity.Warning, Cat,
                        "Рядом с формой нет ссылки на Политику обработки персональных данных",
                        "ст. 18.1, ч.2 152-ФЗ",
                        "Разместить ссылку на Политику прямо в форме"));
                }
                if (!isLogin && !form.HasConsentCheckbox && form.HasImplicitConsentText)
                {
                    results.Add(CheckResult.Create(Severity.Warning, Cat,
                        "Согласие выражено только текстом без чекбокса",
                        "ст. 9 152-ФЗ",
                        "Добавить чекбокс, чтобы согласие было конкретным и осознанным"));
                }
                if (!isLogin && (form.HasConsentCheckbox || form.HasImplicitConsentText) && !form.NearbyConsentLink)
                {
                    results.Add(CheckResult.Create(Severity.Warning, Cat,
                        "Рядом с формой нет ссылки на текст согласия",
                        "ст. 9 152-ФЗ",
                        "Добавить ссылку на документ согласия"));
                }
                if (IsExternal(form))
                {
                    results.Add(CheckResult.Create(Severity.Warning, Cat,
                        "Action ведет на другой домен",
                        "ст. 6, 9 152-ФЗ",
                        "Убедиться, что получатель данных указан в Политике и согласии"));
                }
            }
            return results;
        }

        /// <summary>
        /// Проверяет, отправляет ли форма данные на другой домен (не совпадающий с текущим сайтом).
        /// </summary>
        private static bool IsExternal(FormInfo form) {
            if (!System.Uri.TryCreate(form.ActionUrl, System.UriKind.Absolute, out var action)) return false;
            if (!System.Uri.TryCreate(form.BasePageUrl, System.UriKind.Absolute, out var current)) return false;
            static string Root(string h) => h.StartsWith("www.") ? h[4..] : h;
            var a = Root(action.Host);
            var c = Root(current.Host);
            return !(a == c || a.EndsWith("." + c) || c.EndsWith("." + a));
        }
    }
}

using PdnAudit.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services {
    public static class OperatorRules {
        private const string Cat = "Информация об операторе";

        public static List<CheckResult> Check(OperatorInfo op) {
            var results = new List<CheckResult>();

            // 1. Наименование
            if (string.IsNullOrWhiteSpace(op.Name))
                results.Add(CheckResult.Create(Severity.Critical, Cat,
                    "Не найдено наименование оператора персональных данных",
                    "ст. 18.1, ч. 2; ст. 22.1 152-ФЗ",
                    "Указать полное наименование организации (или ФИО индивидуального предпринимателя) в футере, на странице «Контакты» и в Политике",
                    "Автоматически не найдено на проверенных страницах"));
            else
                results.Add(CheckResult.Create(Severity.Passed, Cat,
                    "Наименование оператора указано", "ст. 18.1 152-ФЗ", "",
                    $"{op.Name} (источник: {Src(op, "Наименование")})"));

            // 2. Реквизиты (ИНН, ОГРН)
            if (string.IsNullOrEmpty(op.INN))
                results.Add(CheckResult.Create(Severity.Warning, Cat,
                    "Не найден ИНН оператора", "ст. 18.1, 22.1 152-ФЗ",
                    "Указать ИНН и ОГРН (ОГРНИП) на странице «Контакты» или «Реквизиты»"));
            else if (!op.IsINNValid)
                results.Add(CheckResult.Create(Severity.Warning, Cat,
                    "ИНН не проходит проверку контрольной суммы", "ст. 18.1 152-ФЗ",
                    "Проверить правильность ИНН на сайте",
                    $"Найдено: {op.IsINNValid} (источник: {Src(op, "ИНН")})"));
            else
                results.Add(CheckResult.Create(Severity.Passed, Cat,
                    "ИНН указан и корректен", "ст. 18.1 152-ФЗ", "",
                    $"{op.IsINNValid} (источник: {Src(op, "ИНН")})"));

            if (string.IsNullOrEmpty(op.OGRN))
                results.Add(CheckResult.Create(Severity.Warning, Cat,
                    "Не найден ОГРН/ОГРНИП оператора", "ст. 18.1 152-ФЗ",
                    "Указать ОГРН (ОГРНИП) рядом с ИНН"));

            // 3. Адрес
            if (string.IsNullOrWhiteSpace(op.Address))
                results.Add(CheckResult.Create(Severity.Warning, Cat,
                    "Не найден адрес оператора", "ст. 18.1, ч. 2; ст. 14 152-ФЗ",
                    "Указать юридический или почтовый адрес оператора"));

            // 4. Каналы для обращений субъектов ПДн
            if (string.IsNullOrWhiteSpace(op.Email) && string.IsNullOrWhiteSpace(op.Phone))
                results.Add(CheckResult.Create(Severity.Warning, Cat,
                    "Не найдены контакты для обращений субъектов ПДн (email, телефон)",
                    "ст. 14, 20, 21 152-ФЗ",
                    "Указать email или телефон, по которым можно отправить запрос или отозвать согласие"));
            else
                results.Add(CheckResult.Create(Severity.Passed, Cat,
                    "Указаны контакты оператора", "ст. 14 152-ФЗ", "",
                    $"Email: {op.Email ?? "-"}; телефон: {op.Phone ?? "-"}"));

            return results;
        }

        private static string Src(OperatorInfo op, string key) =>
            op.Sources.TryGetValue(key, out var url) ? url : "-";
    }
}

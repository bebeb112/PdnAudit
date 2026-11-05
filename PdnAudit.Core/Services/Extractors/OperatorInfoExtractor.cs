using Microsoft.Playwright;
using PdnAudit.Core.Models.Operator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services.Extractors {
    public class OperatorInfoExtractor {
        private const RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase;
        private static readonly Regex InnRx = new(
        @"ИНН(?:\s*/\s*КПП)?\s*[:№\-]?\s*(\d{12}|\d{10})(?!\d)(?:\s*/\s*(\d{9}))?", options);
        private static readonly Regex OgrnRx = new(@"ОГРН(?:ИП)?\s*[:№\-]?\s*(\d{15}|\d{13})(?!\d)", options);
        private static readonly Regex KppRx = new(@"КПП\s*[:№\-]?\s*(\d{9})(?!\d)", options);

        private static readonly Regex NameRx = new(
            @"(?:(?:ООО|АО|ПАО|ЗАО|ОАО|НКО)|Общество\s+с\s+ограниченной\s+ответственностью|Акционерное\s+общество)\s*[«""“„][^»""”“\n]{2,80}[»""”]" +
            @"|(?:ИП|Индивидуальный\s+предприниматель)\s+[А-ЯЁ][а-яё]+\s+[А-ЯЁ][а-яё]+(?:\s+[А-ЯЁ][а-яё]+)?",
            RegexOptions.Compiled);

        private static readonly Regex AddressRx = new(
            @"(?:юридический\s+адрес|юр\.?\s*адрес|почтовый\s+адрес|адрес)\s*[:\-–]?\s*((?:\d{6}\s*,?\s*)?[А-ЯЁ][^\n]{10,160})", options);
        private static readonly Regex IndexAddressRx = new(@"\b\d{6},\s*[А-ЯЁа-яё][^\n]{10,150}", options);

        private static readonly Regex EmailRx = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", options);
        private static readonly Regex PhoneRx = new(
            @"(?:\+7|8)[\s\-]*\(?\d{3}\)?[\s\-]*\d{3}[\s\-]*\d{2}[\s\-]*\d{2}", options);
        public static void Extract(string? rawText, string sourceUrl, OperatorInfo info) {
            string text = Clean(rawText);
            if (text.Length == 0) return;

            // ИНН (предпочитаем тот, у которого верна контрольная сумма)
            if (info.INN == null || !info.IsINNValid)
            {
                foreach (Match m in InnRx.Matches(text))
                {
                    string inn = m.Groups[1].Value;
                    bool valid = IsInnValid(inn);
                    if (info.INN == null || valid && !info.IsINNValid)
                    {
                        info.INN = inn;
                        info.IsINNValid = valid;
                        info.Sources["ИНН"] = sourceUrl;
                        if (m.Groups[2].Success && info.KPP == null)
                        {
                            info.KPP = m.Groups[2].Value;
                            info.Sources["КПП"] = sourceUrl;
                        }
                    }
                    if (valid) break;
                }
            }

            if (info.OGRN == null && OgrnRx.Match(text) is { Success: true } og)
            { info.OGRN = og.Groups[1].Value; info.Sources["ОГРН"] = sourceUrl; }

            if (info.KPP == null && KppRx.Match(text) is { Success: true } kp)
            { info.KPP = kp.Groups[1].Value; info.Sources["КПП"] = sourceUrl; }

            if (info.Name == null && NameRx.Match(text) is { Success: true } nm)
            { info.Name = nm.Value.Trim(); info.Sources["Наименование"] = sourceUrl; }

            if (info.Address == null)
            {
                var am = AddressRx.Match(text);
                string? addr = am.Success ? am.Groups[1].Value : IndexAddressRx.Match(text) is { Success: true } im ? im.Value : null;
                if (addr != null) { info.Address = addr.Trim(); info.Sources["Адрес"] = sourceUrl; }
            }

            if (info.Email == null)
            {
                foreach (Match m in EmailRx.Matches(text))
                {
                    if (Regex.IsMatch(m.Value, @"\.(png|jpe?g|svg|webp|gif)$", RegexOptions.IgnoreCase)) continue;
                    info.Email = m.Value; info.Sources["Email"] = sourceUrl; break;
                }
            }

            if (info.Phone == null && PhoneRx.Match(text) is { Success: true } ph)
            { info.Phone = ph.Value.Trim(); info.Sources["Телефон"] = sourceUrl; }
        }

        public static bool IsInnValid(string inn) {
            static int Calc(string s, int[] k) { int sum = 0; for (int i = 0; i < k.Length; i++) sum += (s[i] - '0') * k[i]; return sum % 11 % 10; }

            if (inn.Length == 10)
                return Calc(inn, new[] { 2, 4, 10, 3, 5, 9, 4, 6, 8 }) == inn[9] - '0';
            if (inn.Length == 12)
                return Calc(inn, new[] { 7, 2, 4, 10, 3, 5, 9, 4, 6, 8 }) == inn[10] - '0'
                    && Calc(inn, new[] { 3, 7, 2, 4, 10, 3, 5, 9, 4, 6, 8 }) == inn[11] - '0';
            return false;
        }

        // Убираем невидимые символы, но сохраняем регистр и переводы строк
        private static string Clean(string? t) {
            if (string.IsNullOrEmpty(t)) return "";
            t = Regex.Replace(t, "[\u00AD\u200B-\u200D\uFEFF]", "");
            t = Regex.Replace(t, "[ \t\u00A0\u202F]+", " ");
            t = Regex.Replace(t, @"\s*\n\s*", "\n");
            return t.Trim();
        }
}
}

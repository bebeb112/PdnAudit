using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PdnAudit.Core.Services.Extractors {
    public class TextNormalizer {
        private static readonly char[] invisibleChars =
        {
            '\u00AD', // мягкий перенос (&shy;)
            '\u200B', // пробел нулевой ширины
            '\u200C', // нулевой разделитель
            '\u200D', // нулевой соединитель
            '\uFEFF'  // BOM / неразрывный нулевой пробел
        };
        private static readonly Regex whiteSpaceRegex = new(@"\s+", RegexOptions.Compiled);
        public static string Normalize(string? text) {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var sb = new StringBuilder(text.Length);
            foreach(char c in text)
            {
                if(Array.IndexOf(invisibleChars, c) >= 0)
                {
                    continue;
                }
                if(c == '\u00A0' || c == '\u202F')
                {
                    sb.Append(' ');
                    continue;
                }
                sb.Append(c);
            }
            string result = whiteSpaceRegex.Replace(sb.ToString(), " ").Trim();
            return result.ToLowerInvariant();
        }
        public static string CleanJsonFromAI(string aiResponse) {
            var match = Regex.Match(aiResponse, @"\{.*\}", RegexOptions.Singleline);
            if (match.Success)
            {
                return match.Value;
            }
            return aiResponse;
        }
    }
}

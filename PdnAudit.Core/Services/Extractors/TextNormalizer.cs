using System.Text;
using System.Text.RegularExpressions;

namespace PdnAudit.Core.Services.Extractors {
    /// <summary>
    /// Служебный класс для нормализации текста.
    /// Удаляет невидимые символы, приводит пробелы к единому виду и извлекает JSON из ответов ИИ.
    /// </summary>
    public class TextNormalizer {

        // Список невидимых символов, которые нужно удалять (мягкие переносы, пробелы нулевой ширины и т.д.)
        private static readonly char[] invisibleChars =
        {
            '\u00AD', // мягкий перенос (&shy;)
            '\u200B', // пробел нулевой ширины
            '\u200C', // нулевой разделитель
            '\u200D', // нулевой соединитель
            '\uFEFF'  // BOM / неразрывный нулевой пробел
        };
        private static readonly Regex whiteSpaceRegex = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Очищает текст от невидимых символов, заменяет все виды пробелов на обычный пробел, 
        /// удаляет лишние пробелы и приводит текст к нижнему регистру.
        /// </summary>
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

        /// <summary>
        /// Извлекает чистый JSON-объект из ответа ИИ, удаляя markdown-разметку и лишний текст.
        /// </summary>
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

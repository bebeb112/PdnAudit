using PdnAudit.Core.Models.Tracking;

namespace PdnAudit.Core.Services.Dictionaries {
    /// <summary>
    /// Правило классификации для стороннего домена (трекера).
    /// </summary>
    public record TrackerRule(string Pattern, string Name, string Category, bool IsForeign);

    /// <summary>
    /// Справочник известных сторонних сервисов (аналитика, реклама, виджеты) и префиксов их cookies.
    /// Используется для классификации сетевых запросов и оценки рисков по 152-ФЗ.
    /// </summary>
    public class TrackerDictionary {

        // Список известных доменов-трекеров
        public static readonly TrackerRule[] Domains =
    {
        new("mc.yandex.ru", "Яндекс.Метрика", TrackerCategory.Analytics, false),
        new("top-fwz1.mail.ru", "Top.Mail.ru", TrackerCategory.Analytics, false),
        new("top.mail.ru", "Top.Mail.ru", TrackerCategory.Analytics, false),
        new("vk.com", "VK (пиксель/виджеты)", TrackerCategory.Ads, false),
        new("google-analytics.com", "Google Analytics", TrackerCategory.Analytics, true),
        new("analytics.google.com", "Google Analytics", TrackerCategory.Analytics, true),
        new("googletagmanager.com", "Google Tag Manager", TrackerCategory.Analytics, true),
        new("hotjar.com", "Hotjar", TrackerCategory.Analytics, true),
        new("clarity.ms", "Microsoft Clarity", TrackerCategory.Analytics, true),
        new("facebook.net", "Meta Pixel", TrackerCategory.Ads, true),
        new("facebook.com", "Meta (Facebook)", TrackerCategory.Ads, true),
        new("doubleclick.net", "Google Ads", TrackerCategory.Ads, true),
        new("googleadservices.com", "Google Ads", TrackerCategory.Ads, true),
        new("analytics.tiktok.com", "TikTok Pixel", TrackerCategory.Ads, true),
        new("jivosite.com", "JivoSite", TrackerCategory.Widget, false),
        new("jivo.ru", "JivoSite", TrackerCategory.Widget, false),
        new("tawk.to", "Tawk.to", TrackerCategory.Widget, true),
        new("intercom.io", "Intercom", TrackerCategory.Widget, true),
        new("livechatinc.com", "LiveChat", TrackerCategory.Widget, true),
        new("youtube.com", "YouTube", TrackerCategory.Video, true),
        new("youtube-nocookie.com", "YouTube", TrackerCategory.Video, true),
        new("vimeo.com", "Vimeo", TrackerCategory.Video, true),
        new("fonts.googleapis.com", "Google Fonts", TrackerCategory.Resource, true),
        new("fonts.gstatic.com", "Google Fonts", TrackerCategory.Resource, true),
        new("maps.googleapis.com", "Google Maps", TrackerCategory.Resource, true),
        new("cdnjs.cloudflare.com", "Cloudflare CDN", TrackerCategory.Resource, true),
        new("cdn.jsdelivr.net", "jsDelivr CDN", TrackerCategory.Resource, true),
        new("unpkg.com", "unpkg CDN", TrackerCategory.Resource, true),
    };

        // Список известных префиксов cookies для определения сервиса, который их установил
        public static readonly (string Prefix, string Name, string Category)[] CookiePrefixes =
        {
        ("_ga", "Google Analytics", TrackerCategory.Analytics),
        ("_gid", "Google Analytics", TrackerCategory.Analytics),
        ("_gcl_", "Google Ads", TrackerCategory.Ads),
        ("_ym", "Яндекс.Метрика", TrackerCategory.Analytics),
        ("_fbp", "Meta Pixel", TrackerCategory.Ads),
        ("_fbc", "Meta Pixel", TrackerCategory.Ads),
        ("_hj", "Hotjar", TrackerCategory.Analytics),
        ("_clck", "Microsoft Clarity", TrackerCategory.Analytics),
        ("_clsk", "Microsoft Clarity", TrackerCategory.Analytics),
        ("tmr_", "Top.Mail.ru", TrackerCategory.Analytics),
        ("_tt_", "TikTok Pixel", TrackerCategory.Ads),
    };

        /// <summary>
        /// Ищет правило для указанного хоста (с учетом поддоменов).
        /// </summary>
        public static TrackerRule? MatchHost(string host) =>
            Domains.FirstOrDefault(r => host == r.Pattern || host.EndsWith("." + r.Pattern));

        /// <summary>
        /// Определяет сервис по имени cookie (по префиксу).
        /// </summary>
        public static (string Name, string Category)? MatchCookie(string name) {
            foreach (var c in CookiePrefixes)
                if (name.StartsWith(c.Prefix, StringComparison.OrdinalIgnoreCase)) return (c.Name, c.Category);
            return null;
        }

        /// <summary>
        /// Проверяет, относятся ли два хоста к одному сайту (сравнивает корневые домены).
        /// Приблизительно: сравниваем последние две части хоста (для .co.uk и т.п. будет неточно).
        /// </summary>
        public static bool IsSameSite(string a, string b) {
            static string Root(string h) {
                var p = h.TrimStart('.').ToLowerInvariant().Split('.');
                return p.Length <= 2 ? string.Join('.', p) : string.Join('.', p[^2..]);
            }
            return Root(a) == Root(b);
        }

        // <summary>
        /// Определяет, является ли домен зарубежным (не в зоне .ru, .su, .рф).
        /// </summary>
        public static bool LooksForeign(string host) =>
            !(host.EndsWith(".ru") || host.EndsWith(".su") || host.EndsWith(".рф") || host.EndsWith(".xn--p1ai"));
    }
}

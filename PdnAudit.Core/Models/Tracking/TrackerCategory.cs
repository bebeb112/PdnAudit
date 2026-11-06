namespace PdnAudit.Core.Models.Tracking {
    /// <summary>
    /// Константы для классификации сторонних сервисов и cookies, обнаруженных на сайте.
    /// Используются для формирования правил оценки рисков (аналитика, реклама, виджеты и т.д.).
    /// </summary>
    public static class TrackerCategory {
        public const string Analytics = "Аналитика";
        public const string Ads = "Реклама / пиксели";
        public const string Widget = "Чаты и виджеты";
        public const string Video = "Видео";
        public const string Resource = "Шрифты, карты, CDN";
        public const string Other = "Прочий сторонний домен";
        public const string Technical = "Технические / прочие";
    }
}

namespace PdnAudit.Core.Models.Tracking {
    /// <summary>
    /// Модель, описывающая найденный на сайте баннер об использовании файлов cookie.
    /// Содержит информацию о его тексте и наличии кнопок выбора (Принять/Отклонить).
    /// </summary>
    public class CookieBannerInfo {
        // Флаг: найден ли баннер на странице
        public bool Found { get; set; }

        // Текст уведомления о cookies
        public string Text { get; set; } = "";

        // Флаг: найдена ли кнопка согласия
        public bool AcceptFound { get; set; }

        // Текст кнопки согласия
        public string AcceptText { get; set; } = "";

        // Флаг: есть ли кнопка отказа от cookies
        public bool HasReject { get; set; }

        // Флаг: есть ли кнопка настройки cookies
        public bool HasSettings { get; set; }

        // Флаг: есть ли в баннере ссылка на Политику обработки ПДн или Cookie-политику
        public bool HasPolicyLink { get; set; }
    }
}

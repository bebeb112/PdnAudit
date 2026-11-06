namespace PdnAudit.Core.Models.Tracking {
    /// <summary>
    /// Модель, описывающая результаты одного этапа анализа трекеров (например, «до согласия» или «после согласия»).
    /// Содержит сетевые запросы, установленные cookies и информацию о баннере.
    /// </summary>
    public class TrackingPhase {
        // Флаг: было ли на данном этапе нажато согласие на использование cookies
        public bool AcceptedConsent { get; set; }

        // Список сетевых запросов, зафиксированных на этом этапе
        public List<RequestInfo> Requests { get; set; } = new();

        // Список cookies, установленных на этом этапе
        public List<CookieInfo> Cookies { get; set; } = new();

        // Информация о найденном баннере cookies на этом этапе
        public CookieBannerInfo Banner { get; set; } = new();

        // Список ошибок, возникших в процессе сбора данных на этом этапе
        public List<string> Errors { get; set; } = new();
    }

}

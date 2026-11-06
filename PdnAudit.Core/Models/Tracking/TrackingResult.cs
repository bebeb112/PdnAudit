namespace PdnAudit.Core.Models.Tracking {
    /// <summary>
    /// Главная модель результатов анализа трекеров и cookies.
    /// Содержит данные двух этапов: до нажатия кнопки согласия и после.
    /// </summary>
    public class TrackingResult {
        // Результаты сбора данных до согласия пользователя (чистый контекст)
        public TrackingPhase Before { get; set; } = new();

        // Результаты сбора данных после нажатия кнопки «Принять» (повторный заход)
        public TrackingPhase After { get; set; } = new();   
    }
}

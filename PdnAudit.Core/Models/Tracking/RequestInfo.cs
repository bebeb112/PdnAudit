namespace PdnAudit.Core.Models.Tracking {
    // <summary>
    /// Модель, описывающая сетевой запрос, отправленный браузером при загрузке сайта.
    /// Используется для обнаружения сторонних трекеров (аналитика, реклама) и оценки трансграничной передачи данных.
    /// </summary>
    public class RequestInfo {
        // Домен (хост), на который был отправлен запрос
        public string Host { get; set; } = "";

        // Пример полного URL запроса
        public string ExampleUrl { get; set; } = "";

        // Тип ресурса (script, image, xhr, fetch и т.д.)
        public string ResourceType { get; set; } = "";

        // Название сервиса, которому принадлежит домен
        public string ServiceName { get; set; } = "";

        // Категория сервиса (Аналитика, Реклама, Виджеты, CDN и т.д.)
        public string Category { get; set; } = "";

        // Флаг: является ли сервис зарубежным
        public bool IsForeign { get; set; }

        // Количество запросов к данному домену
        public int Count { get; set; }
    }
}

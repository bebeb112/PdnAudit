namespace PdnAudit.Core.Models.Tracking {
    /// <summary>
    /// Модель, описывающая конкретный файл cookie, установленный на сайте.
    /// Используется для классификации cookies (технические, аналитические, рекламные) и выявления сторонних сервисов.
    /// </summary>
    public class CookieInfo {
        // Имя cookie
        public string Name { get; set; } = "";

        // Домен, для которого установлен cookie
        public string Domain { get; set; } = "";

        // Дата и время истечения срока действия
        public DateTimeOffset? Expires { get; set; }   // null = сессионная

        // лаг: является ли cookie сторонним (установлен не доменом сайта, а внешним сервисом)
        public bool IsThirdParty { get; set; }

        // Название сервиса, которому принадлежит cookie
        public string ServiceName { get; set; } = "";

        // Категория cookie (Аналитика, Реклама, Технические и т.д.)
        public string Category { get; set; } = "";
    }
}

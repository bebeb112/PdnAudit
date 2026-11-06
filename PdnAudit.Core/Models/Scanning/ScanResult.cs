namespace PdnAudit.Core.Models.Scanning {
    /// <summary>
    /// Главная модель, содержащая результаты первичного сканирования сайта.
    /// Хранит базовую информацию о странице, найденные ссылки и формы.
    /// </summary>
    public class ScanResult {
        // Исходный URL-адрес проверяемого сайта.
        public string Url { get; set; }

        // Заголовок главной страницы
        public string PageTitle { get; set; }

        // Список всех найденных ссылок на стартовой странице
        public List<string> Links { get; set; } = new List<string>();

        // Список форм, обнаруженных на сайте
        public List<FormInfo> Forms { get; set; } = new List<FormInfo>();

        // Флаг, указывающий, что формы не были найдены автоматически. 
        // Если true, пользователю рекомендуется проверить сайт вручную
        public bool NeedsManualCheck { get; set; }
    }
}

namespace PdnAudit.Core.Models.Scanning {
    /// <summary>
    /// Модель для передачи статуса текущего этапа сканирования из ядра (Core) в интерфейс (WPF).
    /// Используется совместно с интерфейсом IProgress для обновления экрана загрузки.
    /// </summary>
    public class ScanProgress {
        // Текстовое описание текущего действия
        public string Message { get; set; }

        // Конструктор, принимающий сообщение о текущем этапе
        public ScanProgress(string message) => Message = message;
    }
}

namespace PdnAudit.Core.Models.Scanning {
    /// <summary>
    /// Модель найденного юридического документа на сайте.
    /// Хранит ссылку на документ, его тип и извлеченный текст для последующего анализа.
    /// </summary>
    public class Document {
        // Полный URL-адрес, по которому расположен документ
        public string Url { get; set; }

        // Тип документа
        public string DocumentType { get; set; }

        // Полный текст документа, извлеченный со страницы
        public string FullText { get; set; }

    }
}

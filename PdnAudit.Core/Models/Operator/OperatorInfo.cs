namespace PdnAudit.Core.Models.Operator {

    /// <summary>
    /// Модель данных, содержащая найденную информацию об операторе персональных данных (компании или ИП).
    /// Используется для хранения реквизитов, контактов и источников, откуда эти данные были извлечены.
    /// </summary>
    public class OperatorInfo {
        // Наименование организации или ФИО ИП
        public string? Name { get; set; }

        // ИНН оператора
        public string? INN { get; set; }

        // Флаг, указывающий, прошел ли ИНН проверку контрольной суммы
        public bool IsINNValid { get; set; }

        // ОГРН или ОГРНИП
        public string? OGRN { get; set; }

        // КПП (Код причины постановки на учет)
        public string? KPP { get; set; }

        // Юридический или фактический адрес
        public string? Address { get; set; }

        // Контактный email для обращений субъектов ПДн
        public string? Email { get; set; }

        // Контактный телефон для обращений субъектов ПДн
        public string? Phone { get; set; }

        // Словарь, хранящий источники найденных данных.
        // Ключ — название поля (например, "ИНН", "Email"), значение — URL страницы, где оно было найдено.
        public Dictionary<string, string> Sources { get; } = new();
    }
}

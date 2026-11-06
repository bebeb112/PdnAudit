namespace PdnAudit.Core.Models.Scanning {
    /// <summary>
    /// Модель найденной формы на сайте (регистрация, вход, обратная связь и т.д.).
    /// Содержит информацию о полях ввода, наличии чекбокса согласия и ссылках на юридические документы.
    /// </summary>
    public class FormInfo {
        // URL страницы, на которой расположена форма
        public string BasePageUrl { get; set; }

        // URL, на который отправляются данные формы
        public string ActionUrl { get; set; }

        // Тип формы (Авторизация, Регистрация, Оформление заказа, Обратная связь и т.д.)
        public string FormType { get; set; }

        // Флаг: найден ли чекбокс согласия на обработку ПДн.
        public bool HasConsentCheckbox { get; set; }

        // Флаг: отмечен ли чекбокс согласия по умолчанию
        public bool ConsentCheckedByDefault { get; set; }

        // Флаг: есть ли рядом с формой ссылка на Политику обработки ПДн
        public bool NearbyPrivacyLink { get; set; }

        // Флаг: есть ли рядом с формой ссылка на отдельное Согласие
        public bool NearbyConsentLink { get; set; }

        // Флаг: найден ли текст неявного согласия ("Нажимая кнопку, вы соглашаетесь...")
        public bool HasImplicitConsentText { get; set; }

        // Текст кнопки отправки формы
        public string SubmitTextNearby { get; set; }

        // Список полей ввода, найденных в форме
        public List<FormField> Fields { get; set; } = new List<FormField>();

        /// <summary>
        /// Вычисляет, собирает ли данная форма персональные данные.
        /// Анализирует типы полей (email, tel, password) и ключевые слова в их названиях/подсказках.
        public bool HasPdnFields { 
            get {
                if (Fields == null || Fields.Count == 0) return false;

                string[] pdnKeywords = { "имя", "фио", "телефон", "почта", "email", "phone", "name", "tel", "mail", "адрес", "address" };

                foreach (var field in Fields)
                {
                    if (field.Type == "email" || field.Type == "tel" || field.Type == "password")
                    {
                        return true;
                    }
                    string searchString = $"{field.Name} {field.Placeholder} {field.Label}".ToLower();

                    foreach (var keyword in pdnKeywords)
                    {
                        if (searchString.Contains(keyword))
                        {
                            return true;
                        }
                    }
                }

                return false;
            } 
        }
    }
    /// <summary>
    /// Модель одного поля ввода внутри формы.
    /// </summary>
    public class FormField {
        // HTML-тип поля (text, email, tel, password и т.д.)
        public string Type { get; set; }

        // Атрибут name или id поля
        public string Name { get; set; }

        // Текст метки, связанной с полем
        public string Label { get; set; }

        // Текст-подсказка внутри поля
        public string Placeholder { get; set; }
    }
}

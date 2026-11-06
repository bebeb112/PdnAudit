using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models.Scanning {
    public class FormInfo {
        public string BasePageUrl { get; set; }
        public string ActionUrl { get; set; }
        public string FormType { get; set; }
        public bool HasConsentCheckbox { get; set; }
        public bool ConsentCheckedByDefault { get; set; }
        public string ConsentText { get; set; }
        public bool NearbyPrivacyLink { get; set; }
        public bool NearbyConsentLink { get; set; }
        public bool HasImplicitConsentText { get; set; }
        public string SubmitTextNearby { get; set; }
        public List<FormField> Fields { get; set; } = new List<FormField>();
        public bool HasPdnFields { 
            get {
                if (Fields == null || Fields.Count == 0) return false;

                // Список ключевых слов, которые указывают на сбор ПДн
                string[] pdnKeywords = { "имя", "фио", "телефон", "почта", "email", "phone", "name", "tel", "mail", "адрес", "address" };

                foreach (var field in Fields)
                {
                    // 1. Проверяем тип поля
                    if (field.Type == "email" || field.Type == "tel" || field.Type == "password")
                    {
                        return true;
                    }

                    // 2. Проверяем имя (name) и подсказку (placeholder) на наличие ключевых слов
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
    public class FormField {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
        public string Placeholder { get; set; }
    }
}

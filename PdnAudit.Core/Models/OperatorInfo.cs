using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    public class OperatorInfo {
        public string? Name { get; set; }
        public string? INN { get; set; }
        public bool IsINNValid { get; set; }
        public string? OGRN { get; set; }
        public string? KPP { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        // Ключ - поле класса, значение - url страницы, где найдено
        public Dictionary<string, string> Sources { get; } = new();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Документы - Согласие на обработку персональных данных, Политика обработки персональных данных
namespace PdnAudit.Core.Models.Scanning {
    public class Document {
        public string Url { get; set; }
        public string DocumentType { get; set; }
        public string FullText { get; set; }
        public bool HasINN { get; set; }
        public bool HasPurposes { get; set; }
        public bool HasRetentionPeriod { get; set; }
    }
}

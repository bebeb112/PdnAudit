using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Снимок сайта - все элементы сайта, которые нужно проверить на соответствие требованиям 152-ФЗ
namespace PdnAudit.Core.Models {
    public class PageSnapshot {
        public string Url { get; set; }
        public DateTime ScanTime { get; set; }
        public List<PageSnapshot> Pages { get; set; }
        public List<Document> Documents { get; set; }
    }
}

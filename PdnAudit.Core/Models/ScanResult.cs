using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    public class ScanResult {
        public string Url { get; set; }
        public string PageTitle { get; set; }
        public List<string> Links { get; set; } = new List<string>();
        public List<string> Forms { get; set; } = new List<string>();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models.Scanning {
    public class ScanResult {
        public string Url { get; set; }
        public string PageTitle { get; set; }
        public List<string> Links { get; set; } = new List<string>();
        public List<FormInfo> Forms { get; set; } = new List<FormInfo>();
        public bool NeedsManualCheck { get; set; }
    }
}

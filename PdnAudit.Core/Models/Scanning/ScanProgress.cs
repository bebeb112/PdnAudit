using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models.Scanning {
    public class ScanProgress {
        public string Message { get; set; }
        public ScanProgress(string message) => Message = message;
    }
}

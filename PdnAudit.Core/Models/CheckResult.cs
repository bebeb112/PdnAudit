using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    public class CheckResult {
        public string Severity { get; set; }
        public string Title { get; set; }
        public string LawReference { get; set; }
        public string Recommendation { get; set; }

    }
}

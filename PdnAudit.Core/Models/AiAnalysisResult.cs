using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    public class AiAnalysisResult {
        public bool HasPurposes { get; set; }
        public bool HasRetention { get; set; }
        public bool HasThirdParty { get; set; }
    }
}

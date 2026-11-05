using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    public class CookieBannerInfo {
        public bool Found { get; set; }
        public string Text { get; set; } = "";
        public bool AcceptFound { get; set; }
        public string AcceptText { get; set; } = "";
        public bool HasReject { get; set; }
        public bool HasSettings { get; set; }
        public bool HasPolicyLink { get; set; }
    }
}

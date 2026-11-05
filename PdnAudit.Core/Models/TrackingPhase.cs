using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    public class TrackingPhase {
        public bool AcceptedConsent { get; set; }
        public List<RequestInfo> Requests { get; set; } = new();
        public List<CookieInfo> Cookies { get; set; } = new();
        public CookieBannerInfo Banner { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

}

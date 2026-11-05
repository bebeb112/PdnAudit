using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models.Tracking {
    public class CookieInfo {
        public string Name { get; set; } = "";
        public string Domain { get; set; } = "";
        public DateTimeOffset? Expires { get; set; }   // null = сессионная
        public bool IsThirdParty { get; set; }
        public string ServiceName { get; set; } = "";
        public string Category { get; set; } = "";
    }
}

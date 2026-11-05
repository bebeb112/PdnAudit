using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models {
    public class RequestInfo {
        public string Host { get; set; } = "";
        public string ExampleUrl { get; set; } = "";
        public string ResourceType { get; set; } = "";
        public string ServiceName { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsForeign { get; set; }
        public int Count { get; set; }
    }
}

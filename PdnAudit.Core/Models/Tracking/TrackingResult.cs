using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models.Tracking {
    public class TrackingResult {
        public TrackingPhase Before { get; set; } = new();   // до согласия
        public TrackingPhase After { get; set; } = new();    // после нажатия «Принять»
    }
}

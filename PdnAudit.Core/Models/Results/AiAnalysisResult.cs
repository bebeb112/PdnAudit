using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PdnAudit.Core.Models.Results {
    public class AiAnalysisResult {
        public AiItem Purposes { get; set; } = new();
        public AiItem LegalBasis { get; set; } = new();
        public AiItem DataCategories { get; set; } = new();
        public AiItem Retention { get; set; } = new();
        public AiItem ThirdParties { get; set; } = new();
        public AiItem CrossBorder { get; set; } = new();
        public AiItem SubjectRights { get; set; } = new();
        public AiItem ConsentWithdrawal { get; set; } = new();

        [JsonIgnore] public bool ParseFailed { get; set; }
        [JsonIgnore] public string? Error { get; set; }
        [JsonIgnore]
        public IEnumerable<AiItem> All => new[] {
            Purposes, LegalBasis, DataCategories, Retention,
            ThirdParties, CrossBorder, SubjectRights, ConsentWithdrawal };

    }
    public class AiItem {
        public bool Found { get; set; }
        public string Quote { get; set; } = "";
        [JsonIgnore] public bool Unverified { get; set; }
    }
}

using System;

namespace FreePackages {
	internal sealed class PromotionEvidence {
		internal const string TrustedSource = "steamdb-free-promotions";

		public EPackageType TargetType { get; set; }
		public uint TargetID { get; set; }
		public string Source { get; set; } = "";
		public string SourceUrl { get; set; } = "";
		public DateTime ObservedAtUtc { get; set; }
		public DateTime? PromotionEndsAtUtc { get; set; }
		public bool IsExplicitFreeToKeep { get; set; }
		public bool PermanentLicenceConfirmed { get; set; }

		public bool HasValidShape() =>
			TargetID > 0 &&
			(TargetType == EPackageType.App || TargetType == EPackageType.Sub) &&
			Source == TrustedSource &&
			IsExplicitFreeToKeep &&
			PermanentLicenceConfirmed &&
			Uri.TryCreate(SourceUrl, UriKind.Absolute, out Uri? uri) &&
			uri.Scheme == Uri.UriSchemeHttps &&
			uri.Host.Equals("steamdb.info", StringComparison.OrdinalIgnoreCase);
	}
}

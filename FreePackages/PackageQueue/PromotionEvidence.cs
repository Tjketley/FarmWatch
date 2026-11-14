using System;

namespace FreePackages {
	internal sealed class PromotionEvidence {
		internal const string TrustedSource = "steam-pics-freepromotion";

		public EPackageType TargetType { get; set; }
		public uint TargetID { get; set; }
		public string Source { get; set; } = "";
		public string SourceUrl { get; set; } = "";
		public DateTime ObservedAtUtc { get; set; }
		public DateTime? PromotionEndsAtUtc { get; set; }
		public bool IsExplicitFreeToKeep { get; set; }
		public bool PermanentLicenceConfirmed { get; set; }
		public bool IsOwnedGameFreeDlc { get; set; }
		public uint ParentAppID { get; set; }

		public bool HasValidShape() =>
			TargetID > 0 &&
			((TargetType == EPackageType.Sub && !IsOwnedGameFreeDlc && ParentAppID == 0) || (TargetType == EPackageType.App && IsOwnedGameFreeDlc && ParentAppID > 0)) &&
			Source == TrustedSource &&
			IsExplicitFreeToKeep &&
			PermanentLicenceConfirmed &&
			ObservedAtUtc.Kind == DateTimeKind.Utc &&
			ObservedAtUtc <= DateTime.UtcNow &&
			Uri.TryCreate(SourceUrl, UriKind.Absolute, out Uri? uri) &&
			uri.Scheme == Uri.UriSchemeHttps &&
			uri.Host.Equals("steamdb.info", StringComparison.OrdinalIgnoreCase) &&
			uri.Port == 443 &&
			string.IsNullOrEmpty(uri.UserInfo) &&
			uri.AbsolutePath == (TargetType == EPackageType.Sub ? $"/sub/{TargetID}/" : $"/app/{TargetID}/") &&
			string.IsNullOrEmpty(uri.Query) &&
			string.IsNullOrEmpty(uri.Fragment);
	}
}

using System;

namespace FreePackages {
	internal sealed class PromotionEvidence {
		internal const string TrustedSource = "steam-pics-freepromotion";
		internal const string TrustedDlcSource = "steam-pics-free-dlc";

		public EPackageType TargetType { get; set; }
		public uint TargetID { get; set; }
		public string Source { get; set; } = "";
		public string SourceUrl { get; set; } = "";
		public DateTime ObservedAtUtc { get; set; }
		public DateTime? PromotionEndsAtUtc { get; set; }
		public bool IsExplicitFreeToKeep { get; set; }
		public bool PermanentLicenceConfirmed { get; set; }
		public bool IsOwnedGameFreeDlc { get; set; }
		public bool IsExplicitFreeDlc { get; set; }
		public uint ParentAppID { get; set; }

		public bool HasValidShape() {
			if (TargetID == 0 || ObservedAtUtc.Kind != DateTimeKind.Utc || ObservedAtUtc > DateTime.UtcNow ||
				!Uri.TryCreate(SourceUrl, UriKind.Absolute, out Uri? uri) ||
				uri.Scheme != Uri.UriSchemeHttps ||
				!uri.Host.Equals("steamdb.info", StringComparison.OrdinalIgnoreCase) ||
				uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) ||
				!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) {
				return false;
			}

			if (TargetType == EPackageType.Sub) {
				return !IsOwnedGameFreeDlc && !IsExplicitFreeDlc && ParentAppID == 0 &&
					Source == TrustedSource && IsExplicitFreeToKeep && PermanentLicenceConfirmed &&
					uri.AbsolutePath == $"/sub/{TargetID}/";
			}

			if (TargetType == EPackageType.App) {
				return IsOwnedGameFreeDlc && IsExplicitFreeDlc && !IsExplicitFreeToKeep &&
					!PermanentLicenceConfirmed && ParentAppID > 0 && Source == TrustedDlcSource &&
					uri.AbsolutePath == $"/app/{TargetID}/";
			}

			return false;
		}
	}
}
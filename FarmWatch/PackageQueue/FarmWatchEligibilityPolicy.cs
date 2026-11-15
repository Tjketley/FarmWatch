namespace FreePackages {
	internal static class FarmWatchEligibilityPolicy {
		// A package is eligible only when Steam explicitly marks it as Free-to-Keep
		// and it contains exactly one genuinely paid Game app.
		internal static bool IsFreeToKeepPackage(FilterablePackage package) {
			if (!package.FreePromotion || package.FreeWeekend) {
				return false;
			}

			if (package.LicenseType != SteamKit2.ELicenseType.SinglePurchase || package.BillingType != EBillingType.NoCost) {
				return false;
			}

			if (package.PackageContents.Count != 1) {
				return false;
			}

			FilterableApp app = package.PackageContents[0];
			return app.Type == EAppType.Game && !app.IsFreeApp;
		}
	}
}

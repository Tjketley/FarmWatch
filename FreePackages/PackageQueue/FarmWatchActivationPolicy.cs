namespace FreePackages {
	internal static class FarmWatchActivationPolicy {
		internal static bool ShouldObserveOnly(bool observeOnly, bool globalAutoClaimEnabled, bool botAutoClaimEnabled) =>
			observeOnly || !globalAutoClaimEnabled || !botAutoClaimEnabled;

		// The shared final gate permits only verified permanent game promotions or
		// verified free DLC for an already-owned game. Playtests are never eligible.
		internal static bool ShouldActivate(bool observeOnly, bool globalAutoClaimEnabled, bool botAutoClaimEnabled, EPackageType packageType, bool hasCurrentEvidence) =>
			packageType is EPackageType.App or EPackageType.Sub &&
			hasCurrentEvidence &&
			!ShouldObserveOnly(observeOnly, globalAutoClaimEnabled, botAutoClaimEnabled);
	}
}
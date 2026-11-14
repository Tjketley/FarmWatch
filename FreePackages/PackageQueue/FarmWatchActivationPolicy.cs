namespace FreePackages {
	internal static class FarmWatchActivationPolicy {
		internal static bool ShouldObserveOnly(bool observeOnly, bool globalAutoClaimEnabled, bool botAutoClaimEnabled) =>
			observeOnly || !globalAutoClaimEnabled || !botAutoClaimEnabled;
	}
}

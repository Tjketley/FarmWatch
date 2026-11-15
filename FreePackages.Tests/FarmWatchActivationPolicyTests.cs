using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreePackages.Tests;

[TestClass]
public sealed class FarmWatchActivationPolicyTests {
	[TestMethod]
	public void DefaultsToObserveOnly() {
		Assert.IsTrue(FarmWatchActivationPolicy.ShouldObserveOnly(true, false, false));
	}

	[TestMethod]
	public void ObserveOnlyAlwaysBlocksActivation() {
		Assert.IsTrue(FarmWatchActivationPolicy.ShouldObserveOnly(true, true, true));
	}

	[TestMethod]
	public void GlobalKillSwitchBlocksActivation() {
		Assert.IsTrue(FarmWatchActivationPolicy.ShouldObserveOnly(false, false, true));
	}

	[TestMethod]
	public void PerBotSwitchBlocksActivation() {
		Assert.IsTrue(FarmWatchActivationPolicy.ShouldObserveOnly(false, true, false));
	}

	[TestMethod]
	public void OnlyExplicitlyEnabledNonObserveModeCanActivate() {
		Assert.IsFalse(FarmWatchActivationPolicy.ShouldObserveOnly(false, true, true));
	}

	[TestMethod]
	public void FinalGateAllowsVerifiedFreeToKeepPackageWhenExplicitlyEnabled() {
		Assert.IsTrue(FarmWatchActivationPolicy.ShouldActivate(false, true, true, EPackageType.Sub, true));
	}

	[TestMethod]
	public void FinalGateAllowsVerifiedOwnedGameFreeDlcWhenExplicitlyEnabled() {
		Assert.IsTrue(FarmWatchActivationPolicy.ShouldActivate(false, true, true, EPackageType.App, true));
	}

	[TestMethod]
	public void FinalGateRejectsFreeToKeepWithoutCurrentEvidence() {
		Assert.IsFalse(FarmWatchActivationPolicy.ShouldActivate(false, true, true, EPackageType.Sub, false));
	}

	[TestMethod]
	public void FinalGateRejectsOwnedGameFreeDlcWithoutCurrentEvidence() {
		Assert.IsFalse(FarmWatchActivationPolicy.ShouldActivate(false, true, true, EPackageType.App, false));
	}

	[TestMethod]
	public void FinalGateRejectsPlaytestsEvenWithEvidenceAndAllSwitchesEnabled() {
		Assert.IsFalse(FarmWatchActivationPolicy.ShouldActivate(false, true, true, EPackageType.Playtest, true));
	}

	[TestMethod]
	public void FinalGateRejectsActivationInObserveOnlyMode() {
		Assert.IsFalse(FarmWatchActivationPolicy.ShouldActivate(true, true, true, EPackageType.App, true));
	}

	[TestMethod]
	public void FinalGateRejectsActivationWhenGlobalSwitchIsOff() {
		Assert.IsFalse(FarmWatchActivationPolicy.ShouldActivate(false, false, true, EPackageType.Sub, true));
	}

	[TestMethod]
	public void FinalGateRejectsActivationWhenBotSwitchIsOff() {
		Assert.IsFalse(FarmWatchActivationPolicy.ShouldActivate(false, true, false, EPackageType.App, true));
	}
}
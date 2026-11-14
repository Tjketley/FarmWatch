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
}

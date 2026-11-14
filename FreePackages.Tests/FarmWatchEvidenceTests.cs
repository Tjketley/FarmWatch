using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreePackages.Tests;

[TestClass]
public sealed class FarmWatchEvidenceTests {
	[TestMethod]
	public void AcceptsOnlySteamDbFreeToKeepEvidenceShape() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.Sub,
			TargetID = 12345,
			Source = PromotionEvidence.TrustedSource,
			SourceUrl = "https://steamdb.info/sub/12345/",
			ObservedAtUtc = DateTime.UtcNow,
			IsExplicitFreeToKeep = true,
			PermanentLicenceConfirmed = true
		};

		Assert.IsTrue(evidence.HasValidShape());
	}

	[TestMethod]
	public void RejectsNonSteamDbEvidenceSource() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.Sub,
			TargetID = 12345,
			Source = PromotionEvidence.TrustedSource,
			SourceUrl = "https://example.com/sub/12345/",
			ObservedAtUtc = DateTime.UtcNow,
			IsExplicitFreeToKeep = true,
			PermanentLicenceConfirmed = true
		};

		Assert.IsFalse(evidence.HasValidShape());
	}

	[TestMethod]
	public void RejectsEvidenceWithoutPermanentLicenceConfirmation() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.App,
			TargetID = 12345,
			Source = PromotionEvidence.TrustedSource,
			SourceUrl = "https://steamdb.info/app/12345/",
			ObservedAtUtc = DateTime.UtcNow,
			IsExplicitFreeToKeep = true,
			PermanentLicenceConfirmed = false
		};

		Assert.IsFalse(evidence.HasValidShape());
	}

	[TestMethod]
	public void RejectsPlaytestEvidence() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.Playtest,
			TargetID = 12345,
			Source = PromotionEvidence.TrustedSource,
			SourceUrl = "https://steamdb.info/app/12345/",
			ObservedAtUtc = DateTime.UtcNow,
			IsExplicitFreeToKeep = true,
			PermanentLicenceConfirmed = true
		};

		Assert.IsFalse(evidence.HasValidShape());
	}
}

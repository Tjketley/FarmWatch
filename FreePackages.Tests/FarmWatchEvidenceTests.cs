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
	public void RejectsAppIdOnlyEvidenceEvenWhenMarkedPermanent() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.App,
			TargetID = 12345,
			Source = PromotionEvidence.TrustedSource,
			SourceUrl = "https://steamdb.info/app/12345/",
			ObservedAtUtc = DateTime.UtcNow,
			IsExplicitFreeToKeep = true,
			PermanentLicenceConfirmed = true
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

	[TestMethod]
	public void AcceptsFreeDlcEvidenceForOwnedGameShape() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.App,
			TargetID = 54321,
			Source = PromotionEvidence.TrustedDlcSource,
			SourceUrl = "https://steamdb.info/app/54321/",
			ObservedAtUtc = DateTime.UtcNow,
			IsOwnedGameFreeDlc = true,
			IsExplicitFreeDlc = true,
			ParentAppID = 12345
		};

		Assert.IsTrue(evidence.HasValidShape());
	}

	[TestMethod]
	public void OwnedGameFreeDlcFactoryProducesEvidenceAcceptedByFinalGate() {
		BotCache cache = new();
		PromotionEvidence evidence = PromotionEvidence.ForOwnedGameFreeDlc(54321, 12345, DateTime.UtcNow);

		Assert.IsTrue(evidence.HasValidShape());
		Assert.IsTrue(cache.AddPromotionEvidence(evidence));
		Assert.IsTrue(cache.HasCurrentClaimEvidence(new Package(EPackageType.App, 54321)));
	}

	[TestMethod]
	public void RejectsAppEvidenceWithoutOwnedGameDlcClassification() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.App,
			TargetID = 54321,
			Source = PromotionEvidence.TrustedDlcSource,
			SourceUrl = "https://steamdb.info/app/54321/",
			ObservedAtUtc = DateTime.UtcNow,
			IsExplicitFreeDlc = true,
			ParentAppID = 12345
		};

		Assert.IsFalse(evidence.HasValidShape());
	}

	[TestMethod]
	public void RejectsFreeDlcEvidenceWithoutParentGame() {
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.App,
			TargetID = 54321,
			Source = PromotionEvidence.TrustedDlcSource,
			SourceUrl = "https://steamdb.info/app/54321/",
			ObservedAtUtc = DateTime.UtcNow,
			IsOwnedGameFreeDlc = true,
			IsExplicitFreeDlc = true
		};

		Assert.IsFalse(evidence.HasValidShape());
	}
	[TestMethod]
	public void RejectsStaleOwnedGameFreeDlcEvidenceAtFinalGate() {
		BotCache cache = new();
		PromotionEvidence evidence = PromotionEvidence.ForOwnedGameFreeDlc(54321, 12345, DateTime.UtcNow.AddMinutes(-6));

		Assert.IsTrue(cache.AddPromotionEvidence(evidence));
		Assert.IsFalse(cache.HasCurrentClaimEvidence(new Package(EPackageType.App, 54321)));
	}

	[TestMethod]
	public void RejectsExpiredFreeToKeepEvidenceAtFinalGate() {
		BotCache cache = new();
		PromotionEvidence evidence = new() {
			TargetType = EPackageType.Sub,
			TargetID = 12345,
			Source = PromotionEvidence.TrustedSource,
			SourceUrl = "https://steamdb.info/sub/12345/",
			ObservedAtUtc = DateTime.UtcNow,
			PromotionEndsAtUtc = DateTime.UtcNow.AddMinutes(-1),
			IsExplicitFreeToKeep = true,
			PermanentLicenceConfirmed = true
		};

		Assert.IsTrue(cache.AddPromotionEvidence(evidence));
		Assert.IsFalse(cache.HasCurrentClaimEvidence(new Package(EPackageType.Sub, 12345)));
	}

}

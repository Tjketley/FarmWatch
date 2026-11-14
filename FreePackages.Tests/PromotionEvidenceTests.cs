using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreePackages.Tests;

[TestClass]
public sealed class PromotionEvidenceTests {
	private static PromotionEvidence CreateEvidence(DateTime observedAt, DateTime? endsAt = null) => new() {
		TargetType = EPackageType.Sub,
		TargetID = 12345,
		Source = PromotionEvidence.TrustedSource,
		SourceUrl = "https://steamdb.info/sub/12345/",
		ObservedAtUtc = observedAt,
		PromotionEndsAtUtc = endsAt,
		IsExplicitFreeToKeep = true,
		PermanentLicenceConfirmed = true
	};

	[TestMethod]
	public void AcceptsFreshMatchingEvidence() {
		BotCache cache = new();
		cache.PromotionEvidence.Add(CreateEvidence(DateTime.UtcNow));
		Assert.IsTrue(cache.HasCurrentFreeToKeepEvidence(new Package(EPackageType.Sub, 12345)));
	}

	[TestMethod]
	public void RejectsStaleEvidence() {
		BotCache cache = new();
		cache.PromotionEvidence.Add(CreateEvidence(DateTime.UtcNow.AddMinutes(-6)));
		Assert.IsFalse(cache.HasCurrentFreeToKeepEvidence(new Package(EPackageType.Sub, 12345)));
	}

	[TestMethod]
	public void RejectsExpiredPromotionEvidence() {
		BotCache cache = new();
		cache.PromotionEvidence.Add(CreateEvidence(DateTime.UtcNow, DateTime.UtcNow.AddSeconds(-1)));
		Assert.IsFalse(cache.HasCurrentFreeToKeepEvidence(new Package(EPackageType.Sub, 12345)));
	}

	[TestMethod]
	public void RejectsEvidenceForDifferentPackage() {
		BotCache cache = new();
		cache.PromotionEvidence.Add(CreateEvidence(DateTime.UtcNow));
		Assert.IsFalse(cache.HasCurrentFreeToKeepEvidence(new Package(EPackageType.Sub, 99999)));
	}

	[TestMethod]
	public void RejectsUntrustedEvidenceUrl() {
		BotCache cache = new();
		PromotionEvidence evidence = CreateEvidence(DateTime.UtcNow);
		evidence.SourceUrl = "https://steamdb.example/sub/12345/";
		Assert.IsFalse(cache.AddPromotionEvidence(evidence));
	}

	[TestMethod]
	public void RejectsEvidenceWithFutureTimestamp() {
		BotCache cache = new();
		PromotionEvidence evidence = CreateEvidence(DateTime.UtcNow.AddMinutes(1));
		Assert.IsFalse(cache.AddPromotionEvidence(evidence));
	}

	[TestMethod]
	public void RejectsEvidenceForAppQueueEntry() {
		BotCache cache = new();
		cache.PromotionEvidence.Add(CreateEvidence(DateTime.UtcNow));
		Assert.IsFalse(cache.HasCurrentFreeToKeepEvidence(new Package(EPackageType.App, 12345)));
	}
	[TestMethod]
	public void RejectsMalformedPersistedEvidenceAtFinalGate() {
		BotCache cache = new();
		PromotionEvidence evidence = CreateEvidence(DateTime.UtcNow);
		evidence.SourceUrl = "https://steamdb.info/sub/99999/?redirect=1";
		cache.PromotionEvidence.Add(evidence);
		Assert.IsFalse(cache.HasCurrentFreeToKeepEvidence(new Package(EPackageType.Sub, 12345)));
	}

	[TestMethod]
	public void RejectsPersistedEvidenceWithNonUtcTimestampAtFinalGate() {
		BotCache cache = new();
		PromotionEvidence evidence = CreateEvidence(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local));
		cache.PromotionEvidence.Add(evidence);
		Assert.IsFalse(cache.HasCurrentFreeToKeepEvidence(new Package(EPackageType.Sub, 12345)));
	}

}

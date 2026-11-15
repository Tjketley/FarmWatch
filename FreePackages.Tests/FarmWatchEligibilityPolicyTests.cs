using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamKit2;

namespace FreePackages.Tests;

[TestClass]
[DeploymentItem("TestData")]
public sealed class FarmWatchEligibilityPolicyTests {
	private static FilterablePackage CreateEligiblePackage() {
		KeyValue? packageKey = KeyValue.LoadAsText("package_freepromotion.txt");
		KeyValue? appKey = KeyValue.LoadAsText("package_with_single_app_app_1.txt");
		if (packageKey == null || appKey == null) {
			throw new InvalidOperationException("Synthetic Steam PICS fixtures could not be loaded.");
		}

		FilterablePackage package = new(packageKey);
		package.AddPackageContents(new[] { appKey });
		return package;
	}

	[TestMethod]
	public void AcceptsExplicitFreeToKeepSinglePaidGame() {
		Assert.IsTrue(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(CreateEligiblePackage()));
	}

	[TestMethod]
	public void RejectsNoCostPackageWithoutFreePromotionFlag() {
		FilterablePackage package = CreateEligiblePackage();
		package.FreePromotion = false;
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}

	[TestMethod]
	public void RejectsFreeWeekendEvenIfFreePromotionFlagIsPresent() {
		FilterablePackage package = CreateEligiblePackage();
		package.FreeWeekend = true;
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}

	[TestMethod]
	public void RejectsWrongLicenceType() {
		FilterablePackage package = CreateEligiblePackage();
		package.LicenseType = (ELicenseType) 0;
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}

	[TestMethod]
	public void RejectsNonNoCostBilling() {
		FilterablePackage package = CreateEligiblePackage();
		package.BillingType = EBillingType.FreeOnDemand;
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}

	[TestMethod]
	public void RejectsPackagesContainingMultipleApps() {
		FilterablePackage package = CreateEligiblePackage();
		package.PackageContents.Add(package.PackageContents[0]);
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}

	[TestMethod]
	public void RejectsDlcContent() {
		FilterablePackage package = CreateEligiblePackage();
		package.PackageContents[0].Type = EAppType.DLC;
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}

	[TestMethod]
	public void RejectsPermanentlyFreeGame() {
		FilterablePackage package = CreateEligiblePackage();
		package.PackageContents[0].IsFreeApp = true;
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}

	[TestMethod]
	public void RejectsDemoContent() {
		FilterablePackage package = CreateEligiblePackage();
		package.PackageContents[0].Type = EAppType.Demo;
		Assert.IsFalse(FarmWatchEligibilityPolicy.IsFreeToKeepPackage(package));
	}
}

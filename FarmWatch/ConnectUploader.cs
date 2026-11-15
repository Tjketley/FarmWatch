using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;

namespace FreePackages;

/// <summary>
/// Optional, outbound-only WatchFarm Connect uploader. Disabled unless explicitly configured.
/// </summary>
internal static class ConnectUploader {
	private const int MaximumPendingUploads = 24;
	private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
	private static readonly SemaphoreSlim Gate = new(1, 1);
	private static readonly string OutboxPath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.Connect.outbox.json");
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	private static readonly List<UploadEnvelope> Pending = new();
	private static Timer? UploadTimer;
	private static bool Initialized;
	private static DateTime? LastSuccessUtc;
	private static string LastStatus = "Disabled";

	internal static async Task Initialize() {
		if (Initialized) return;
		Initialized = true;
		await LoadOutbox().ConfigureAwait(false);
		if (!FarmWatchSettings.Current.ConnectEnabled) {
			LastStatus = "Disabled";
			return;
		}
		if (string.IsNullOrEmpty(FarmWatchSettings.Current.ConnectInstanceId)) {
			FarmWatchSettings.Current.ConnectInstanceId = Guid.NewGuid().ToString("N");
			await FarmWatchSettings.Current.SaveConnect().ConfigureAwait(false);
		}
		StartTimer();
		LastStatus = "Waiting for first upload";
	}

	internal static async Task<bool> Configure(bool enabled, string? endpoint, string? token) {
		if (enabled) {
			if (string.IsNullOrWhiteSpace(endpoint) ||
				!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri) ||
				uri.Scheme != Uri.UriSchemeHttps ||
				!string.IsNullOrEmpty(uri.UserInfo) ||
				!string.IsNullOrEmpty(uri.Query) ||
				!string.IsNullOrEmpty(uri.Fragment) ||
				string.IsNullOrWhiteSpace(token) ||
				!System.Text.RegularExpressions.Regex.IsMatch(token, "^[A-Za-z0-9_-]{32,128}$")) {
				return false;
			}

			FarmWatchSettings.Current.ConnectEndpoint = uri.AbsoluteUri.TrimEnd('/');
			FarmWatchSettings.Current.ConnectToken = token;
			if (string.IsNullOrEmpty(FarmWatchSettings.Current.ConnectInstanceId)) {
				FarmWatchSettings.Current.ConnectInstanceId = Guid.NewGuid().ToString("N");
			}
			FarmWatchSettings.Current.ConnectEnabled = true;
			await FarmWatchSettings.Current.SaveConnect().ConfigureAwait(false);
			StartTimer();
			LastStatus = "Configured; upload queued";
			Utilities.InBackground(UploadNow);
			return true;
		}

		FarmWatchSettings.Current.ConnectEnabled = false;
		await FarmWatchSettings.Current.SaveConnect().ConfigureAwait(false);
		UploadTimer?.Dispose();
		UploadTimer = null;
		LastStatus = "Disabled";
		return true;
	}

	internal static object GetStatus() => new {
		enabled = FarmWatchSettings.Current.ConnectEnabled,
		endpointConfigured = !string.IsNullOrEmpty(FarmWatchSettings.Current.ConnectEndpoint),
		instanceId = FarmWatchSettings.Current.ConnectInstanceId,
		pendingUploads = Pending.Count,
		lastSuccessUtc = LastSuccessUtc,
		status = LastStatus
	};

	private static void StartTimer() {
		UploadTimer?.Dispose();
		UploadTimer = new Timer(async _ => await UploadNow().ConfigureAwait(false), null, TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(5));
	}

	private static async Task LoadOutbox() {
		try {
			if (!File.Exists(OutboxPath)) return;
			string json = await File.ReadAllTextAsync(OutboxPath).ConfigureAwait(false);
			List<UploadEnvelope>? loaded = JsonSerializer.Deserialize<List<UploadEnvelope>>(json, JsonOptions);
			if (loaded != null) Pending.AddRange(loaded.TakeLast(MaximumPendingUploads));
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
			LastStatus = "Outbox could not be loaded";
		}
	}

	private static async Task SaveOutbox() {
		string temporaryPath = OutboxPath + ".tmp";
		string json = JsonSerializer.Serialize(Pending, JsonOptions);
		await File.WriteAllTextAsync(temporaryPath, json).ConfigureAwait(false);
		File.Move(temporaryPath, OutboxPath, true);
	}

	private static UploadEnvelope CreateSnapshot() {
		var handlers = PackageHandler.Handlers.Values.ToArray();
		var bots = Bot.BotsReadOnly?.Values.ToArray() ?? [];
		int activations = handlers.Sum(handler => handler.BotCache.Activations.Count);
		int queued = handlers.Sum(handler => handler.BotCache.Packages.Count);
		int changedApps = handlers.Sum(handler => handler.BotCache.ChangedApps.Count);
		int changedPackages = handlers.Sum(handler => handler.BotCache.ChangedPackages.Count);
		int ownedPackages = handlers.Sum(handler => handler.BotCache.NewOwnedPackages.Count);
		int seenPackages = handlers.Sum(handler => handler.BotCache.SeenPackages.Count);
		int ignoredApps = handlers.Sum(handler => handler.BotCache.IgnoredApps.Count);
		int playtests = handlers.Sum(handler => handler.BotCache.WaitlistedPlaytests.Count);
		int evidence = handlers.Sum(handler => handler.BotCache.PromotionEvidence.Count);
		DateTime? lastActivation = handlers.Select(handler => handler.BotCache.GetLastActivation()).Where(value => value.HasValue).Max();

		var statistics = new {
			farmwatch_version = typeof(FreePackages).Assembly.GetName().Version?.ToString() ?? "unknown",
			claim_summary = FarmWatchStatisticsStore.GetClaimSummary(),
			farming_summary = FarmWatchStatisticsStore.GetFarmingSummary(),
			trade_summary = FarmWatchStatisticsStore.GetTradeSummary(),
			global_auto_claim_enabled = FreePackages.FarmWatchAutoClaimEnabled,
			observe_only = FreePackages.FarmWatchObserveOnly,
			last_pics_change_number = FreePackages.GlobalCache?.LastChangeNumber ?? 0,
			bots_total = bots.Length,
			bots_connected = bots.Count(bot => bot.IsConnectedAndLoggedOn),
			bots_with_farmwatch = handlers.Length,
			bots_auto_claim_enabled = handlers.Count(handler => handler.FarmWatchEnabled),
			queued_packages = queued,
			changed_apps = changedApps,
			changed_packages = changedPackages,
			new_owned_packages = ownedPackages,
			seen_packages = seenPackages,
			ignored_apps = ignoredApps,
			waitlisted_playtests = playtests,
			promotion_evidence = evidence,
			activations_in_cache = activations,
			last_activation_utc = lastActivation?.ToUniversalTime()
		};

		return new UploadEnvelope {
			SchemaVersion = 1,
			UploadId = Guid.NewGuid(),
			InstanceId = FarmWatchSettings.Current.ConnectInstanceId,
			RecordedAt = DateTime.UtcNow,
			Statistics = statistics,
			Claims = FarmWatchStatisticsStore.GetClaims().TakeLast(500).Select(claim => (object) new {
				claim_id = claim.EventId,
				target_type = claim.TargetType,
				target_id = claim.TargetId,
				claimed_at = claim.ClaimedAtUtc.ToUniversalTime().ToString("o"),
				source = claim.Source
			}).ToArray()
		};
	}

	internal static async Task UploadNow() {
		if (!FarmWatchSettings.Current.ConnectEnabled ||
			string.IsNullOrEmpty(FarmWatchSettings.Current.ConnectEndpoint) ||
			string.IsNullOrEmpty(FarmWatchSettings.Current.ConnectToken)) return;
		if (!await Gate.WaitAsync(0).ConfigureAwait(false)) return;

		try {
			if (Pending.Count < MaximumPendingUploads) {
				// Capture each scheduled interval while offline, retaining the newest
				// 24 snapshots in FIFO order until the receiver acknowledges them.
				Pending.Add(CreateSnapshot());
				await SaveOutbox().ConfigureAwait(false);
			} else {
				LastStatus = "Outbox full; preserving pending uploads until connection recovers";
			}

			while (Pending.Count > 0) {
				UploadEnvelope item = Pending[0];
				using HttpRequestMessage request = new(HttpMethod.Post, FarmWatchSettings.Current.ConnectEndpoint);
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", FarmWatchSettings.Current.ConnectToken);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				request.Content = new StringContent(JsonSerializer.Serialize(item, JsonOptions), Encoding.UTF8, "application/json");

				using HttpResponseMessage response = await Client.SendAsync(request).ConfigureAwait(false);
				if (!response.IsSuccessStatusCode) {
					LastStatus = $"Upload failed: HTTP {(int) response.StatusCode}";
					ASF.ArchiLogger.LogGenericWarning($"WatchFarm Connect upload failed with HTTP {(int) response.StatusCode}; retaining upload {item.UploadId} for retry.");
					return;
				}

				string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				using JsonDocument document = JsonDocument.Parse(responseBody);
				if (!document.RootElement.TryGetProperty("ok", out JsonElement ok) || ok.ValueKind != JsonValueKind.True) {
					LastStatus = "Receiver did not acknowledge upload";
					return;
				}

				Pending.RemoveAt(0);
				await SaveOutbox().ConfigureAwait(false);
				LastSuccessUtc = DateTime.UtcNow;
				LastStatus = "Connected";
			}
		} catch (Exception e) {
			LastStatus = "Upload pending; will retry";
			ASF.ArchiLogger.LogGenericWarningException(e);
		} finally {
			Gate.Release();
		}
	}

	private sealed class UploadEnvelope {
		[JsonPropertyName("schema_version")]
		public int SchemaVersion { get; init; }
		[JsonPropertyName("upload_id")]
		public Guid UploadId { get; init; }
		[JsonPropertyName("instance_id")]
		public string InstanceId { get; init; } = "";
		[JsonPropertyName("recorded_at")]
		public DateTime RecordedAt { get; init; }
		[JsonPropertyName("statistics")]
		public object Statistics { get; init; } = new();
		[JsonPropertyName("claims")]
		public object[] Claims { get; init; } = Array.Empty<object>();
	}
}

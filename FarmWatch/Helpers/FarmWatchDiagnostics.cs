using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;

namespace FreePackages;

internal sealed class FarmWatchEvent {
	public long Id { get; init; }
	public DateTime TimestampUtc { get; init; }
	public string Level { get; init; } = "info";
	public string Category { get; init; } = "runtime";
	public string? Bot { get; init; }
	public string Message { get; init; } = "";
	public string? Details { get; init; }
}

internal static class FarmWatchDiagnostics {
	private const int MaximumEvents = 500;
	private const long MaximumLogBytes = 5 * 1024 * 1024;
	private static readonly ConcurrentQueue<FarmWatchEvent> Events = new();
	private static readonly object FileLock = new();
	private static readonly string LogPath = Path.Combine("plugins", "FarmWatch", "FarmWatch.events.jsonl");
	private static long NextId;
	private static bool Initialized;

	internal static void Initialize() {
		lock (FileLock) {
			if (Initialized) return;
			Initialized = true;
			AppDomain.CurrentDomain.UnhandledException += (_, args) => Record("crash", "Unhandled process exception.", level: "error", details: args.ExceptionObject?.ToString() ?? "No exception details supplied.");
			TaskScheduler.UnobservedTaskException += (_, args) => {
				Record("crash", "Unobserved task exception.", level: "error", details: args.Exception.ToString());
			};

			try {
				if (!File.Exists(LogPath)) return;

				foreach (string line in GetLastLines(File.ReadLines(LogPath), MaximumEvents)) {
					try {
						FarmWatchEvent? entry = JsonSerializer.Deserialize(line, FarmWatchJsonContext.Default.FarmWatchEvent);
						if (entry == null) continue;
						Events.Enqueue(entry);
						NextId = Math.Max(NextId, entry.Id);
					} catch (JsonException) {
						// Ignore a partially written final record and continue recovering earlier events.
					}
				}
			} catch (Exception e) {
				ASF.ArchiLogger.LogGenericWarningException(e);
			}
		}
	}

	internal static void Record(string category, string message, string? bot = null, string level = "info", string? details = null) {
		Initialize();
		FarmWatchEvent entry = new() {
			Id = Interlocked.Increment(ref NextId),
			TimestampUtc = DateTime.UtcNow,
			Level = level,
			Category = category,
			Bot = bot,
			Message = message,
			Details = details
		};

		Events.Enqueue(entry);
		while (Events.Count > MaximumEvents) Events.TryDequeue(out _);

		try {
			lock (FileLock) {
				Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
				if (File.Exists(LogPath) && new FileInfo(LogPath).Length >= MaximumLogBytes) {
					string backupPath = LogPath + ".1";
					if (File.Exists(backupPath)) File.Delete(backupPath);
					File.Move(LogPath, backupPath);
				}

				byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry, FarmWatchJsonContext.Default.FarmWatchEvent) + Environment.NewLine);
				using FileStream stream = new(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
				stream.Write(bytes);
				stream.Flush(true);
			}
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
		}
	}

	internal static string[] GetLastLines(IEnumerable<string> lines, int limit) {
		ArgumentNullException.ThrowIfNull(lines);
		if (limit <= 0) return Array.Empty<string>();

		Queue<string> recent = new();
		foreach (string line in lines) {
			recent.Enqueue(line);
			if (recent.Count > limit) recent.Dequeue();
		}
		return recent.ToArray();
	}

	internal static string StorageFileName => Path.GetFileName(LogPath);
	internal static bool IsPersistent => true;

	internal static FarmWatchEvent[] GetRecent(int limit = 200) {
		int safeLimit = Math.Clamp(limit, 1, MaximumEvents);
		FarmWatchEvent[] recent = Events.ToArray();
		Array.Sort(recent, (left, right) => right.Id.CompareTo(left.Id));
		if (recent.Length > safeLimit) Array.Resize(ref recent, safeLimit);
		return recent;
	}
}

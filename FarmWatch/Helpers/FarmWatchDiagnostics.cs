using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

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
	private static readonly ConcurrentQueue<FarmWatchEvent> Events = new();
	private static long NextId;

	internal static void Record(string category, string message, string? bot = null, string level = "info", string? details = null) {
		Events.Enqueue(new FarmWatchEvent {
			Id = Interlocked.Increment(ref NextId),
			TimestampUtc = DateTime.UtcNow,
			Level = level,
			Category = category,
			Bot = bot,
			Message = message,
			Details = details
		});

		while (Events.Count > MaximumEvents) {
			Events.TryDequeue(out _);
		}
	}

	internal static FarmWatchEvent[] GetRecent(int limit = 200) {
		int safeLimit = Math.Clamp(limit, 1, MaximumEvents);
		return Events.ToArray().OrderByDescending(entry => entry.Id).Take(safeLimit).ToArray();
	}
}

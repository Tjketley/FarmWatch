# WatchFarm Connect uploader (FarmWatch Core)

FarmWatch Connect is an optional, outbound-only statistics uploader. It is disabled by default and does not expose ASF or create inbound connections.

## Configure

1. Install and activate the WatchFarm Connect WordPress plugin.
2. In WordPress admin, open **WatchFarm** and generate an upload token.
3. Copy the endpoint shown on that page (normally `https://your-site.example/wp-json/watchfarm-connect/v1/upload`).
4. Configure FarmWatch through the authenticated local ASF IPC API:

```http
POST /Api/FarmWatch/Connect
Content-Type: application/json
```

```json
{
  "enabled": true,
  "endpoint": "https://your-site.example/wp-json/watchfarm-connect/v1/upload",
  "token": "PASTE_THE_WORDPRESS_TOKEN_HERE"
}
```

The ASF IPC API is protected by ASF's normal local authentication. Do not publish the token in screenshots, logs, source control or public support requests.

To disable uploads:

```json
{
  "enabled": false
}
```

Disabling the uploader stops network activity. Already queued uploads remain on disk and are retained if Connect is re-enabled.

## Status

- `GET /Api/FarmWatch/ConnectStatus` returns enabled state, whether an endpoint is configured, pending upload count, last successful upload time and connection status.
- The token itself is never returned by the status endpoint.

## Transport contract

FarmWatch sends the WatchFarm Connect v1 envelope:

- `schema_version`: integer `1`
- `upload_id`: UUID, generated once per snapshot and reused for every retry
- `instance_id`: stable random identifier generated once and stored in FarmWatch settings
- `recorded_at`: UTC timestamp
- `statistics`: aggregate FarmWatch counters and state
- `claims`: an array of successful claim events with a stable `claim_id`, `target_type` (`App` or `Sub`), `target_id`, UTC `claimed_at` timestamp, and `source`

The current statistics payload includes plugin version, global auto-claim and observe-only flags, PICS change number, bot totals/connection/configuration counts, queue size, changed app/package counts, newly owned and seen package counts, ignored apps, waitlisted playtests, promotion evidence, activation-cache count, last activation time, durable FarmWatch claim totals (Free-to-Keep packages, free-DLC apps, today and this month), and farming-session totals (session count, active sessions, tracked seconds and unique games queued at session start).

Successful FarmWatch claims are also recorded locally in `FarmWatch.statistics.json`. This is a durable event ledger, separate from the short-lived activation rate-limit cache. It deduplicates by bot + claim type + target ID. The current ledger records successful claims made after this version is installed; it does not invent historical claims from ownership snapshots. Farming sessions are recorded from ASF's start/stop/finished callbacks in a separate local `FarmWatch.farming.json` ledger. Session duration is measured locally from callback timestamps; game IDs are captured from ASF's farming queue at session start, so per-game time is not claimed where ASF does not expose reliable attribution.

Claim events include package/app target IDs and timestamps but no bot name. No Steam credentials, Steam IDs, account names, filters, or raw diagnostics/event logs are uploaded.

## Delivery and retry behaviour

- A snapshot is persisted to `FarmWatch.Connect.outbox.json` before the HTTP request is sent.
- Uploads run on a five-minute schedule, with an initial attempt shortly after enabling.
- HTTPS only; requests use `Authorization: Bearer` and JSON.
- A successful `{ "ok": true }` response removes that upload from the outbox.
- Timeouts, network errors, non-success HTTP responses and invalid acknowledgements retain the same upload UUID for retry.
- Up to 24 snapshots are retained. When the outbox is full, new snapshots are paused rather than deleting unsent data.
- Repeated uploads are safe because the WordPress receiver deduplicates by `instance_id` and `upload_id`.

The uploader is independent of FarmWatch's claiming switches. Enabling Connect does not enable auto-claiming or change any bot configuration.


## Statistics time boundaries

The API's `today` and `thisMonth` counters and the 30-day activity series use UTC calendar boundaries. Farming durations are clipped to the requested period boundaries rather than counting an entire session in every period it overlaps. Any farming session left open when ASF exits is closed at the next process start; time while ASF was offline is not counted as farming.

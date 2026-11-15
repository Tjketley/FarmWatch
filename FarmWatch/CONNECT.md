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
- `claims`: currently an empty array; claim history is deliberately not fabricated from activation counters

The current statistics payload includes plugin version, global auto-claim and observe-only flags, PICS change number, bot totals/connection/configuration counts, queue size, changed app/package counts, newly owned and seen package counts, ignored apps, waitlisted playtests, promotion evidence, activation-cache count and last activation time.

No Steam credentials, Steam IDs, account names, bot names, filters, or raw diagnostics/event logs are uploaded.

## Delivery and retry behaviour

- A snapshot is persisted to `FarmWatch.Connect.outbox.json` before the HTTP request is sent.
- Uploads run on a five-minute schedule, with an initial attempt shortly after enabling.
- HTTPS only; requests use `Authorization: Bearer` and JSON.
- A successful `{ "ok": true }` response removes that upload from the outbox.
- Timeouts, network errors, non-success HTTP responses and invalid acknowledgements retain the same upload UUID for retry.
- Up to 24 snapshots are retained. When the outbox is full, new snapshots are paused rather than deleting unsent data.
- Repeated uploads are safe because the WordPress receiver deduplicates by `instance_id` and `upload_id`.

The uploader is independent of FarmWatch's claiming switches. Enabling Connect does not enable auto-claiming or change any bot configuration.

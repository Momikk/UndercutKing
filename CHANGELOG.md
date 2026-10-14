# UndercutKing Changelog

[中文](CHANGELOG.zh-CN.md) · [README](README.md)

This document records changes in published versions. The plugin is an early test release. See the [validation record](docs/validation.md) for build and regression results; in-game appearance and interactions still require validation.

## 0.1.0.9 — 2026-10-14

- Fix bag-match blue highlighting being limited to item sales instead of also appearing in the retainer overview.
- Read normal bags while the overview is open. Turn a retainer's market sale text blue when any listing in its latest scanned snapshot for the current character matches a bag item, regardless of NQ/HQ.
- Preserve undercut-count suffixes and market judgments; restore previous colors when matches disappear or the feature is disabled. Unscanned retainers cannot show a confirmed blue match.

## 0.1.0.8 — 2026-10-14

- Highlight listed item names in blue when the same item exists in the four normal inventory bags. Treat NQ and HQ as identical; exclude the Armoury Chest, saddlebags and retainer inventories.
- Add an independent toggle enabled by default and check bag changes approximately every 0.25 seconds. Restore market highlighting or original styles when the match disappears or the feature is disabled.
- Give blue visual priority over red/yellow sale-list names while retaining the market-based undercut judgment, information-bar counts and price table.
- Add light/dark blue palettes and outlines. Read local inventory only, without additional Universalis requests.

## 0.1.0.7 — 2026-10-14

- Subscribe to Universalis listing additions/removals and coalesce full-price checks only for scanned items of the current character. Do not treat deltas as complete market minima.
- Refresh the baseline after connection/reconnection, back off connection failures, and stop subscriptions on logout, an empty watch list or automatic-query disable.
- Batch up to 50 items per world with a five-second coalescing window and one sequential queue paced at least one second apart. Space per-item push checks at least 30 seconds apart.
- Pause all requests, including manual refreshes, on 429 responses. Wait at least a minute, respect longer Retry-After values, increase backoff, and attempt failed batches at most three times.
- Default to 15-minute verification when push is connected and polling at most five minutes apart otherwise. Migrate the old five-minute default to 15 while retaining other custom intervals and separate two-day quote validity.
- Add a push toggle and connection/queue/rate-limit status. Include the BSON protocol dependency and third-party license notice.

## 0.1.0.6 — 2026-10-14

- Fix quote validity being used as the polling interval. A two-day validity window no longer blocks periodic queries.
- Poll scanned items for the current character every 5 minutes by default, independently configurable from 1 to 60 minutes, with retainer windows and the market board closed.
- Existing configurations default to 5-minute polling while retaining their automatic-query toggle and quote validity setting.
- Throttle retries for old uploads and failed requests; retain per-item deduplication, a maximum of two concurrent requests and a 5-second manual-refresh cooldown.
- Preserve actual quote timestamps and prefer newer quotes. Universalis depends on player uploads, so periodic queries cannot guarantee real-time prices.

## 0.1.0.5 — 2026-10-14

- Hide `(0)` in the retainer list when valid quotes confirm no undercut listings, restoring the native sale quantity text.
- Keep positive counts and `(N+)` or `(?)` indicators for incomplete prices.

## 0.1.0.4 — 2026-10-14

- Append each retainer's undercut listing-slot count after the native sale quantity in the retainer list, e.g. “出售中（14件） (3)”.
- Count scanned snapshots for each retainer of the current character. Separate listing slots of the same item count individually; recalculate after asking prices or market quotes change.
- Show `(N+)` or `(?)` when prices are incomplete, and `(0)` when valid quotes confirm no undercut listings.
- Add an independent retainer-count toggle. Restore original text when counts are disabled, list nodes are reused, or the plugin is unloaded, retaining the existing highlight settings.

## 0.1.0.3 — 2026-10-14

- Improve red and yellow highlight readability with automatic, light and dark theme palettes.
- Use dark red and dark amber on light interfaces, and brighter red and yellow on dark interfaces, with contrasting text outlines.
- Restore original text colors and outlines when highlights are disabled, list nodes are reused, or the plugin is unloaded, while preserving style fields changed by the game or other plugins.

## 0.1.0.2 — 2026-10-14

- Compare NQ and HQ listings together for the same item, using the lowest market price regardless of quality.
- Extend the default quote validity to two days and migrate older configurations to this value.
- Configure quote validity in days, up to 30 days, with a button to reset it to two days.
- Quote validity refers to market data freshness, not listing age. Listing-age tracking is not included in this release.

## 0.1.0.1 — 2026-10-13

- Add a server information bar entry counting undercut listing slots across scanned retainers of the current character.
- Display `压价 N`, `压价 N+` or `压价 ?` to distinguish confirmed counts from listings with unconfirmed prices.
- Open the plugin window by clicking the entry. Allow it to be disabled in settings, hide it on logout, and remove it on unload.
- Recalculate the count after asking prices or market quotes change.

## 0.1.0.0 — 2026-10-13

- Release a standalone lightweight retainer market helper built for Dalamud API 15.
- Read current retainer listings and unit prices, and highlight undercut or unconfirmed-price states in the native sales and retainer lists.
- Support automatic Universalis queries and complete market listings received from the game's Compare Prices feature.
- Add `/uking` and `/undercutking` commands, settings, and a price view for the current retainer.
- Provide price notifications without automatically changing prices or operating retainers. Sales history, statistical reports and CSV export are not included.
- Adapt portions of inventory and native UI interaction code from Critical_Impact's Allagan Market under AGPL-3.0-or-later, retaining source attribution.

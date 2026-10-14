# UndercutKing Changelog

[中文](CHANGELOG.zh-CN.md) · [README](README.md)

This document records changes in published versions. The plugin is an early test release. See the [validation record](docs/validation.md) for build and regression results; in-game appearance and interactions still require validation.

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

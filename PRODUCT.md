# Product

<!-- impeccable:product-schema 1 -->

> Inferred from the V2 codebase, README and the user's /goal brief (2026-11-11). The user asked to proceed without an interview; facts marked *(inferred)* were not confirmed in words.

## Platform

windows-desktop (WPF, .NET 8). Not web, not mobile: native Windows controls, mouse + keyboard, window from 880×580 up to full screen.

## Users

DJs, music collectors and music lovers *(from README)*. They own a list of songs (a tracklist, a folder of audio files, a wantlist) and need the matching YouTube video for every line. Sessions are long and repetitive: dozens to hundreds of songs, one decision each *(inferred)*.

## Product Purpose

Load a .txt with one song per line (or generate it from an audio folder), search YouTube for each line, let the user pick the right video, export the chosen links as TXT / CSV / M3U. Success: get through the whole list fast, never lose progress, never pick the wrong video.

## Positioning

A queue-driven picker, not a search box: the list is the unit of work, each song has a status, progress is saved on every pick, and the YouTube quota is tracked in real units.

## Operating Context

Batch work at a desk, often evening sessions while preparing sets *(inferred)*. The user scans thumbnails, durations, channels and view counts to tell an official release from a live bootleg or a lyric video. Repeated keyboard-paced decisions.

## Capabilities and Constraints

- Queue with per-song status (pending / done / skipped / no results), click to jump, go back, edit query, skip, refresh bypassing cache.
- Filters: duration, upload date, views. Load more results.
- Result sources: API, cache, test mode.
- Resume previous session banner, autosave indicator, quota pill (units today, searches left).
- Settings: language (en/it/fr/es/de, live switch), theme (system/light/dark, live switch), results per search, test mode, cache-only, API keys (DPAPI encrypted), cache stats/clear.
- Dialogs: Settings, About, Edit query, Create TXT from audio folder.
- V3 scope: interface redesign only. ViewModel, services and models stay unchanged; bindings are the contract.

## Brand Commitments

- Name: Phonexis. A TX-Breaker / MetaGroove project.
- MetaGroove brand purple #6B4092 (existing logo.png and phonexis.ico must keep working).
- Light and dark themes, plus follow-system.

## Evidence on Hand

- Logo: src/Phonexis/Resources/Images/logo.png, icon phonexis.ico.
- Real UI copy in five languages under src/Phonexis/Resources/i18n/.
- No testimonials, usage numbers or screenshots of real use: none to be invented.

## Product Principles

1. The queue is the product: where you are in the list is always visible.
2. One decision per song, made fast: the comparison data (duration, channel, views, date) is readable at a glance.
3. Never lose work: saving and quota state are visible but quiet.
4. Native and trustworthy: standard Windows affordances, keyboard friendly.

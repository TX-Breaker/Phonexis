# Changelog

## 3.0.0

Complete interface redesign and video features on top of the V2 engine.

### Search
- Search without an API key: free search through YouTube's public search page (YoutubeExplode), with views and upload date read from each video page.
- Choose the engines and their order: Free then API (default), API then free, Free only, API only — in Settings › General or from the title-bar picker.
- Automatic fallback to the other engine when one fails; the status bar reports it. When all fail, the error card explains why per engine and offers *Retry with …* and *Always try … first*.
- An engine failing 3 times in a row is tried last for 10 minutes. Empty answers never trigger the fallback, so no API quota is spent on songs YouTube does not have.
- When free search finds nothing, the "no results" card offers *Retry with YouTube API* (with a key set).
- Result descriptions stay on one line so every card keeps the same size; *Show full description* opens the whole text.
- The "no API key" banner appears only in *API only* mode; the first-run guide lets you pick the engines and presents the key as optional; the quota pill hides in *Free only* mode.

### Interface
- New "Obi" design system (see `DESIGN.md`): purple rail that fills as you work, custom window chrome, light/dark themes, Segoe UI + Bahnschrift, motion that respects Windows "Show animations".
- Catalog-numbered queue with status marks; result cards with fixed-scale thumbnails, tabular spec line and compact layout on narrow windows.
- Skeleton loading, empty and error states, drag & drop overlay, dialogs restyled, first-run guide.

### Picking
- Automatic preselection of the most likely video (file name, audio duration, artist/title, official channel, unwanted versions, popularity); `Enter` picks it.
- Hover frames on thumbnails (YouTube storyboards) and an in-app video preview (WebView2).
- Keyboard shortcuts for every step (`1`–`9`, `S`, `←`, `E`, `R`, `P`, `Ctrl+O/S/E/D`, `Ctrl+Shift+S`, `F1`).

### Lists
- `.phonexis` list files keep audio paths, edited queries, picks and download state; the audio-folder dialog saves them directly.

### Videos
- Download the picked videos named after their tracks, next to the audio, in a `video` subfolder or in one folder.
- Built-in downloader (YoutubeExplode + FFmpeg), yt-dlp, JDownloader 2 (`.crawljob`), and link handoff to MediaHuman (automatic "Paste link"), IDM, 4K Video Downloader+, Free Download Manager, Stacher, Parabolic, Open Video Downloader, ClipGrab, YTD, Freemake.
- Rename & archive: recognises files saved by any downloader (video ID, or title + duration) and renames/moves them; undoable.
- Optional: videos take the creation and modification dates of their audio files.
- Stalled downloads are detected (no progress for 90 s), retried twice, then marked failed so the queue keeps going; "Retry failed" reruns only those.

### Engineering
- xUnit test project (`tests/Phonexis.Tests`).
- `PHONEXIS_DATA_DIR` points the app at a separate data folder (portable use, demos, tests); real keys are never imported into it.
- Publish script produces a single `Phonexis.exe` without symbols or package docs.
- Installer (Inno Setup): per-user by default, five languages, `.phonexis` file association, "Open with" for `.txt` lists; the app opens a list passed on the command line.

## 2.0.0

Complete rewrite of V1 in .NET 8 WPF: light/dark themes, five languages switchable live, fixed duration/view filters, real quota accounting, DPAPI-encrypted API keys, per-song selections, visible queue, TXT/CSV/M3U export, autosave and resume, drag & drop and audio-folder lists.

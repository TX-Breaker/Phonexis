---
version: 1
slug: "src-phonexis-mainwindow-xaml"
primary_target: "src/Phonexis/MainWindow.xaml"
related_targets: ["src/Phonexis/Views"]
---

# Phonexis main window (and its dialogs)

Scope: the whole WPF app shell: MainWindow (welcome / results / complete states) plus Settings, About, Edit query, Folder-to-TXT dialogs. Visitor mode: **Operate**. Build path: code-led (native WPF).

Audience & job: DJs and collectors working through a list of songs, one YouTube pick per line, fast and without losing progress. Constraints: ViewModel, services, bindings and i18n keys are fixed; only XAML, theme dictionaries and UI-only helpers change. Light, dark and follow-system themes. Native controls stay native in behaviour.

Unattended run: the user asked (/goal) not to be interrupted, so the assigned direction was built without the decision page; challengers' verdicts are recorded below.

## Direction contract

THESIS: The window is a record jacket and the left edge wears its obi, the Japanese import band that tells you what the record is and how far into the series you are. Refuses the category default: grey sidebar + white cards + one purple button.

OWN-WORLD: Committed MetaGroove purple obi rail (#6B4092 light / #3E2160 dark ground with #B793EA highlight) running the full window height; jacket ground in cool ink-tinted neutrals (light #F3F2F6 / dark #110F15). Bahnschrift (DIN-cut, condensed widths) for catalog numbers, durations, counts and labels in tabular figures; Segoe UI for titles and body. Vermilion obi-stamp #E0482F only for the "picked" mark. Segoe MDL2 icons. 6px corners, hairline borders, soft offset shadows.

STORY: The user sees the song number and total on the obi, the query in big type, compares five same-scale results by duration/views/date on one tabular spec line, picks one, and watches the obi fill another notch.

FIRST VIEWPORT: 64px obi rail at left (logo, vertical PHONEXIS wordmark, nav actions, progress filling bottom-up, settings/theme/about at the foot). 300px queue panel with catalog-numbered rows (001 …). Main column: custom title bar with quota meter and caption buttons; current-song header with "012 / 148" in Bahnschrift, query at 22px, toolbar (Back, Edit, Search again, Skip); filter strip; result rows (192×108 thumbnails, title, channel, spec line, Select primary + Open/Copy).

FORM: Japanese LP obi strip, position 4 of 7 on the grounded list. seed:a121be937b80157b

SIGNATURE MOVE: The obi fills. Progress is the rail itself, a light band rising up the purple strip with an eased fill on every pick; the current queue row is "wrapped" (tinted band + purple catalog number), and picked rows carry the vermilion stamp dot.

RAISES:
- From Datamatics (declined): every number is tabular and column-aligned.
- From the botanical folio (declined): every result renders at one fixed scale and baseline so thumbnails compare directly.
- From the streaming wall (declined): the hovered result lifts while the rest stays still.
- From the cassette J-card (competitive): running times sit right-aligned and tight, read like a tracklist.
- From the Factory catalog sleeve (competitive): catalog numbering (001, 002 …) is the queue's identity.
- Emigre bitmap specimen: declined, nothing kept (pixel type would hurt a working list).

MOTION GRAMMAR: 160–260ms exponential ease-out; state views fade+rise 10px; result rows stagger 35ms; skeleton shimmer while searching; rail fill eases 420ms; dialogs scale 0.97→1. Disabled when Windows "show animations" is off.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

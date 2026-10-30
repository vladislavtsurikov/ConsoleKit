---
title: "Log files (sessions)"
description: "LogSessionWriter (project VladislavTsurikov.ConsoleKit.Files) journals a LogHub to disk for the whole run of an application."
---

`LogSessionWriter` (project `VladislavTsurikov.ConsoleKit.Files`) journals a `LogHub` to disk for
the whole run of an application. This page is the format specification: give it to an LLM
together with the log folder.

## Folder

`%LOCALAPPDATA%/<ProductName>/logs`, for example `%LOCALAPPDATA%/MCPFamily/logs`.
`LogFileOptions.Directory` overrides it; MCPFamily also honours `MCPFAMILY_LOG_DIR`. Several
applications of one product share the folder.

```text
current-Worker-4321-2026-10-09_15-30-12.jsonl                running session (process 4321)
2026-10-09_15-30-12__2026-10-09_17-02-45_Worker.jsonl        finished session
2026-10-09_15-30-12__2026-10-09_17-02-45_Worker_part2.jsonl  continuation of a large session
2026-10-09_15-30-12__2026-10-09_17-02-45_Worker.summary.md   summary — read this first
2026-10-08_10-01-00__crashed_Desktop-5120.jsonl              session that never closed
2026-10-08_10-01-00__crashed_Desktop-5120.summary.md
index.json                                                   list of sessions
```

## Lifecycle

1. **Start.** The writer creates `current-<app>-<pid>-<start>.jsonl` and writes entries to it while
   the application runs: from a background queue, flushed every 500 ms and at once on Error or
   Fatal. A crash therefore loses at most the last half second.
2. **Close** (`Dispose`, normal process exit):
   - the files are renamed `<start>__<end>_<app>`;
   - `<name>.summary.md` is written;
   - `index.json` is updated.
3. **Recovery.** The next time any application of the product opens the folder, `current-*` files of
   processes that are gone are renamed `<start>__crashed_<app>-<pid>` and get a summary.
4. **Limits** (`LogFileOptions`):

   | Option | Default | Meaning |
   |---|---|---|
   | `MaxSessions` | 30 | Finished sessions kept **per application**. |
   | `MaxAgeDays` | 14 | Older finished sessions are deleted. |
   | `MaxTotalSizeMb` | 300 | Whole folder; the oldest sessions are deleted first. |
   | `MaxFileSizeMb` | 20 | A larger session continues in `_part2`, `_part3`, … |
   | `MinimumLevel` | Debug | Lower levels stay in memory only. |

   Running sessions are never deleted.

## Entry format (JSON Lines)

Every file is UTF-8 JSON Lines. **The first line is the session header**:

```json
{"session":{"product":"MCPFamily","app":"Worker","version":"1.0.0.0","pid":4321,"started":"2026-10-09T15:30:12.345+03:00","machine":"PC","format":1}}
```

Every following line is one entry:

```json
{"t":"2026-10-09T15:31:02.120+03:00","l":"Error","src":"endpoint:unity","pid":5012,"stream":"stderr","cat":"Bridge","msg":"Connection refused","detail":"…","props":{"Port":"6400"}}
```

| Key | Meaning |
|---|---|
| `t` | Time, ISO 8601 with offset. |
| `l` | Level: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`. |
| `src` | Source: `worker` (the app itself), `endpoint:<id>`, `runtime`, `clients`, … |
| `pid` | Process that wrote the line (optional). |
| `stream` | `stdout` or `stderr` for process output (optional). |
| `cat` | Logger category (optional). |
| `msg` | Message. |
| `detail` | Full text: stack trace, original line, last stderr lines of a failed exit (optional; omitted when equal to `msg`). |
| `props` | Structured properties, string values (optional). |

Process lifecycle entries have messages:
- `Started: PID n`;
- `Restarted: PID n`;
- `Exited with code n` — `Error` when n ≠ 0.

## Summary (`.summary.md`)

- Session header: product, version, process, start, end or "did not close normally", files.
- Counts per level and per source.
- Process starts and exits.
- The first 50 warnings and errors with the file and **line number** of each entry.
- A reminder of this format.

## index.json

An array of sessions, newest first. For each session:
- `id`, `app`, `processId`;
- `started`, `ended`;
- `status`: `Finished` or `Crashed`;
- `files`, `summary`;
- `entries`, `errors`, `warnings`.

`LogSessionCatalog.List(directory)` returns the same list plus running sessions.
`LogSessionCatalog.ReadEntries(file)` reads a file back as `LogEntry` values.

## Recipes for an LLM

- Start with the newest `*.summary.md` (or `index.json`) to pick a session.
- Errors: lines containing `"l":"Error"` or `"l":"Fatal"`; the summary gives their line numbers.
- One endpoint: lines with `"src":"endpoint:<id>"`.
- Why a process died: its `Exited with code` entry; the `detail` holds its last stderr lines.

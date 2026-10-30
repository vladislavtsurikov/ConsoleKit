---
title: "Log data flow"
---

```text
Serilog event ───────> SerilogLogSource ─────────────┐
                                                      │
Process (IProcessOutput)                              │
  stdout / stderr / start / exit ─> ProcessLogSource ─┤
                                                      │
Log file of another process ─> FileTailLineStream ─> ProcessLogSource
                                                      │
Entries of another process's journal ─> RemoteLogSource (mirrored)
                                                      │
                                                      v
                                                   LogHub
                                   ┌──────────────────┴──────────────────┐
                                   v                                     v
                            LogEntryStore                         ILogSink(s)
                       (ring buffer, ids, events)          LogSessionWriter: session file,
                                   │                       summary, index, retention
                                   v
             ConsoleViewModel: incremental filter, counters, collapse
                                   │
                                   v
                     ConsoleView (Avalonia) or ConsolePanel (AbberiaUI)
```

- **`LogHub`** is the entry point of one journal: sources write into it, it stores entries in memory
  and hands every stored entry to its sinks. Sinks never block or break the console: a failing sink
  is ignored.
- **`LogEntryStore`** numbers entries in storage order and raises `EntryAppended`, `EntryEvicted`
  and `EntriesCleared`.
- **`ConsoleViewModel`** appends new entries to the visible list and keeps the counters from these
  events. It rebuilds the list from a snapshot only after a filter change, a clear, or an eviction
  that a collapsed row cannot follow.
- **Mirrored entries** — entries copied from another process's journal, for example Worker logs shown
  in Desktop — are shown but not written to disk again: they are already in that process's session
  file.

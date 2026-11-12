# SUS conversion fixtures (P2-6)

These two charts and `expected.txt` pin the behaviour of the SUS chart importer
(`SusRuntimeConverter`) while it is split into `SusParser`, `SusTimingConverter`,
`SusNoteConverter` and `SusEventConverter`. The split is only accepted when the
same input still produces the same timestamps, event order, note classes and
long-note links.

| File | What it exercises |
| --- | --- |
| `timing.sus` | `#REQUEST ticks_per_beat`, two `#BPM` definitions, a mid-bar BPM change, time-signature changes, `#TIL00` high-speed rows, `#VOLUME` rows, and one row of each plain note kind (normal, critical, skip, friction, friction-hide, flick, skill, fever). |
| `linked.sus` | Note chains: long-note head/connection/tail, generated hold-combo bodies, guide chains (`Guide`/`GuideHidden`/`GuideEnd`), and same-timestamp pairs. |

`expected.txt` is the textual transcript the importer produced **before** the
split. Each case line is `CASE <fixture> mirror=<bool> combo=<bool> pair=<bool>`;
inside a case, `FRAME`, `EVENT`, `ROOT` and `CHILD` lines follow in production
order. `SusChartSnapshotTests` regenerates the same text and compares it line by
line, so any drift in the importer shows up as a diff instead of a silent
behaviour change.

Regenerate it only when a behaviour change is intended:

```powershell
# inside the editor, after the intended change is verified by other tests
unity command eval_file --file TestResults/restructure-p2-6-capture-sus.cs
```

The cassette is intentionally verbose: a golden line list is what lets a
reviewer compare the split frame by frame, which a hash alone would not.

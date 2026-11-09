# Hold overlap regression

Minimal geometry excerpt from the user-supplied `chart (1).zip`, Breach of Faith / BOF,
MASTER 34, chart author yorutsuki (2026-11-03 debugging session).

Only seven long-note chains (48 authored nodes) and BPM 172 are retained. No audio,
jacket, unrelated notes, or playback package metadata is included. This is an input
regression fixture, not a playable redistribution of the song.

The tests drive the real converter, update loop, touch source and judgment pipeline.
They check following each strand and keeping hands on their respective sides, at
60 and 165 Hz. Before the matching fix, side-preserving input releases a hold near
score time 12.96 seconds (roots at ticks 9600/13440), and again near 15.75 seconds
(roots at ticks 9600/21120). Audio playback includes the package's nine-second filler.

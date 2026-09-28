# Performance checks

The 2.0.6 chunk-streaming changes were measured in a native Windows player on
28 September 2026 using an Intel Core i5-12450H and an RTX 4060 Laptop GPU.
Both renderers generated the same 81 chunks from seed 1453 at a four-chunk radius.
The test timed CPU work inside each renderer update, not total frame time or FPS.

| Renderer | Median update | 95th percentile | Longest update | Total CPU work |
| --- | ---: | ---: | ---: | ---: |
| 2.0.5 synchronous renderer | 17.31 ms | 21.93 ms | 32.85 ms | 1422.18 ms |
| 2.0.6 incremental renderer | 2.42 ms | 3.10 ms | 4.16 ms | 743.50 ms |

The incremental renderer spread this work over 350 updates instead of 81.
Distant terrain therefore fills in over more frames, while nearby edits receive
priority. The graphics preset, texture detail and mesh geometry were not reduced.
The three-update remesh fixture peaked at 3.06 ms.

The comparison also passed exact mesh-attribute and collision-geometry checks on
nine chunks containing a village, every supported block type, fluid levels and
boundary doors. In-flight edits and view-distance changes were tested for stale
collision geometry. The temporary legacy renderer is not included in the release.

`scripts/test-performance.ps1` runs the current renderer, edit/collision and
background-save checks. It writes `performance.json` and a test log beneath
`artifacts/` and uses fresh saves beneath `.cache/`. Hardware load, drivers, view
distance and world contents affect results. Mesh uploads, collider cooking and
individual terrain-generation calls still have some indivisible work; a 3 ms
budget is a scheduling target, not a hard upper bound or a promised frame rate.

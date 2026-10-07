# M1 review log

Your comments on `milestone/m1-ta-client`, and what happens to each one. Fix-ups are applied together once you've finished the review, so the branch doesn't move under you.

| # | Comment | Decision | Status |
|---|---|---|---|
| 1 | R4-2: when TA's `media_url` points to another channel's folder, correct it | Build the path from the video's own `channel_id` (`<channel_id>/<video_id>.<ext>`) and flag the video as corrected | Moved to M2 (not part of the reviewed M1), done in `feature/m2-media-path-correction`; scan: 44,972 valid, 5 corrected, 0 rejected |
| 2 | `TaMapper`: why a mapper class instead of an implicit operator? | Keep `TaMapper` for now. An implicit conversion shouldn't throw or lose data, and this mapping does both (it needs prior validation and drops fields). Revisit if you get a better idea | Kept as is |

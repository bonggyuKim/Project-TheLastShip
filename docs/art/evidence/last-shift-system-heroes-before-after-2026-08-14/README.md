# Last Shift system hero fixtures — before/after

Card: `7feffe89`

The six captures use the same scene camera positions at 1280×720.

| Room | Before | After | Fixture under review |
| --- | --- | --- | --- |
| Power | `power_before.png` | `power_after.png` | power bus panel |
| Cooling | `cooling_before.png` | `cooling_after.png` | heat exchanger coil |
| Life support | `life_support_before.png` | `life_support_after.png` | scrubber stack |

- Before source: visual-review capture committed at `526d996`, before the card's system-hero wiring.
- After source: clean detached worktree at `a640118` (the source commit merged as `20e3841`).
- Capture command: `DoodleUp.Editor.LastShiftVisualReviewCapture.CaptureForAutomation`
- Unity: `6000.3.19f1`, batchmode with GPU rendering.
- Result: `[LAST_SHIFT_VISUAL_REVIEW] views=11 ... result=PASS`

Only the three room pairs required by the card are retained here.

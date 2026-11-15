# Group A: SSVEP frequency response

Your scene(s) and scripts go in this folder. Start with docs/03-build-your-scene.md.

Keep this file up to date. The analysis depends on it.

## Scene

- Scene file: `GroupA_SSVEP.unity`
- Script: `SSVEPTask.cs` (on the Stim Control object)
- Who edits the scene: Annabelle

Scene setup:

- `patch`: Quad at the template's stimulus position (`ballStartPos`, 0.5 m in front of the viewer), about 19° wide, material `PatchMat` (HDRP/Unlit). The script drives its brightness through the material's emission with Exposure Weight = 0, so the patch's luminance does not depend on the camera exposure.
- `fixation`: small red sphere just in front of the patch's center, about 1.2°.
- Sky and Fog Volume uses `SkyandFogSettingsProfile 1` (a group copy of the shared profile) with Exposure → Mode = Fixed, Fixed Exposure = 13, so automatic exposure cannot partly cancel the flicker.

## Events logged

The `Trial` column holds the trial number (1–40); events outside trials have trial −1.

| Event | Value | When it is logged |
|---|---|---|
| `settings` | `refreshHz=90;stimSeconds=8;waveform=Square` | once, at experiment start |
| `order` | the shuffled list of `n` (frames per cycle) for the session, space-separated | once, at experiment start |
| `stim_on` | `<f>Hz;n=<n>`, e.g. `15Hz;n=6` | first rendered frame of the flicker |
| `flick_on` / `flick_off` | `<f>Hz` | every on/off change of the patch (square wave) |
| `flick_frame` | patch level 0–1 | every frame (sine wave only) |
| `stim_off` | `<f>Hz;long_frames=<k>` | first frame after the flicker; `k` counts frames longer than 1.5 frame periods (dropped frames) |
| `break_start` / `break_end` | (empty) | self-paced break (Space) every 10 trials |

All visual events are logged after `WaitForEndOfFrame()`, so the time refers to the frame that showed the change.

## Conditions and trial counts

- Square-wave flicker, white/black, at f = 90 / n Hz with n = 15, 12, 10, 9, 8, 7, 6, 5, 4, 3 frames per cycle: 6, 7.5, 9, 10, 11.25, 12.86, 15, 18, 22.5, 30 Hz.
- 45 Hz (n = 2) is not included because the EPOC X only records up to 43 Hz. n = 7 (12.86 Hz) closes the gap between 11.25 and 15 Hz for phase unwrapping.
- 4 repetitions per frequency = 40 trials in random order. 8 s of flicker per trial (whole cycles), 3–4 s rest between trials (5 s before the first), and a break every 10 trials. That's about 7.7 min of trials plus calibration and breaks.
- Duty cycle: the patch is on for floor(n/2) of every n frames, so D = 1/2 for even n and below 1/2 for odd n (1/3 at 30 Hz). In the analysis, divide each response by the stimulus's own Fourier coefficient at f, which has amplitude ∝ sin(πD) and phase −πD, before plotting amplitude or fitting the phase slope.

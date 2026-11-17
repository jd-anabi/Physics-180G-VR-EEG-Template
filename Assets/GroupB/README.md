# Group B: interface difficulty and information throughput

Your scene(s) and scripts go in this folder. Start with docs/03-build-your-scene.md.

Keep this file up to date. The analysis depends on it.

## Scene

- Scene file: `GroupB_Buttons.unity` (a copy of `Examples/TaskTemplate.unity`; exposure set to Fixed, EV 13, in `GroupB_SkyandFog.asset`)
- Script: `ButtonSearchTask.cs` on the **Stim Control** object. It builds the panel and buttons itself when the experiment starts; all settings are in the Inspector.
- Material: `ButtonMat.mat` (HDRP/Unlit, copied per object; colors are drawn as emission with Exposure Weight 0)
- `LowGpuMode.cs` on **Sky and Fog Volume**: set `lowGpu = true` in the script to try the scene on a laptop with integrated graphics. The template's physically based sky makes such GPUs hang as soon as Unity draws the scene. The flag turns the sky, the static lighting sky and the fog off in Edit and Play mode without changing the scene file. Quit Unity before changing the flag, and keep it `false` in commits so the headset shows the template's sky.
- Who edits the scene: Charles Victorio

## Task

Each trial: a fixation cross at the panel's center (0.8–1.2 s), then the target letter at the center (1 s), then n lettered buttons at random positions. The participant looks at the button with that letter and presses **Space**; the button closest to the gaze direction is the answer. Without the headset (**Use Eye Tracking** unticked) the mouse pointer stands in for the gaze.

- Panel: 34° × 24°, grey, at `textPos` (≈0.5 m away). Buttons: 3.5° squares on a 6 × 4 grid of 5° cells, at most one per cell, jittered so neighbors stay ≥0.75° apart. Letters from `ABCDEFGHJKLMNPRSTUVXYZ`, all different within a trial.
- Brightness: buttons are slightly lighter than the panel and their letters dark, so a button with its letter is about as bright as the panel behind it and mean luminance barely changes with n. Check this in the headset.
- A press counts only when the gaze is within 4° of a button's center; during a blink the last valid gaze sample (≤0.2 s old) is used. Other presses are logged as `press_ignored` and the trial goes on.

## Events logged

Trial column = trial number in the session (practice included). Positions are degrees from the panel's center, + = right / up.

| Event | Value | When it is logged |
|---|---|---|
| `settings` | `viewDist=0.500;grid=6x4;cellDeg=5;buttonDeg=3.5;...;input=gaze` | Once, at the start |
| `order` | `row=3;n=16 2 8 4` | Once: Latin square row and block order |
| `block_start` | `block=1;n=8;layout=random` | Start of each block (block 0 = practice) |
| `baseline_start` / `baseline_end` | `block=1;n=8` | 5 s with only the fixation cross, before the block's trials (pupil baseline) |
| `fixation_on` | `block=1;n=8` | First frame of the fixation cross |
| `cue_on` | `block=1;n=8;target=K` | First frame of the target letter |
| `buttons_on` | `block=1;n=8;target=K;tx=-7.6;ty=2.4;layout=K:-7.6:2.4 B:4.9:-7.3 ...` | First frame with the buttons (RT time zero) |
| `response` | `chosen=K;correct=1;rt_ms=812;px=-7.1;py=2.0;err_deg=0.8;src=gaze` | Space press near a button; `px`/`py` = where gaze hit the panel |
| `press_ignored` | `reason=no_gaze` or `reason=off_buttons;px=...;py=...` | Space press that didn't pick a button |
| `timeout` | `block=1;n=8;target=K` | No answer within 5 s |
| `buttons_off` | `block=1;n=8` | First frame without the buttons |
| `block_end` | `block=1;n=8` | End of each block |
| `break_start` / `break_end` | | Pause screen between blocks (participant presses Space) |

## Conditions and trial counts

- Factor: number of buttons, n = 2, 4, 8, 16 (1.6 to 4.1 bits in the Hick model).
- Positions and letters randomized every trial (visual search). Setting **Layout** to `FixedEachBlock` keeps one layout per block instead (the Hick condition, not yet scheduled).
- 4 practice trials with n = 4 (block 0), then 20 trials per level in 4 blocks.
- Block order: row (participant number mod 4) of a balanced Latin square: row 0 = 2, 4, 16, 8; row 1 = 4, 8, 2, 16; row 2 = 8, 16, 4, 2; row 3 = 16, 2, 8, 4.
- Expected length: about 4 s per trial, so roughly 7 minutes including baselines and breaks.

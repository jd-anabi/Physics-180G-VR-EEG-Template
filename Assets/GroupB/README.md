# Group B: interface difficulty and information throughput

Your scene(s) and scripts go in this folder. Start with docs/03-build-your-scene.md.

Keep this file up to date. The analysis depends on it.

## Scene

- Scene file: `GroupB_Buttons.unity` (a copy of `Examples/TaskTemplate.unity`; exposure set to Fixed, EV 13, in `GroupB_SkyandFog.asset`)
- Script: `ButtonSearchTask.cs` on the **Stim Control** object. It builds the panel and buttons itself when the experiment starts; all settings are in the Inspector.
- Material: `ButtonMat.mat` (HDRP/Unlit, copied per object; colors are drawn as emission with Exposure Weight 0)
- `LowGpuMode.cs` on **Sky and Fog Volume**: for trying the scene on a laptop with integrated graphics, where the template's physically based sky makes the GPU hang as soon as Unity draws the scene. When on, it turns the sky, the static lighting sky and the fog off in Edit and Play mode without changing the scene file. It is a per-computer setting (EditorPrefs), switched with **Tools > Group B > Low-GPU Mode**, so it is never committed. Until it is set on a computer it is on for Intel graphics and off otherwise, so it is off on the lab PC. Switching it off on a laptop that needs it makes Unity hang.
- Who edits the scene: Charles Victorio

## Task

Each trial: a fixation cross at the panel's center (0.8–1.2 s), then the target letter at the center (1 s), then n lettered buttons. The participant looks at the button with that letter and presses **Space**; the button closest to the gaze direction is the answer. Without the headset (**Use Eye Tracking** unticked) the mouse pointer stands in for the gaze.

There are two kinds of blocks:

- **Random layout** (visual search): new positions and letters every trial, so the participant has to search the panel. Prediction: T = a + b·n.
- **Fixed layout** (Hick): one layout for the whole block. The block starts with a study period that shows the buttons without a cue for 0.6 s per button (at least 3 s: 3, 3, 4.8 and 9.6 s for n = 2, 4, 8, 16), so the participant can learn where each letter is and then only has to choose. Prediction: T = a + b·log₂(n+1). The targets are drawn without replacement, refilled when all buttons have been the target, so each button is the target about equally often.

The pause screen before each block says which kind comes next.

- Panel: 34° × 24°, grey, at `textPos` (≈0.5 m away). Buttons: 3.5° squares on a 6 × 4 grid of 5° cells, at most one per cell, jittered so neighbors stay ≥0.75° apart. Letters from `ABCDEFGHJKLMNPRSTUVXYZ`, all different within a trial.
- Brightness: buttons are slightly lighter than the panel and their letters dark, so a button with its letter is about as bright as the panel behind it and mean luminance barely changes with n. Check this in the headset.
- A press counts only when the gaze is within 4° of a button's center; during a blink the last valid gaze sample (≤0.2 s old) is used. Other presses are logged as `press_ignored` and the trial goes on.

## Events logged

Trial column = trial number in the session (practice included). Positions are degrees from the panel's center, + = right / up.

| Event | Value | When it is logged |
|---|---|---|
| `settings` | `viewDist=0.500;grid=6x4;cellDeg=5;buttonDeg=3.5;...;trialsPerBlock=12;layouts=random fixed;...;input=gaze` | Once, at the start |
| `order` | `row=3;layouts=fixed random;n=16 2 8 4` | Once: Latin square row, layout order and level order |
| `block_start` | `block=1;n=8;layout=fixed` | Start of each block (block 0 = practice, random layout) |
| `baseline_start` / `baseline_end` | `block=1;n=8;layout=fixed` | 5 s with only the fixation cross, before the block's trials (pupil baseline) |
| `study_start` | `block=1;n=8;layout=fixed;buttons=K:-7.6:2.4 B:4.9:-7.3 ...` | Fixed blocks only: first frame of the study period (buttons, no cue) |
| `study_end` | `block=1;n=8;layout=fixed` | First frame after the study period |
| `fixation_on` | `block=1;n=8;layout=fixed;i=3` | First frame of the fixation cross; `i` = trial number in the block (1–12) |
| `cue_on` | `block=1;n=8;layout=fixed;i=3;target=K;rep=1` | First frame of the target letter; `rep` (fixed blocks only) = how often K was the target earlier in this block |
| `buttons_on` | `block=1;n=8;layout=fixed;i=3;target=K;rep=1;tx=-7.6;ty=2.4;buttons=K:-7.6:2.4 B:4.9:-7.3 ...` | First frame with the buttons (RT time zero); `tx`/`ty` = target position, `buttons` = every button |
| `response` | `chosen=K;correct=1;rt_ms=812;px=-7.1;py=2.0;err_deg=0.8;src=gaze` | Space press near a button; `px`/`py` = where gaze hit the panel |
| `press_ignored` | `reason=no_gaze` or `reason=off_buttons;px=...;py=...` | Space press that didn't pick a button |
| `timeout` | `block=1;n=8;layout=fixed;i=3;target=K;rep=1` | No answer within 5 s |
| `buttons_off` | `block=1;n=8;layout=fixed;i=3` | First frame without the buttons |
| `block_end` | `block=1;n=8;layout=fixed` | End of each block |
| `break_start` / `break_end` | | Pause screen before each block (participant presses Space) |

## Conditions and trial counts

- Factors: number of buttons, n = 2, 4, 8, 16 (1.6 to 4.1 bits in the Hick model), × layout, random or fixed.
- 4 practice trials with n = 4 and a random layout (block 0), then 8 blocks of 12 trials: all four levels with one layout, then all four with the other (96 trials).
- Order, from the participant number p (the digits of the participant code):
  - Layout order: even p starts with random, odd p with fixed.
  - Level order: row (p mod 4) of a balanced Latin square, the same in both halves. Row 0 = 2, 4, 16, 8; row 1 = 4, 8, 2, 16; row 2 = 8, 16, 4, 2; row 3 = 16, 2, 8, 4.
  - So any 4 consecutive participant numbers use every row once and each layout order twice. Within each layout order, every level comes once early (1st or 2nd block of a half) and once late (3rd or 4th).
- Expected length: about 9–10 minutes. 96 trials at about 3.7 s each (fixation 1 s, cue 1 s, RT about 1.2 s, 0.5 s gap) take 6 min; baselines 45 s, study periods 20 s, practice 20 s, 8 pauses about 1.5 min, instructions about 30 s.

## Analysis plan

- Mean RT of correct trials per participant, n and layout. Fit T = a + b·n (search) and T = a + b·log₂(n+1) (Hick) to each layout and compare the fits (e.g. R² or AIC per participant). We expect search to fit the random layout and Hick to fit the fixed layout.
- In a fixed block the participant learns the layout as the block goes on: with n = 16 and 12 trials each letter is the target less than once on average. So we plot RT against `i` (trial number in the block) and against `rep` for each n, and check whether the fixed-layout result holds when the first trials of each block are left out (or with `i` as a covariate).
- Pupil: mean pupil size after `buttons_on` minus the block's baseline, against n and layout.

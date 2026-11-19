# Group A: SSVEP frequency response

Your scene(s) and scripts go in this folder. Start with docs/03-build-your-scene.md.

Keep this file up to date. The analysis depends on it.

> **Current protocol: alpha perturbation** (`AlphaPerturbTask.cs`), described in [the last section](#alpha-perturbation-protocol-alphaperturbtaskcs). After talking with the professor we perturb the frequency around 10 Hz instead of running a full sweep. The sweep (`SSVEPTask.cs`, `GroupA_SSVEP.unity`), described in the sections below, is kept for reference only.

## Scene (sweep, reference only)

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

---

# Alpha perturbation protocol (`AlphaPerturbTask.cs`)

Replaces the full sweep, following the professor's suggestion.

## Motivation

The visual cortex responds most strongly to flicker near 10 Hz, and alpha oscillations entrain to it (Herrmann 2001). The limitation: not everyone's alpha synchronizes at exactly 10 Hz. Each person's individual alpha frequency (IAF) is a bit lower or higher, and entrainment is strongest when the flicker is close to that frequency (the "Arnold tongue"; Notbohm et al. 2016). So we:

1. establish a **baseline at 10 Hz**, letting alpha lock to the flicker;
2. **perturb** the frequency up or down and watch how the alpha phase follows.

We expect the alpha phase to change with a delay of about 5 s after each frequency change, so each frequency is held for **5 s between measurements**.

Scene: duplicate `GroupA_SSVEP.unity` → `GroupA_AlphaPerturb.unity`, then on **Stim Control** remove `SSVEP Task` and add `Alpha Perturb Task`. Same `patch` and `fixation` objects.

## Design (defaults, all editable in the Inspector)

- **Continuous flicker**, sine wave by default. The phase is accumulated frame by frame (`phase += f / refreshHz`), so any frequency works (not only 90/n Hz) and the stimulus phase never jumps when the frequency changes.
- **Segments of 5 s** (`stepSeconds`): one alpha-phase measurement per segment, which leaves time for the expected ~5 s lag.
- **Each block:** 30 s baseline at 10 Hz (6 segments), then each perturbation for 5 s followed by 5 s back at 10 Hz, in random order.
- **Perturbations** (`offsets`) are fractions of `baseHz`: −0.2, −0.1, +0.1, +0.2 → 8, 9, 11, 12 Hz. ±20% spans the usual range of individual alpha frequencies (≈8–12 Hz). **To confirm with the professor:** he mentioned changing by 20%; we read that as the largest perturbation. If he meant something else, change `offsets`.
- All five frequencies run a whole number of cycles in 5 s (40–60), so every segment starts at the same point of the wave.
- 3 blocks × 2 repetitions of each offset = 6 per offset. 110 s per block, about 5.5 min of flicker plus breaks (Space) between blocks.
- Optional `noFlickerSeconds`: fixation-only rest before the first block, to measure each person's own alpha frequency (off by default).

## Phase continuity

The script keeps one running stimulus phase (in cycles) and advances it every frame by `f / refreshHz`; the brightness is computed from that phase. When the frequency changes, the phase is not reset; only its rate changes (φ(t) = ∫ f dt). So a step in frequency gives a kink in the phase, not a jump. A jump would be a phase reset, which the brain responds to on its own and would be confounded with the frequency change. Relative to a 10 Hz rhythm, the stimulus drifts by 360° per second per 1 Hz of offset; that drift is the perturbation the alpha has to follow.

## Why a sine wave, not a square wave

At 90 Hz, 8, 11 and 12 Hz are not a whole number of frames per cycle (11.25, 8.18, 7.5). A square wave's on/off edges would be rounded to frames, so cycle lengths alternate (e.g. 7 and 8 frames at 12 Hz) and individual edges are off by up to ~5.5 ms (≈ ±20° at 10 Hz). Because phase is what we measure, we use a sine wave, which sets the exact brightness every frame at any frequency. Trade-off: a full on/off square flicker may drive entrainment somewhat harder. If alpha locking is weak in pilots, make the patch brighter (Notbohm et al. 2016: phase locking increases with intensity); `Waveform` can also be switched to `Square` in the Inspector. 

### Keeping the sine sinusoidal in light

Two things in the render path are nonlinear, so the sine would not be a sine at the eye:

1. **Colour space.** The project is in linear colour space, where `Material.SetColor` treats a colour as sRGB and converts it (a level of 0.5 would become ~0.21). `AlphaPerturbTask` now interpolates in linear space and writes with `SetVector`, which is not converted. Done in code.
2. **Tonemapping.** HDRP's default volume profile applies **ACES** tonemapping, and neither the shared sky profile nor our copy overrides it. **Do this once in Unity:** select `SkyandFogSettingsProfile 1` (our group's copy) → **Add Override → Post-processing → Tonemapping** → tick **Mode** and set it to **None**. While there, consider adding **Bloom** with Intensity 0: the default profile has bloom at 0.2, which spreads a glow from the bright patch that also flickers.

Then check on the headset that the patch still looks right (without ACES, bright parts of the scene may look harsher). A photometer or a phone camera on the lens at a slow flicker is the only way to confirm the luminance is actually sinusoidal. The Varjo display's own response is outside our control. The square wave only uses two levels (black and white), so none of this affects it.

## Before recording

- Check the frame rate on the lab PC with the headset. The stimulus phase advances by f / 90 per rendered frame, so dropped frames lower every frequency. Each `segment_end` row reports dropped frames; they should be 0. (Test runs on a laptop without the headset don't reach 90 fps, so their `long_frames` counts don't say anything about recording day.)

## Events logged

Trial column = segment number across the session (1, 2, 3, ...).

| Event | Value | When |
|---|---|---|
| `settings` | all parameters | once |
| `no_flicker_start` / `no_flicker_end` | | only if `noFlickerSeconds > 0` |
| `block_start` | `block=1;order=+0.200 -0.100 ...` (Trial = block) | start of each block |
| `segment` | `f=11.000Hz;offset=+0.100;kind=perturb;block=1;phase=312.4567` | first rendered frame of each 5 s segment; `kind` = baseline / perturb / return; `phase` = stimulus phase in cycles |
| `flick_frame` | `level=0.734;phase=312.5678` | every frame |
| `segment_end` | `long_frames=<k>` | end of each segment (dropped frames) |
| `flicker_off`, `block_end` | `block=<b>` | end of each block |
| `break_start` / `break_end` | | between blocks |

Analysis: epoch the EEG (O1/O2) on `segment` events, band-pass around the segment's `f`, and compare the alpha phase with the stimulus phase (from `flick_frame`'s `phase`) over each 5 s segment, especially how long the phase difference takes to settle after a `perturb` or `return` step.

## References

- Herrmann, C. S. (2001). Human EEG responses to 1–100 Hz flicker: resonance phenomena in visual cortex and their potential correlation to cognitive phenomena. *Experimental Brain Research*, 137, 346–353. https://doi.org/10.1007/s002210100682
- Notbohm, A., Kurths, J., & Herrmann, C. S. (2016). Modification of brain oscillations via rhythmic light stimulation provides evidence for entrainment but not for superposition of event-related responses. *Frontiers in Human Neuroscience*, 10, 10. https://doi.org/10.3389/fnhum.2016.00010

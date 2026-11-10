# 2. How a session runs

Every group's scene runs the same sequence. The shared parts come from `ExperimentBase`; your group writes step 5.

| Step | Participant sees | Operator does |
|---|---|---|
| 1. Calibration | Varjo's calibration dots | Nothing, unless it fails: **Backslash** restarts it |
| 2. Participant code | A text box | Press **Space** to select the box, type the code (e.g. `P07`), press **Enter** |
| 3. Instructions | Your instruction text | Participant reads; press **Space** to continue |
| 4. Countdown | "Starting in 3, 2, 1" | Nothing |
| 5. Your experiment | Your trials or blocks | Whatever your design needs |
| 6. End | "Thank you…" | Ask for the closing sync gesture, then press **Space**. Play mode stops by itself |

**Escape** stops the scene at any time and writes an `aborted` row to the event log.

**Right Ctrl** turns the eye log off and on by hand. Do not press it during a recording.

The eye log starts as soon as calibration succeeds, so the participant-code screen is a good moment for the opening sync gesture (see [Recording day](04-recording-day.md)).

## Files each run creates

All on the lab PC, inside the Unity project:

| File | Written by | Contents |
|---|---|---|
| `Assets/Data/<code><scene>events-<date-time>.csv` | EventLogger | One row per event you log |
| `Assets/Logs/<code><scene>eyeTr-<date-time>.csv` | EyeTrackingControl | Gaze, validity, pupil size, eye openness, ~200 Hz |

Example for participant `P07` running Group C's scene `GroupC_Arithmetic`:

```
P07GroupC_Arithmeticevents-2026-11-19-13-05-12.csv
P07GroupC_ArithmeticeyeTr-2026-11-19-13-05-12.csv
```

The two files share the code, scene name and date-time, so the analysis code pairs them automatically. The eye log is written under a temporary name (`eyeTr-<time>.csv`) during the run and gets its final name when Play mode stops. If Unity crashes, look for the temporary name.

The EEG is recorded separately in EmotivPRO and exported as its own CSV.

## One participant, three scenes

Each participant runs all three groups' scenes in one sitting, one after another, entering the same participant code each time. Each scene produces its own pair of files.

`EyeTrackingControl` recalibrates at the start of every scene by default. If the headset has not been taken off, you can untick **Calibrate On Start** on the `eyeTracking` object to save about a minute per scene.

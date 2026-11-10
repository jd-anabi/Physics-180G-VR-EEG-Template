# 5. Data and sync

## Data rules

- **No participant data on GitHub.** Not in this repo, not in a fork, not in a gist. Recordings live in the course's shared data folder only.
- **Codes, not names.** File names and notes use participant codes (`P01`, `P02`, …). The list linking codes to people, if one is needed at all, stays with the TA.
- **Keep data files out of AI coding tools.** Let the AI write code that reads the files; do not upload or paste the recordings themselves into the tool.

## File formats

### Event log: `<code><scene>events-<date-time>.csv`

Comma-separated, one row per `EventLogger.Log` call.

| Column | Meaning |
|---|---|
| `UnixMs` | Time of the row, milliseconds since 1 Jan 1970 (lab PC clock) |
| `Frame` | Unity frame number (`Time.frameCount`); consecutive frames are ~11.1 ms apart |
| `Trial` | Trial or block number passed to `Log` (−1 if none) |
| `Event` | Event name, e.g. `stim_on` |
| `Value` | Condition or extra information, e.g. `15Hz` |

Every file also contains `session_start`, `experiment_start`, `experiment_end` and `session_end` (or `aborted`).

### Eye log: `<code><scene>eyeTr-<date-time>.csv`

**Semicolon**-separated (the gaze vector contains commas), one row per eye-tracker sample, ~200 Hz. It starts when calibration succeeds and stops when Play mode stops.

| Column | Meaning | Units |
|---|---|---|
| `CaptureTime` | When the eye cameras took the sample | Unix ms |
| `CalcXEccentricity` | Horizontal gaze angle, + = right, `atan(x/z)` | degrees |
| `CalcYEccentricity` | Vertical gaze angle, + = up, `atan(y/z)` | degrees |
| `CombinedGazeForward` | Gaze direction vector (x, y, z), both eyes combined | unit vector |
| `Valid` | `VALID`, or `INVALID` during blinks and tracking loss | |
| `LeftPupilDiameterMM`, `RightPupilDiameterMM` | Pupil diameter; 0 when that eye is not tracked | mm |
| `LeftEyeOpenness`, `RightEyeOpenness` | 0 = closed, 1 = open | |

Gaze is measured relative to the headset, so head movement is not included.

### EEG: the EmotivPRO CSV export

The first line is a header with recording information (including the sampling rate); the second line has the column names. Use the `Timestamp` column (Unix **seconds**, already corrected by EmotivPRO for wireless delay) and pick channels by name (`EEG.O2`), not by column number. `CQ.<channel>` columns give contact quality (0–4). `rsst_lab.load_emotiv` and `vreeg.load_eeg` handle all of this.

## One clock

Unity (`EventLogger`), the eye tracker (`CaptureTime`, converted to Unix time by the Varjo plugin) and EmotivPRO (`Timestamp`) all report Unix time from the same lab PC. To find the EEG around an event, look up the event's `UnixMs` in the EEG's `Timestamp × 1000`. No resampling, no assumption that samples are exactly 1/fs apart: EmotivPRO's sample spacing differs from the nominal rate by a few hundred ppm, which adds up to tens of ms over a minute if you count samples instead of using timestamps.

What is left after that is small and can be checked:

- **A constant offset** between the eye tracker and the EEG of a few ms (different paths through the system). `vreeg.estimate_offset` measures it from the sync gesture: horizontal eye movements change the voltage between F8 and F7, because the eye is an electric dipole (cornea positive relative to retina). The function finds the lag that best lines up the change in F8 − F7 with the change in horizontal gaze angle. `check_session.py` prints it for every session.
- **The display delay**: an event is logged when Unity finishes a frame; the image appears one to two frames later. It is roughly constant, so it cancels in comparisons between conditions.
- **Frame quantization**: events can only happen on frame boundaries, ~11 ms apart.

If a timing jitter σ is present, it blurs averages of phase-locked responses: an oscillation at frequency f keeps a fraction exp(−(2πfσ)²/2) of its amplitude in a trial average. For σ = 5 ms that is 0.95 at 10 Hz but 0.45 at 40 Hz, which is why timing matters most for Group A's higher frequencies.

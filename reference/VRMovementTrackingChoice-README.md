# VR Movement Tracking Choice

A Unity (2022.3.12f1) eye-tracking experiment for the Varjo headset. A participant follows a moving black ball. The ball stops and splits into two balls that move apart vertically, one **up** and one **down**. We measure how long it takes the eyes to start following one of them, and which one they pick.

This README covers what each output file contains, how the MATLAB scripts turn those files into results, and how to compare our data against the external dataset or other outside data.

---

## 1. What happens in a trial

```
 cue (dot)        black ball moves           ball splits            balls move apart
 appears   ──►    left or right      ──►     into 2 balls   ──►     up and down
                  (5–15°, random)                                    (10°)
   │                 │                          │                      │
CueShowTime     moveStartTime               SplitTime               endTime
```

- The black ball moves horizontally at **10°/s** for a random 5–15°. Left or right is random.
- At the split, the red ball goes up or down at random. The second ball (blue, green, etc.) always goes the opposite way at the same speed.
- One session is **50 trials** by default.

**Protocols (Unity scenes):**

| Scene | Protocol name in analysis | What appears after the split |
|---|---|---|
| `Simple` | SimpleTwo (`S`) | Two balls of the **same** color |
| `SimpleOneBall` | SimpleOne (`SO`) | **One** ball, up or down |
| `Choice` | Choice (`C`) | Two balls of **different** colors (red/blue) |
| `RGChoice`, `GYChoice`, `BYChoice`, `BGChoice`, `RYChoice` | Color choice | Two balls in that color pair (R=red, G=green, B=blue, Y=yellow) |
| `ShapeChoice` | Shape choice | Two different shapes |
| `ChoiceButton`, `SimpleButton` | Button versions | Same as above, plus the participant presses **V** or **B** |

---

## 2. Output files

Each session writes **two CSV files with the same name and timestamp**, so you can pair them:

```
<ParticipantID><SceneName>rtData-YYYY-MM-DD-HH-MM-SS.csv   ← trial events  (Assets/Data/)
<ParticipantID><SceneName>eyeTr-YYYY-MM-DD-HH-MM-SS.csv    ← raw gaze      (Assets/Logs/)
```

Example: `LVNJChoicertData-2025-05-08-13-01-22.csv` and `LVNJChoiceeyeTr-2025-05-08-13-01-22.csv`.

`Assets/Data/runData.csv` also gets one line per session with the computer, file name, and number of trials completed.

### All times are Unix milliseconds

Every timestamp in both files is **milliseconds since 1 Jan 1970**, taken from the same PC clock. Both files therefore share one timeline: subtract one time from another to get a duration in ms, and use them to find eye samples that fall inside a trial.

### 2a. `rtData` — one row per trial

Comma-separated, one header row, one row per trial.

| Column | Meaning | Units |
|---|---|---|
| `CueShowTime` | Cue dot appears (trial start). If the cue is turned off, this is just the trial start. | Unix ms |
| `moveStartTime` | Cue disappears, black ball appears and starts moving. | Unix ms |
| `direction` | Black ball direction: `1` = right, `-1` = left. | — |
| `SplitTime` | **The black ball stops and splits.** This is time 0 for the analysis. | Unix ms |
| `xEcc` | Horizontal angle where the ball stopped. + = right of center. | degrees |
| `x`, `y`, `z` | Unity world position of the split point (`y` is always 0). | meters |
| `endTime` | The two balls finish moving. | Unix ms |
| `redDirection` | Red ball direction: **`1` = down, `-1` = up**. The other ball does the opposite. | — |
| `redXEcc` | Horizontal angle of the red ball at the end. Equals `xEcc` because it only moves vertically. | degrees |
| `redYEcc` | Vertical angle of the red ball at the end (about ±10). + = up. | degrees |
| `buttonPressTime` | *(button scenes only)* When V or B was pressed. `0` = no press. | Unix ms |
| `buttonPressed` | *(button scenes only)* `V`, `B`, or `None`. | — |

⚠️ `redDirection` is easy to misread. **`1` means the red ball went DOWN.** To check, look at `redYEcc`: it's negative when `redDirection = 1`.

Durations you can compute from these columns:
- Black ball travel time: `SplitTime − moveStartTime`
- Button reaction time: `buttonPressTime − SplitTime` (skip rows where it's `0`)

### 2b. `eyeTr` — raw gaze samples

**Semicolon-separated** (`;`, not commas), one row per eye-tracker sample, about **200 Hz** (one row every ~5 ms). It runs from the end of calibration until the program closes, so it includes time between trials.

| Column | Meaning | Units |
|---|---|---|
| `CaptureTime` | When the eye tracker captured this sample. | Unix ms |
| `CalcXEccentricity` | Horizontal gaze angle. + = right. `atan(x/z)` of the gaze direction. | degrees |
| `CalcYEccentricity` | Vertical gaze angle. + = up. `atan(y/z)` of the gaze direction. | degrees |
| `CombinedGazeForward` | Raw gaze direction vector `(x, y, z)` (both eyes combined). | unit vector |
| `Valid` | `VALID` or `INVALID` (blink, lost tracking). | — |

Things to know:
- Gaze is measured **relative to the headset**, not the room. Head movement is not removed.
- `INVALID` rows are not filtered out by the analysis scripts. Blinks can show up as spikes.
- Read the file in MATLAB with `readtable(file, 'Delimiter', ';')`. The vector column contains commas, which is why the file uses semicolons.

---

## 3. How the analysis turns this into a "reaction time"

The main result is the **saccade latency after the split**: how many ms after `SplitTime` the eyes make their first large vertical movement toward one of the balls.

The MATLAB scripts (`mvmtChoiceAnalysis.m`, `aggregateAnalysis.m`) do this for each trial:

1. **Window:** take eye samples from 400 ms before to 1000 ms after `SplitTime`. Times are re-zeroed so `SplitTime = 0`.
2. **Smooth:** apply a 3-sample moving average to the X/Y eccentricity.
3. **Vertical velocity:** `diff(Y) / diff(time)`, in °/s.
4. **Find the first peak** in |vertical velocity| after a minimum time, with at least a minimum prominence (see the table below). That peak's time is the trial's **reaction time**.
5. **Up or down:** if the average Y in the window is ≥ 0, the trial counts as **Up**. Otherwise it counts as **Down**. This tells you which ball the eyes followed.
6. **Remove outliers** per participant per protocol with the 1.5×IQR rule. Then report the **mean, SD, SEM, and 95% CI (1.96×SEM)**.

⚠️ In the code, the variable `reaction_times = SplitTime − CueShowTime` is **not** the reaction time. It's only used to recover `SplitTime`. The real result is `peak_times`.

⚠️ Because this measures the time of **peak velocity**, not saccade **onset**, values come out later than onset-based latencies in the literature. Keep this in mind when comparing (see section 5).

### Scripts

| Script | Use it for | Output |
|---|---|---|
| `mvmtChoiceAnalysis.m` | One session. Set the two file paths at the top. | Per-trial plots (`Trial_N_YEcc.png`, `Trial_N_YVelocity.png`) and histograms (`YVelPeakHist.png`, `YVelPeakHist_by_avgY.png`) in `./Figures` |
| `aggregateAnalysis.m` | All participants in one folder, comparing SimpleTwo / SimpleOne / Choice | `Figures/Aggregate/Comparison_*.png`, `YvelPeakComparison_*.png` |
| `aggregateAnalysisColors.m` | All color-pair Choice sessions in one folder | Color comparison figures |

**The scripts do not use the same settings.** Results from different scripts are not directly comparable unless you make these match:

| Setting | `mvmtChoiceAnalysis.m` | `aggregateAnalysis.m` |
|---|---|---|
| Earliest peak counted | 100 ms (hard-coded) | `minPeakTime = 20` ms |
| Minimum peak prominence | 100 °/s (hard-coded) | `minPeakProminence = 50` °/s |
| Vertical offset subtracted | `yOffset = 1`° | `yOffset = 0` |

`mvmtChoiceAnalysis.m` currently loops over **trials 10–20 only** (`for i = 10:20%1:length(...)`). Change it back to `1:length(reaction_times)` for a full session.

### Reading the aggregate figures

- **`Comparison_A_vs_B.png`:** each colored dot is one participant's mean reaction time in protocol A (x) vs protocol B (y), with 95% CI bars. The black dot is the group mean with a t-based 95% CI.
  - Above the diagonal: slower in B.
  - Below the diagonal: slower in A.
- **`YvelPeakComparison_Protocol_X.png`:** per participant, mean reaction time on **Down** trials (x) vs **Up** trials (y). Points off the diagonal mean an up/down asymmetry.
- **`YvelPeakComparison_AllProtocols.png`:** the same plot with all protocols overlaid. Marker shape shows the protocol (○ SimpleTwo, □ SimpleOne, △ Choice).
- **`YVelPeakHist.png`:** the distribution of one participant's reaction times, with the mean (red) and 95% CI (green).

---

## 4. Data folders

| Folder | Contents | File-name style |
|---|---|---|
| `FinalData/` | In-lab pilot (Feb 2025), movement protocols | Short codes: `<Name>C…`, `<Name>S…`, `<Name>SO…` |
| `FinalDataMovementExternal/` | **External participants** (May 2025), movement protocols, anonymous 4-letter IDs | Full scene names: `<ID>Choice…`, `<ID>Simple…`, `<ID>SimpleOneBall…` |
| `FinalDataColor/`, `FinalDataColorAnonymized/` | Color-pair Choice sessions | `<ID><ColorPair>Choice…` |
| `FinalDataButton/` | Button-press sessions | `<ID>CB…` / `<ID>RB…` |
| `FinalDataShape/`, `FinalDataShapes/` | Shape choice sessions | `<ID>ShapeChoice…` |
| `Figures/` | Generated plots, per participant and `Aggregate/` | — |

---

## 5. Comparing our data against outside data

### Against the external dataset (`FinalDataMovementExternal/`)

1. **Use the same script and settings for both groups.** Run `aggregateAnalysis.m` on the in-lab folder, then on the external folder, without changing any parameters between runs. Set the folder at the top:
   ```matlab
   dataFolder = 'C:\...\VRMovementTrackingChoice\FinalDataMovementExternal';
   ```
2. **Save the figures between runs.** Figures go to `<parent of dataFolder>/Figures/Aggregate/`. Every `FinalData*` folder sits in the repo root, so **each run overwrites the last one's figures**. Rename or copy `Figures/Aggregate` before the second run.
3. **Check which files were actually used.** The script pairs `rtData` + `eyeTr` files by ID and protocol. Watch for these problems:
   - **Unpaired files are skipped**, with a "Missing file for participant…" message. Some external participants are missing one half of a pair, for example a `SimpleOneBall` rtData file with no eyeTr file.
   - **Repeat sessions:** if a participant has two sessions of the same protocol (a restart), only one is kept. Delete or move the bad/partial one first. A short `rtData` file (<51 lines) is usually an aborted run.
   - **IDs are case-normalized.** `dira` and `DIRA` count as the same person, `Dira`.
   - **Shape files** (`…ShapeChoice…`) are read as participant `<ID>Shape` doing `Choice`. Remove them from the folder before running a movement-only comparison.
4. **Compare like with like.** Compare the same protocol across groups, for example the mean Choice reaction time of in-lab vs external participants. Use the per-participant means stored in `overall_stats` in the MATLAB workspace. If you combine both folders into one, compare protocols within each participant.

### Against published or other-lab data

Before putting our numbers next to someone else's, check:

- **Definition of reaction time.** Ours is the time from **ball split to the first vertical velocity peak**. Most papers report **saccade onset** (first sample above a velocity threshold), which comes earlier. Either compare against peak-based values, or add an onset detection step.
- **Time zero.** Ours is `SplitTime`, when the target changes direction. Theirs may be target appearance or a go signal.
- **Units.** Our angles are degrees of visual angle relative to the headset. Velocities are °/s and times are ms.
- **Sampling and filtering.** Ours is about 200 Hz with a 3-sample moving average. A lower sampling rate or heavier filtering shifts peak times.
- **Stimulus.** The targets are 10°/s smooth-pursuit-style moving balls in VR at 0.5 m, not stepped targets on a monitor.
- **Outlier removal.** We drop values outside 1.5×IQR per participant before averaging. Report this when comparing.
- **Statistic.** Our CIs are 1.96×SEM per participant, and a t-based 95% CI across participants for the group mean.

---

## 6. Quick-start checklist

1. Run a scene in Unity. Collect `Assets/Data/*rtData*.csv` and `Assets/Logs/*eyeTr*.csv`.
2. Check the pair has matching timestamps and the rtData file has 50 trial rows.
3. Move both files into the right `FinalData*` folder.
4. For one session, open `mvmtChoiceAnalysis.m`, set the `rtData` / `eyeData` paths, and run it.
5. For a group, open `aggregateAnalysis.m`, set `dataFolder`, and run it. Copy `Figures/Aggregate` somewhere safe before running another group.

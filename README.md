# Physics 180G Final Projects: VR + EEG (Fall 2026)

This repository is the starting point for all three final projects. It contains one Unity project for the Varjo headset, with the shared parts already working, plus Python code to load and line up the recordings.

| Group | Project | Your folder |
|---|---|---|
| A | SSVEP: frequency response of the visual system | `Assets/GroupA/` |
| B | Interface difficulty and information throughput | `Assets/GroupB/` |
| C | Mental arithmetic: alpha suppression and pupil dilation | `Assets/GroupC/` |

The project descriptions, schedule and recording-day rules are in the course doc. This repo covers the software.

## What is already done for you

- **Eye tracking.** Calibration and a ~200 Hz gaze log, now with pupil diameter (mm) and eye openness for each eye (`Assets/Scripts/Shared/EyeTrackingControl.cs`).
- **Event logging.** One line of code writes a timestamped row for anything your experiment does (`EventLogger.Log("stim_on", "15Hz", trial)`).
- **Session flow.** Calibration, participant code, instructions, countdown and the end screen are handled by `ExperimentBase`. You write only the part in the middle: your trials or blocks.
- **One clock.** Unity events, the eye tracker and EmotivPRO all use the lab PC's clock (Unix time), so the files line up by timestamp.
- **Analysis loaders.** `analysis/vreeg.py` reads all three file types, cuts EEG epochs around events, and checks the EEG / eye-tracker sync. `analysis/check_session.py` checks a session right after recording.
- **A working example.** `Assets/Examples/TaskTemplate.unity` runs `ExampleTask.cs`, a 10-trial reaction-time task, end to end.

## Start here

1. [Setup](docs/01-setup.md): install Unity, Git and GitHub Desktop, clone this repo, open the project.
2. [How a session runs](docs/02-how-a-session-runs.md): what the participant and operator see, which keys do what, which files come out.
3. [Build your scene](docs/03-build-your-scene.md): copy the template, write `RunExperiment()`, log your events. Includes notes for each group.
4. [Recording day](docs/04-recording-day.md): the station checklist, in order.
5. [Data and sync](docs/05-data-and-sync.md): file formats, the shared clock, the data rules.
6. [Analysis](docs/06-analysis.md): Python setup and how to use `vreeg.py`.

## Three rules

1. **Participant data never goes on GitHub.** Unity saves recordings inside `Assets/Data` and `Assets/Logs`. Those folders are ignored by Git; move the files to the course's shared data folder after every session. Use participant codes, never names.
2. **Edit only your own group's folder** (`Assets/GroupA`, `GroupB` or `GroupC`). Unity scene files do not merge well; two people editing the same scene will lose work. If something shared needs to change (`Assets/Scripts/Shared`, `analysis/vreeg.py`), tell the TA and the other groups first.
3. **Pull before you start, commit and push when you stop.** Small, frequent commits with clear messages.

## Repository map

```
Assets/
  Scripts/Shared/        shared code: EyeTrackingControl, ExperimentBase, EventLogger, SessionInfo
  Examples/              TaskTemplate.unity + ExampleTask.cs (start from these)
                         ShapeChoice.unity, SimpleButton.unity: the lab's original experiments (reference)
  GroupA/ GroupB/ GroupC/  your scenes and scripts
  Data/  Logs/           recordings land here (ignored by Git)
analysis/
  vreeg.py               load, line up and epoch the recordings
  check_session.py       quick check after each recording
  rsst_lab.py            the lab's RSST analysis in Python (from the VR + EEG week)
  tests/test_vreeg.py    self-test on a synthetic session
docs/                    the guides listed above
reference/               the lab's original README and MATLAB analysis for ShapeChoice
```

## Credits

The Unity project, eye-tracking logger and example experiments come from the Elegant Mind Club's VRMovementTrackingChoice project (MIT license, see `LICENSE`). The Physics 180G additions are the shared session code, pupil logging, event logging, analysis code and documentation.

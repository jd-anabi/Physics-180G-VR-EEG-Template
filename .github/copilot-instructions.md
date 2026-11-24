# Code review instructions: Physics 180G VR + EEG final projects

Context: a Unity 2022.3.12f1 (HDRP) project for a Varjo headset, recorded together with an Emotiv EPOC X EEG. Three undergraduate groups (A, B, C) each build one experiment scene. Students are new to Unity and Git. Be specific, explain why something matters, and skip style nitpicks.

Start every review with a one-line verdict: "Ready to merge" or "Needs changes". Then list blocking issues, then suggestions.

## Check every pull request

1. **Scope.** Changes should be inside one group folder (`Assets/GroupA`, `Assets/GroupB` or `Assets/GroupC`), each asset with its `.meta` file. Flag changes to `Assets/Scripts/Shared`, `Assets/Examples`, `analysis/`, `docs/`, `ProjectSettings/`, `Packages/` or `.gitignore` unless the description explains why a shared change is needed.
2. **No data.** Block any recording or personal data anywhere: `.csv`, `.edf`, `.bdf`, `.xdf`, screenshots of data, or files containing participant names. Participant data must never be committed.
3. **Scene wiring.** When a scene (`.unity`) and a script are added, the scene must reference the script's GUID (from its `.cs.meta`) in an `m_Script` line. If the scene still references ExampleTask (GUID `4258189aa3a44cccbaa5b28ec037f44e`) and not the group's script, it was probably not saved before committing: blocking.
4. **Script structure.** The experiment class inherits `ExperimentBase` and implements `RunExperiment()`. If it overrides `Start()` or `Update()`, it must call `base.Start()` / `base.Update()`. The class name must match the file name. Loops inside coroutines must `yield`.
5. **Event logging.** Every stimulus change, condition change, response and block boundary the analysis needs is logged with `EventLogger.Log(event, value, trial)`. Visual events are logged after `yield return new WaitForEndOfFrame()`. Conditions go in `value`; numbers use `CultureInfo.InvariantCulture`. The session's randomized order should be logged.
6. **Timing.** Flicker is driven by `Time.frameCount`, not timers. Durations use `WaitForSecondsRealtime`. Flag `Time.time`, `WaitForSeconds` (time-scale dependent) and timing based on `Update()` delta sums.
7. **Brightness.** A scene's `sharedProfile` must not point to the shared `Assets/Settings/SkyandFogSettingsProfile.asset` (GUID `8ba92e2dd7f884a0f88b98fa2d235fe7`), which uses automatic exposure. Groups clone it into their folder and set Exposure mode to Fixed (`mode: m_Value: 0`). Automatic exposure changes the luminance the participant sees, which affects flicker amplitude and pupil size.
8. **Safety.** Group A's flicker must be preceded by the photosensitivity screening described in `docs/04-recording-day.md`; do not add flicker to other groups' scenes.

## Group-specific physics

- **Group A (SSVEP).** Frequencies are 90/n Hz. The EPOC X records only up to about 43 Hz, so anything above ~40 Hz cannot be measured. For the phase-latency fit, neighbouring frequencies between 7.5 and 18 Hz should be at most ~3 Hz apart. Square waves with odd n have an on-fraction D different from 0.5; the stimulus fundamental scales as sin(pi D), so n must be logged. Whole cycles per trial are preferred.
- **Group B (buttons).** Button positions randomized every trial and logged with the target; total bright area roughly constant across levels (pupil follows luminance).
- **Group C (arithmetic).** The display is identical in rest and task (cues by audio); block onsets and offsets are logged with the level; block lengths are jittered (20-30 s); the participant's spoken report is logged and falls in the rest block, not during the task.

## Session length

Each scene must fit the shared session: about 7 min (A), 10 min (B), 12 min (C). Flag designs that clearly exceed this.

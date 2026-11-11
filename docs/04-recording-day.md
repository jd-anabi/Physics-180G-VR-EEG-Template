# 4. Recording day

One participant at a time, about 45 minutes each, all three groups' scenes in one sitting. Print this page and keep it at the station.

## Roles

| Role | Does |
|---|---|
| Participant | Sits still, follows the instructions, can stop at any time |
| EEG operator | Fits the Emotiv, watches contact quality, starts and stops the EEG recording, exports it |
| VR operator | Runs the Unity scenes, types the participant code, presses the keys |
| Timekeeper | Writes down the participant code, scene order, start times, file names and anything unusual |

## Before the participant arrives

- [ ] Lab PC: GitHub Desktop → switch to `main` → pull the latest version; open the project in Unity Hub. Record only from `main`, so every recording uses reviewed code.
- [ ] Varjo Base is running and the headset is tracked.
- [ ] EmotivPRO is open, the EPOC X is charged, saline and towels are ready.
- [ ] **EmotivPRO and Unity run on the same PC.** The files only line up because they share that PC's clock.
- [ ] Look up this participant's scene order in the session log (the order rotates between participants).

## With the participant

1. **Consent and screening.** Taking part is voluntary. Ask about any history of epilepsy or seizures; anyone who says yes skips Group A's flicker scene.
2. **Fit the EEG.** Wet the sensors, fit the EPOC X, and wait until contact is green on every channel, especially O1, O2, F7 and F8.
3. **Put the VR headset on top and check contact again.** The straps press on the back and front sensors and often break contact.
4. **Start the EEG recording** in EmotivPRO, named with the participant code. Use the same sampling rate for everyone (128 Hz over Bluetooth unless the course has decided on 256 Hz with the USB receiver).
5. **Run the first scene.** Open it in Unity, press Play. After calibration, the participant-code screen appears.
6. **Opening sync gesture** (while the code screen is up): the participant looks far left, then far right, three times, then blinks three times. This lets the analysis check the EEG / eye-tracker alignment.
7. Type the participant code (Space, type, Enter) and continue with Space.
8. At the end screen: **closing sync gesture** again, then Space. Play mode stops.
9. Open the next scene and repeat steps 5 to 8, with the same participant code. If the headset stayed on, you can untick **Calibrate On Start** on the `eyeTracking` object.
10. After the last scene, **stop the EEG recording** and export it as CSV.

If something goes wrong in a scene, press **Escape** (it logs `aborted`), note it, and rerun that scene. Do not edit scripts during a recording session.

## After the participant

- [ ] Copy the files into the shared data folder, one subfolder per participant code:
      `Assets/Data/<code>…events-….csv`, `Assets/Logs/<code>…eyeTr-….csv`, and the EmotivPRO CSV.
- [ ] Run the check on that folder (see [Analysis](06-analysis.md)):
      `python analysis/check_session.py <shared folder>/<code> --plot`
      It should find every scene's session, report ~200 Hz eye data with a high valid fraction, and say the EEG covers each session.
- [ ] Once the copy is confirmed, delete the files from `Assets/Data` and `Assets/Logs` on the lab PC.
- [ ] Rinse and dry the EEG sensors.
- [ ] Timekeeper: complete the session log entry.

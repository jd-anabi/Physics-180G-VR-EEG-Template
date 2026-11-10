# 6. Analysis

All analysis code is Python, in `analysis/`. Run it from that folder.

## Setup

```
cd analysis
pip install -r requirements.txt
python tests/test_vreeg.py
```

The self-test writes a fake session (events, eye log, EmotivPRO-style EEG with a known 25 ms clock offset), reads it back, epochs it, and checks that the offset is recovered. It should end with `all checks passed`.

## Check a session right after recording

```
python check_session.py "<shared data folder>/P07" --plot
```

For each scene the participant ran, it prints the event counts, the eye-tracker rate and valid fraction, whether pupil size was recorded, the EEG contact quality, whether the EEG recording covers the scene, and the EEG / eye-tracker offset from the sync gesture. With `--plot` it saves a figure of gaze, pupil and F8 − F7 per session in the same folder.

What to look for: eye data at ~200 Hz with most samples valid (a few percent invalid is normal for blinks; one past lab recording had only 8% valid samples, which leaves almost nothing to analyze), contact 3–4 on the channels you need, and an offset of at most a few tens of ms with r above ~0.3. If something is off, rerun that scene while the participant is still there.

## Loading and lining up: `vreeg.py`

```python
import numpy as np
import vreeg

folder = "<shared data folder>/P07"
s = [x for x in vreeg.find_sessions(folder) if "GroupA" in x["prefix"]][0]
ev  = vreeg.load_events(s["events"])           # DataFrame: UnixMs, Frame, Trial, Event, Value
eye = vreeg.load_eye(s["eyeTr"])               # DataFrame: t_ms, x_deg, y_deg, valid, pupil_l_mm, ...
eeg = vreeg.load_eeg(vreeg.find_emotiv(folder)[0])   # dict: t_ms, fs, eeg{channel}, contact{channel}

# optional: put the eye tracker exactly on the EEG clock
tau_ms, r = vreeg.estimate_offset(eeg, eye)
eye["t_ms"] = eye["t_ms"] + tau_ms

# EEG segments around each stimulus onset
onsets = ev.loc[ev.Event == "stim_on", "UnixMs"].to_numpy()
lags_s, seg = vreeg.epochs(eeg["eeg"]["O2"], eeg["t_ms"], onsets, tmin_s=-1.0, tmax_s=10.0, fs=eeg["fs"])
# seg has one row per onset; average with np.nanmean(seg, axis=0)

# pupil on the EEG time base (NaN in blinks)
pupil = vreeg.interp_to(eeg["t_ms"], eye["t_ms"], eye["pupil_l_mm"])
```

| Function | Does |
|---|---|
| `find_sessions(folder)` | Pairs each scene's events / eyeTr files by code, scene and date-time |
| `find_emotiv(folder)` | Finds EmotivPRO exports in the folder |
| `load_events`, `load_eye`, `load_eeg`, `load_rtdata` | Read each file type (see [Data and sync](05-data-and-sync.md)) |
| `epochs(x, t_ms, event_ms, tmin_s, tmax_s, fs)` | Fixed-length segments around events, from the sample nearest each event |
| `interp_to(t_target, t_src, values)` | Puts gaze or pupil on another time base, leaving gaps as NaN |
| `estimate_offset(eeg, eye)` | EEG / eye-tracker clock offset from horizontal eye movements |
| `nearest_index(t, t_query)` | Index of the sample closest to each query time |

`rsst_lab.py` is the RSST analysis from the VR + EEG week (`load_emotiv`, `rsst_analysis`, `rsst_figure`, `band_signal`).

## What is not here

Filtering, artifact rejection and your group's own measures (SSVEP amplitude and phase, response-time fits, band power and time constants) are part of the projects. Build them on top of these loaders, and share the preprocessing step between groups as agreed in the group plans, so all three use the same filtering and the same rejection of eye artifacts.

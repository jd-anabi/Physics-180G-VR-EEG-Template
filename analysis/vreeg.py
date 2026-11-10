"""vreeg: load and line up the files from one VR + EEG session (Physics 180G, Fall 2026).

One session produces up to three kinds of files, all timestamped on the same PC clock
(Unix time, i.e. seconds or milliseconds since 1 Jan 1970):

    <ID><Scene>events-<date-time>.csv   EventLogger (Unity): one row per event, UnixMs
    <ID><Scene>eyeTr-<date-time>.csv    EyeTrackingControl (Unity): ~200 Hz gaze + pupil, CaptureTime in ms
    <...>EPOCX<...>.csv                 EmotivPRO export: EEG, Timestamp in seconds

Because the clocks are the same, lining them up is a lookup by time, not a resampling problem.
Typical use:

    import vreeg
    s = vreeg.find_sessions("data/P07")[0]
    ev = vreeg.load_events(s["events"])
    eye = vreeg.load_eye(s["eyeTr"])
    eeg = vreeg.load_eeg("data/P07/P07_EPOCX_..._2026.11.19T13.02.11.08.00.csv")
    onsets = ev.loc[ev.Event == "stim_on", "UnixMs"].to_numpy()
    lags, seg = vreeg.epochs(eeg["eeg"]["O2"], eeg["t_ms"], onsets, -0.5, 2.0, eeg["fs"])

Run `python check_session.py <folder>` right after recording to check a session.
"""
import os
import re
import glob

import numpy as np
import pandas as pd
from scipy import signal

from rsst_lab import load_emotiv, CHANNELS

NAME_RE = re.compile(
    r"^(?P<prefix>.*?)(?P<kind>events|eyeTr|rtData)-(?P<stamp>\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2})\.csv$")


# --------------------------------------------------------------------------- finding files
def find_sessions(folder):
    """Group the Unity files in `folder` by session (same name prefix and date-time).
    Returns a list of dicts: prefix, stamp, and the paths for 'events', 'eyeTr', 'rtData'
    (None when that file is missing). Sorted by date-time."""
    sessions = {}
    for path in glob.glob(os.path.join(folder, "*.csv")):
        m = NAME_RE.match(os.path.basename(path))
        if not m:
            continue
        key = (m["prefix"], m["stamp"])
        s = sessions.setdefault(key, dict(prefix=m["prefix"], stamp=m["stamp"],
                                          events=None, eyeTr=None, rtData=None))
        s[m["kind"]] = path
    return [sessions[k] for k in sorted(sessions, key=lambda k: k[1])]


def find_emotiv(folder):
    """EmotivPRO CSV exports in `folder` (recognised by their EEG.<channel> columns)."""
    out = []
    for path in sorted(glob.glob(os.path.join(folder, "*.csv"))):
        if NAME_RE.match(os.path.basename(path)) or path.endswith("_intervalMarker.csv"):
            continue
        with open(path, encoding="utf-8", errors="replace") as fh:
            fh.readline()
            second = fh.readline()
        if "Timestamp" in second and "EEG." in second:
            out.append(path)
    return out


# --------------------------------------------------------------------------- loading
def load_events(path):
    """EventLogger file -> DataFrame with UnixMs (int), Frame, Trial, Event, Value (str)."""
    df = pd.read_csv(path, dtype={"Event": str, "Value": str}, keep_default_na=False)
    df["UnixMs"] = df["UnixMs"].astype("int64")
    return df


def load_rtdata(path):
    """The lab's StimControl trial file (one row per trial, times in Unix ms)."""
    return pd.read_csv(path)


def load_eye(path):
    """EyeTrackingControl file -> DataFrame with columns
        t_ms        capture time, Unix ms (int)
        x_deg       horizontal gaze angle, degrees (+ right); NaN when invalid
        y_deg       vertical gaze angle, degrees (+ up); NaN when invalid
        valid       True / False
        pupil_l_mm, pupil_r_mm, open_l, open_r   (only in files from this repo's logger;
                    pupil is NaN where the headset reported 0, i.e. the eye was not tracked)
    Works for the lab's older 5-column files too."""
    df = pd.read_csv(path, sep=";")
    out = pd.DataFrame({
        "t_ms": df["CaptureTime"].astype("int64"),
        "x_deg": pd.to_numeric(df["CalcXEccentricity"], errors="coerce"),
        "y_deg": pd.to_numeric(df["CalcYEccentricity"], errors="coerce"),
        "valid": df["Valid"].astype(str).str.strip().eq("VALID"),
    })
    out.loc[~out["valid"], ["x_deg", "y_deg"]] = np.nan
    extra = {"LeftPupilDiameterMM": "pupil_l_mm", "RightPupilDiameterMM": "pupil_r_mm",
             "LeftEyeOpenness": "open_l", "RightEyeOpenness": "open_r"}
    for src, dst in extra.items():
        if src in df:
            out[dst] = pd.to_numeric(df[src], errors="coerce")
    for col in ("pupil_l_mm", "pupil_r_mm"):
        if col in out:
            out.loc[out[col] <= 0, col] = np.nan
    return out.sort_values("t_ms", kind="stable").reset_index(drop=True)


def load_eeg(path):
    """EmotivPRO CSV -> the dict from rsst_lab.load_emotiv plus t_ms (Unix ms, float).
    Uses the corrected 'Timestamp' column; do not assume samples are exactly 1/fs apart."""
    rec = load_emotiv(path)
    rec["t_ms"] = rec["t_unix"] * 1000.0
    return rec


# --------------------------------------------------------------------------- lining up
def nearest_index(t_sorted, t_query):
    """Index of the sample in t_sorted (ascending) closest in time to each t_query."""
    t_sorted = np.asarray(t_sorted, float)
    t_query = np.atleast_1d(np.asarray(t_query, float))
    i = np.clip(np.searchsorted(t_sorted, t_query), 1, len(t_sorted) - 1)
    left_closer = (t_query - t_sorted[i - 1]) <= (t_sorted[i] - t_query)
    return np.where(left_closer, i - 1, i)


def epochs(x, t_ms, event_ms, tmin_s, tmax_s, fs):
    """Cut a fixed-length segment of signal x around each event.

    x        1-D signal, sampled at times t_ms (Unix ms, ascending)
    event_ms event times (Unix ms), e.g. from load_events
    tmin_s, tmax_s  window relative to the event, seconds (tmin_s is usually negative)
    fs       nominal sampling rate (Hz), sets the number of samples per segment

    Each segment starts from the sample nearest the event (error at most half a sample,
    3.9 ms at 128 Hz). Returns (lags_s, segments) with segments of shape
    (n_events, n_samples); segments that run off either end of the recording are NaN."""
    x = np.asarray(x, float)
    n0, n1 = int(round(tmin_s * fs)), int(round(tmax_s * fs))
    lags = np.arange(n0, n1 + 1) / fs
    idx = nearest_index(t_ms, event_ms)
    seg = np.full((len(idx), len(lags)), np.nan)
    for k, i in enumerate(idx):
        a, b = i + n0, i + n1 + 1
        if a >= 0 and b <= len(x):
            seg[k] = x[a:b]
    return lags, seg


def interp_to(t_target_ms, t_src_ms, values, max_gap_ms=20.0):
    """Linear interpolation of `values` (sampled at t_src_ms) onto t_target_ms, skipping NaNs.
    Points further than max_gap_ms from any valid source sample (blinks, gaps) become NaN.
    Use it to put gaze or pupil on the EEG time base."""
    t_src = np.asarray(t_src_ms, float)
    v = np.asarray(values, float)
    ok = np.isfinite(v)
    t_src, v = t_src[ok], v[ok]
    t_target = np.asarray(t_target_ms, float)
    out = np.interp(t_target, t_src, v, left=np.nan, right=np.nan)
    j = nearest_index(t_src, t_target)
    out[np.abs(t_src[j] - t_target) > max_gap_ms] = np.nan
    return out


def estimate_offset(eeg, eye, left="F7", right="F8", max_lag_ms=300.0, step_ms=1.0, lowpass_hz=15.0,
                    window_ms=None):
    """Check the clock alignment between EEG and eye tracker with eye movements.

    Horizontal eye movements change the voltage between F8 and F7 (the eye is an electric
    dipole), so HEOG(t) = V_F8 - V_F7 should follow the horizontal gaze angle theta(t).
    This finds the lag tau that maximises the correlation between d/dt HEOG(t) and
    d/dt theta(t - tau). Derivatives are used so slow drifts do not dominate.

    Needs a stretch with clear left-right eye movements (the sync gesture: look far left
    and far right a few times at the start and end of the recording).

    window_ms: optional (start, stop) in Unix ms to use only part of the recording,
    e.g. the first minute with the sync gesture. Shorter windows are much faster.

    Returns (tau_ms, r): add tau_ms to eye-tracker times to put them on the EEG clock.
    |tau| of a few to ~20 ms is expected; r below ~0.3 means the estimate is not reliable."""
    fs = eeg["fs"]
    t = np.asarray(eeg["t_ms"], float)
    heog = np.asarray(eeg["eeg"][right], float) - np.asarray(eeg["eeg"][left], float)
    if window_ms is not None:
        keep = (t >= window_ms[0]) & (t <= window_ms[1])
        t, heog = t[keep], heog[keep]
    b, a = signal.butter(2, lowpass_hz / (0.5 * fs))
    heog = signal.filtfilt(b, a, heog - np.nanmean(heog))
    dv = np.gradient(heog)
    lags = np.arange(-max_lag_ms, max_lag_ms + step_ms / 2, step_ms)
    best_tau, best_r = np.nan, -np.inf
    for tau in lags:
        g = interp_to(t - tau, eye["t_ms"], eye["x_deg"])
        dg = np.gradient(g)
        ok = np.isfinite(dg) & np.isfinite(dv)
        if ok.sum() < 5 * fs:  # need at least ~5 s of overlap
            continue
        r = np.corrcoef(dv[ok], dg[ok])[0, 1]
        if r > best_r:
            best_tau, best_r = tau, r
    return float(best_tau), float(best_r)

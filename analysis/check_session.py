"""Quick check of a recording session. Run it right after recording, before the next participant.

    python check_session.py <folder>            # folder with the session's files
    python check_session.py <folder> --plot     # also save check_<prefix>.png in that folder

It prints, for each Unity session in the folder:
  - event log: duration, number of rows, the most common events
  - eye log: sampling rate, fraction of valid samples, whether pupil size was recorded
  - EEG (if an EmotivPRO CSV is in the folder): sampling rate, contact quality per channel,
    whether its time range covers the Unity session
  - the EEG / eye-tracker clock offset from horizontal eye movements (see vreeg.estimate_offset)
"""
import argparse
import os
import sys

import numpy as np

import vreeg


def fmt_time(ms):
    import datetime
    return datetime.datetime.fromtimestamp(ms / 1000).strftime("%H:%M:%S")


def check(folder, plot=False):
    sessions = vreeg.find_sessions(folder)
    emotiv = vreeg.find_emotiv(folder)
    if not sessions:
        print(f"No Unity session files (*events-*.csv / *eyeTr-*.csv) found in {folder}")
    eegs = []
    for path in emotiv:
        rec = vreeg.load_eeg(path)
        eegs.append((path, rec))
        bad = [ch for ch, q in rec["contact"].items() if q < 3]
        dur = (rec["t_ms"][-1] - rec["t_ms"][0]) / 1000
        print(f"\nEEG  {os.path.basename(path)}")
        print(f"     {fmt_time(rec['t_ms'][0])}-{fmt_time(rec['t_ms'][-1])}  ({dur:.0f} s), fs = {rec['fs']:g} Hz")
        print(f"     contact (0-4): {rec['contact']}")
        if bad:
            print(f"     WARNING: weak contact (<3) on {', '.join(bad)}")

    for s in sessions:
        print(f"\nSession {s['prefix']}  {s['stamp']}")
        t0 = t1 = None
        ev = eye = None
        if s["events"]:
            ev = vreeg.load_events(s["events"])
            t0, t1 = ev["UnixMs"].min(), ev["UnixMs"].max()
            top = ev["Event"].value_counts().head(6).to_dict()
            print(f"  events  {len(ev)} rows, {fmt_time(t0)}-{fmt_time(t1)} ({(t1 - t0) / 1000:.0f} s); {top}")
            if "aborted" in set(ev["Event"]):
                print("  WARNING: session was aborted (Escape)")
        else:
            print("  events  MISSING")
        if s["eyeTr"]:
            eye = vreeg.load_eye(s["eyeTr"])
            dt = np.diff(eye["t_ms"].to_numpy())
            rate = 1000 / np.median(dt) if len(dt) else float("nan")
            pupil = "yes" if "pupil_l_mm" in eye else "no (old logger)"
            line = f"  eye     {len(eye)} samples, ~{rate:.0f} Hz, {100 * eye['valid'].mean():.0f}% valid, pupil: {pupil}"
            if "pupil_l_mm" in eye:
                line += f" ({100 * eye['pupil_l_mm'].notna().mean():.0f}% of samples have a left pupil value)"
            print(line)
            if t0 is None:
                t0, t1 = eye["t_ms"].min(), eye["t_ms"].max()
        else:
            print("  eye     MISSING (no eye log, or Play mode was not stopped normally)")
        if s["rtData"]:
            print(f"  rtData  {len(vreeg.load_rtdata(s['rtData']))} trials")

        match = None
        for path, rec in eegs:
            if t0 is not None and rec["t_ms"][0] <= t0 and rec["t_ms"][-1] >= t1:
                match = (path, rec)
        if eegs and match is None and t0 is not None:
            print("  WARNING: no EEG file covers this whole session (start EEG recording before Unity, stop it after)")
        if match and eye is not None:
            path, rec = match
            print(f"  EEG     {os.path.basename(path)} covers this session")
            tau, r = vreeg.estimate_offset(rec, eye, window_ms=(t0, t1))
            note = "" if r >= 0.3 else "  (weak: do the left-right sync gesture at the start and end)"
            print(f"  sync    eye-tracker -> EEG offset {tau:+.0f} ms, r = {r:.2f}{note}")
        if plot and eye is not None:
            save_plot(folder, s, ev, eye, match[1] if match else None)


def save_plot(folder, s, ev, eye, rec):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    t_ref = eye["t_ms"].iloc[0]
    fig, axes = plt.subplots(3 if rec is not None else 2, 1, figsize=(11, 7), sharex=True)
    ax = axes[0]
    ax.plot((eye["t_ms"] - t_ref) / 1000, eye["x_deg"], lw=0.6, label="horizontal")
    ax.plot((eye["t_ms"] - t_ref) / 1000, eye["y_deg"], lw=0.6, label="vertical")
    ax.set_ylabel("Gaze (deg)")
    ax.legend(loc="upper right", frameon=False)
    ax = axes[1]
    if "pupil_l_mm" in eye:
        ax.plot((eye["t_ms"] - t_ref) / 1000, eye["pupil_l_mm"], lw=0.6, label="left")
        ax.plot((eye["t_ms"] - t_ref) / 1000, eye["pupil_r_mm"], lw=0.6, label="right")
        ax.legend(loc="upper right", frameon=False)
    ax.set_ylabel("Pupil (mm)")
    if rec is not None:
        ax = axes[2]
        heog = rec["eeg"]["F8"] - rec["eeg"]["F7"]
        ax.plot((rec["t_ms"] - t_ref) / 1000, heog - np.median(heog), lw=0.5, color="k")
        ax.set_ylabel("F8 - F7 (uV)")
    if ev is not None:
        for a in axes:
            for t in ev["UnixMs"]:
                a.axvline((t - t_ref) / 1000, color="0.85", lw=0.5, zorder=0)
    axes[-1].set_xlabel("Time from start of eye log (s)")
    fig.suptitle(f"{s['prefix']}  {s['stamp']}")
    out = os.path.join(folder, f"check_{s['prefix']}_{s['stamp']}.png")
    fig.savefig(out, dpi=120)
    plt.close(fig)
    print(f"  plot    {out}")


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("folder")
    p.add_argument("--plot", action="store_true")
    a = p.parse_args()
    if not os.path.isdir(a.folder):
        sys.exit(f"Not a folder: {a.folder}")
    check(a.folder, plot=a.plot)

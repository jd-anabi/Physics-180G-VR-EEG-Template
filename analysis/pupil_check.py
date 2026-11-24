"""Did the eye tracker see the pupil react to light? (Group C pupil check)

    cd analysis
    python pupil_check.py "<folder with the PupilCheck files>"

Reads the events and eyeTr files of the GroupC_PupilCheck scene, averages the left and
right pupil diameter, and for every light and dark phase takes the mean over its last
half (the pupil constricts within ~1 s of light but takes several seconds to re-dilate,
so the last half is the settled size). Prints a table, the light vs. dark difference,
and saves a plot of pupil size over time next to the data.

What a working tracker shows: dark phases clearly larger than light phases, typically
by 1-3 mm (e.g. ~3 mm in white, ~5-6 mm in black), the same way every cycle.
"""
import os
import sys

import numpy as np
import pandas as pd

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vreeg  # noqa: E402

SCENE_TAG = "PupilCheck"


def analyze(folder, plot=True):
    sessions = [s for s in vreeg.find_sessions(folder) if SCENE_TAG in s["prefix"]]
    if not sessions:
        raise SystemExit(f"No {SCENE_TAG} session in {folder}")
    s = sessions[-1]
    if not s["eyeTr"]:
        raise SystemExit("No eye log (eyeTr file) for this session. Was Use Eye Tracking ticked?")
    ev = vreeg.load_events(s["events"])
    eye = vreeg.load_eye(s["eyeTr"])
    if "pupil_l_mm" not in eye:
        raise SystemExit("This eye log has no pupil columns (old logger?).")
    pupil = eye[["pupil_l_mm", "pupil_r_mm"]].mean(axis=1, skipna=True).to_numpy()
    t = eye["t_ms"].to_numpy(float)

    starts = ev[ev.Event == "block_start"].reset_index(drop=True)
    ends = ev[ev.Event == "block_end"].reset_index(drop=True)
    rows = []
    for _, b in starts.iterrows():
        e = ends[ends.UnixMs > b.UnixMs]
        if e.empty:
            continue
        t0, t1 = b.UnixMs, e.UnixMs.iloc[0]
        m = (t >= (t0 + t1) / 2) & (t < t1)
        rows.append(dict(cycle=int(b.Trial), phase=b.Value, length_s=(t1 - t0) / 1000,
                         pupil_mm=np.nanmean(pupil[m]) if m.any() else np.nan,
                         tracked=float(np.mean(np.isfinite(pupil[m]))) if m.any() else 0.0))
    tab = pd.DataFrame(rows)

    print(f"\nSession {s['prefix']}  ({len(eye)} eye samples, "
          f"{len(eye) / ((t[-1] - t[0]) / 1000):.0f} Hz, "
          f"pupil found in {np.mean(np.isfinite(pupil)):.0%} of samples)")
    print(tab.round(2).to_string(index=False))
    light = tab.loc[tab.phase == "light", "pupil_mm"].dropna()
    dark = tab.loc[tab.phase == "dark", "pupil_mm"].dropna()
    if len(light) and len(dark):
        d = dark.mean() - light.mean()
        print(f"\nlight {light.mean():.2f} mm   dark {dark.mean():.2f} mm   difference {d:+.2f} mm")
        if d > 0.5 and (dark.min() > light.max()):
            print("-> The tracker sees the pupil react to light: every dark phase is larger than every light phase.")
        else:
            print("-> No clear light response. Check that the headset showed white/black, that the "
                  "participant looked at the dot, and the pupil columns in the eye log.")

    if plot:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        fig, ax = plt.subplots(figsize=(10, 3.5))
        tt0 = t[0]
        for _, b in starts.iterrows():
            e = ends[ends.UnixMs > b.UnixMs]
            if b.Value == "dark" and not e.empty:
                ax.axvspan((b.UnixMs - tt0) / 1000, (e.UnixMs.iloc[0] - tt0) / 1000, color="0.85")
        ax.plot((t - tt0) / 1000, pupil, lw=0.8)
        ax.set(xlabel="Time (s)", ylabel="Pupil diameter (mm)", title="Pupil check (grey = dark phases)")
        fig.tight_layout()
        out = os.path.join(folder, s["prefix"] + "_pupil_check.png")
        fig.savefig(out, dpi=150)
        print("Saved", out)
    return tab


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    analyze(sys.argv[1])

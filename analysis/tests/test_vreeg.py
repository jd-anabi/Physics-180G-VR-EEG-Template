"""Self-test for vreeg.py on a synthetic session with a known clock offset.

    cd analysis
    python tests/test_vreeg.py

Writes a fake session (events, eye log, EmotivPRO-style CSV) to a temporary folder,
checks that the loaders read it back, that epochs land on the right samples, and that
estimate_offset recovers the offset that was put in.
"""
import os
import sys
import tempfile

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import vreeg  # noqa: E402
from rsst_lab import CHANNELS  # noqa: E402

TRUE_OFFSET_MS = 25.0   # eye-tracker stamps are this much earlier than the EEG's
FS = 128.0


def gaze_path(t_s):
    """Sync gesture (left-right steps of +-15 deg every 0.8 s for 10 s), then small drifts."""
    x = np.where(t_s < 10, 15 * np.sign(np.sin(2 * np.pi * t_s / 1.6)), 0.0)
    x = x + 0.3 * np.sin(2 * np.pi * 0.2 * t_s)
    # smooth the steps a little, as real saccades take ~40 ms
    k = np.exp(-0.5 * (np.arange(-20, 21) / 6.0) ** 2)
    return np.convolve(x, k / k.sum(), mode="same")


def write_session(folder, rng):
    t_start_ms = 1_763_500_000_000  # an arbitrary Unix time in Nov 2025
    dur_s = 60.0
    # eye log, 200 Hz, new 9-column format
    te = np.arange(0, dur_s, 0.005)
    x = gaze_path(te + TRUE_OFFSET_MS / 1000)       # stamps early by the offset
    y = 0.2 * rng.standard_normal(te.size)
    valid = rng.random(te.size) > 0.02
    pupil = 3.5 + 0.3 * np.sin(2 * np.pi * te / 20)
    eye_path = os.path.join(folder, "P99GroupC_ArithmeticeyeTr-2025-11-18-13-00-00.csv")
    with open(eye_path, "w") as fh:
        fh.write("CaptureTime;CalcXEccentricity;CalcYEccentricity;CombinedGazeForward;Valid;"
                 "LeftPupilDiameterMM;RightPupilDiameterMM;LeftEyeOpenness;RightEyeOpenness\n")
        for ti, xi, yi, vi, pi in zip(te, x, y, valid, pupil):
            if vi:
                fh.write(f"{t_start_ms + int(round(ti * 1000))};{xi:.6f};{yi:.6f};(0.0, 0.0, 1.0);VALID;"
                         f"{pi:.6f};{pi:.6f};1.000000;1.000000\n")
            else:
                fh.write(f"{t_start_ms + int(round(ti * 1000))};NaN;NaN;(0.0, 0.0, 0.0);INVALID;"
                         f"0.000000;0.000000;0.000000;0.000000\n")
    # events: one stim_on every 5 s
    ev_path = os.path.join(folder, "P99GroupC_Arithmeticevents-2025-11-18-13-00-00.csv")
    onsets = t_start_ms + np.arange(12, 58, 5) * 1000
    with open(ev_path, "w") as fh:
        fh.write("UnixMs,Frame,Trial,Event,Value\n")
        fh.write(f"{t_start_ms + 500},1,-1,session_start,eyeTracking=True\n")
        for k, t in enumerate(onsets):
            fh.write(f"{t},{100 + k},{k + 1},stim_on,level=7\n")
    # EEG, 128 Hz, starts 2 s before and ends 2 s after the Unity session
    tg = np.arange(-2, dur_s + 2, 1 / FS)
    heog = 8.0 * gaze_path(tg)                      # ~8 uV per degree
    eeg_path = os.path.join(folder, "P99_EPOCX_000000_2025.11.18T12.59.58.08.00.csv")
    with open(eeg_path, "w") as fh:
        fh.write("title:P99, recorded:18.11.25 12.59.58, headset type:EPOCX, "
                 "sampling rate:eeg_128;mot_64;pm_0.1, version:2.0\n")
        cols = ["Timestamp", "OriginalTimestamp", "EEG.Counter", "EEG.Interpolated"] + \
               [f"EEG.{c}" for c in CHANNELS] + [f"CQ.{c}" for c in CHANNELS] + ["MarkerValueInt"]
        fh.write(",".join(cols) + "\n")
        for i, ti in enumerate(tg):
            vals = {c: 4200 + 5 * rng.standard_normal() for c in CHANNELS}
            vals["F8"] += heog[i] / 2
            vals["F7"] -= heog[i] / 2
            # a 10 Hz burst right after every stim_on, to test epochs
            for t_on in (onsets - t_start_ms) / 1000:
                if 0 <= ti - t_on < 0.5:
                    vals["O2"] += 20 * np.sin(2 * np.pi * 10 * (ti - t_on))
            ts = (t_start_ms / 1000) + ti
            row = [f"{ts:.6f}", f"{ts:.6f}", str(i % 128), "0"] + [f"{vals[c]:.4f}" for c in CHANNELS] + \
                  ["4"] * len(CHANNELS) + [""]
            fh.write(",".join(row) + "\n")
    return onsets


def main():
    rng = np.random.default_rng(0)
    with tempfile.TemporaryDirectory() as folder:
        onsets = write_session(folder, rng)
        sessions = vreeg.find_sessions(folder)
        assert len(sessions) == 1 and sessions[0]["events"] and sessions[0]["eyeTr"], sessions
        ev = vreeg.load_events(sessions[0]["events"])
        eye = vreeg.load_eye(sessions[0]["eyeTr"])
        emo = vreeg.find_emotiv(folder)
        assert len(emo) == 1, emo
        eeg = vreeg.load_eeg(emo[0])
        assert eeg["fs"] == 128.0
        assert "pupil_l_mm" in eye and eye["pupil_l_mm"].isna().mean() > 0.01  # zeros became NaN
        assert eye["x_deg"].isna().sum() == (~eye["valid"]).sum()

        on = ev.loc[ev.Event == "stim_on", "UnixMs"].to_numpy()
        assert np.array_equal(on, onsets)
        lags, seg = vreeg.epochs(eeg["eeg"]["O2"], eeg["t_ms"], on, -0.2, 0.6, eeg["fs"])
        avg = np.nanmean(seg, axis=0) - np.nanmean(seg[:, lags < 0])
        burst = np.abs(avg[(lags >= 0) & (lags < 0.5)]).max()
        base = np.abs(avg[lags < 0]).max()
        assert burst > 4 * base, (burst, base)

        tau, r = vreeg.estimate_offset(eeg, eye, window_ms=(eye.t_ms.iloc[0], eye.t_ms.iloc[0] + 20000))
        print(f"estimated offset {tau:+.1f} ms (true {TRUE_OFFSET_MS:+.1f}), r = {r:.2f}")
        assert abs(tau - TRUE_OFFSET_MS) <= 4, tau
        assert r > 0.5, r

        import check_session
        check_session.check(folder, plot=True)
        assert any(f.startswith("check_") for f in os.listdir(folder))
    print("all checks passed")


if __name__ == "__main__":
    main()

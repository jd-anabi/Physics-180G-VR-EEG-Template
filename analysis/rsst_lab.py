"""RSST analysis of Emotiv EPOC X recordings, Physics M180G (Fall 2026).

Python version of the Arisaka lab's MATLAB code (RSST repo: rsst.m and RSSTAnalysis.m,
version 1011.2022, default settings). Checked against the lab's own example
(EOEC3030 recording, O2, 256 Hz): same scalogram, FFT alpha 1.03, RSST alpha 0.49.

Typical use:

    from rsst_lab import load_emotiv, rsst_figure
    rec = load_emotiv("G1P1_EPOCX_123456_2026.11.03T13.10.00.08.00.csv")
    print(rec["fs"], rec["contact"])                 # 128 Hz over Bluetooth; contact 0-4 per channel
    rsst_figure(rec["eeg"]["O2"], rec["fs"], t=rec["t"], markers=rec["markers"],
                title="G1P1, O2", save="G1P1_O2_rsst.png")

Notes
- Pick channels by name (rec["eeg"]["O2"]). Column numbers change between exports.
- fs comes from the file header (128 Hz over Bluetooth). The lab's MATLAB scripts assume 256 Hz.
- Colours are scaled to the channel's largest value, so one artifact can make alpha look faint.
- The alpha exponents are exploratory: RSST alpha is about FFT alpha minus 0.5 by construction,
  and the default fit runs past the headset's ~43 Hz filter edge (try fit_fmax=40).
"""
import numpy as np
import pandas as pd
from scipy import signal
from scipy.optimize import curve_fit

CHANNELS = ["AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2", "P8", "T8", "FC6", "F4", "F8", "AF4"]


# --------------------------------------------------------------------------- loading
def load_emotiv(path):
    """Read an EmotivPRO CSV export. Returns a dict with
    t (s from start), t_unix (s), fs (Hz, nominal), eeg {channel: uV}, contact {channel: 0-4},
    markers [(t_s, value), ...] from keystroke markers."""
    with open(path, encoding="utf-8", errors="replace") as fh:
        header = fh.readline()
    fs = 128.0
    for item in header.split(","):
        if "sampling rate" in item and "eeg_" in item:
            fs = float(item.split("eeg_")[1].split(";")[0].strip())
    df = pd.read_csv(path, skiprows=1)
    t_unix = df["Timestamp"].to_numpy(float)
    t = t_unix - t_unix[0]
    eeg = {ch: df[f"EEG.{ch}"].to_numpy(float) for ch in CHANNELS if f"EEG.{ch}" in df}
    contact = {ch: int(round(df[f"CQ.{ch}"].median())) for ch in CHANNELS if f"CQ.{ch}" in df}
    markers = []
    if "MarkerValueInt" in df:
        m = df["MarkerValueInt"].notna().to_numpy()
        markers = [(float(tt), int(v)) for tt, v in zip(t[m], df["MarkerValueInt"][m])]
    return dict(t=t, t_unix=t_unix, fs=fs, eeg=eeg, contact=contact, markers=markers)


# --------------------------------------------------------------------------- rsst.m
def rsst_m(x, wave_param, fs, nv=32, thr=1e-8):
    """Synchrosqueezed CWT with an analytic Morlet wavelet (centre frequency wave_param),
    as in the lab's rsst.m. Returns the complex transform Tx (frequency x time) and freqs (Hz)."""
    x = np.asarray(x, float).ravel()
    nb = len(x)
    dt = 1 / fs
    noct = int(np.floor(np.log2(nb))) - 1
    na = noct * nv
    scales = 2.0 ** (np.arange(1, na + 1) / nv)
    x = x - x.mean()
    N = nb
    pos = np.arange(1, N // 2 + 1) * 2 * np.pi / N
    omega = np.concatenate([[0.0], pos, -pos[:(N - 1) // 2][::-1]])
    xdft = np.fft.fft(x)
    freq = 2.0 ** np.linspace(np.log2(1 / (nb * dt)), np.log2(1 / (2 * dt)), na)
    M = na
    l2f = np.log2(1 / N)
    l2n = np.log2(0.5)
    Tx = np.zeros((na, N), complex)
    cols = np.arange(N)
    wpos = omega > 0
    for k in range(na):  # one scale at a time to keep memory small
        wft = np.where(wpos, np.exp(-((scales[k] * omega - wave_param) ** 2) / 2), 0.0)
        cwt = np.fft.ifft(xdft * wft)
        dcwt = np.fft.ifft(xdft * (1j * omega) * wft)
        with np.errstate(divide="ignore", invalid="ignore"):
            ph = np.imag(dcwt / cwt) / (2 * np.pi)          # instantaneous frequency, cycles/sample
            ph[np.abs(ph) < thr] = np.nan
            irow = 1 + np.floor(M / (l2n - l2f) * (np.log2(np.abs(ph)) - l2f))
        ok = np.isfinite(irow) & (irow > 0) & (irow <= M) & (np.abs(cwt) > thr)
        np.add.at(Tx, (irow[ok].astype(int) - 1, cols[ok]), cwt[ok])
    return Tx / nv, freq


# --------------------------------------------------------------------------- RSSTAnalysis.m
def _nextpow2(v):
    return int(np.ceil(np.log2(v)))


def _sgolayfilt(x, order, framelen):
    """MATLAB sgolayfilt (numerically stable for long frames)."""
    x = np.asarray(x, float)
    F = int(framelen)
    h = (F - 1) // 2
    tt = np.arange(-h, h + 1) / max(h, 1)
    V = np.vander(tt, order + 1, increasing=True)
    Q, _ = np.linalg.qr(V)
    B = Q @ Q.T
    y = np.convolve(x, B[h][::-1], mode="same")
    y[:h] = B[:h] @ x[:F]
    y[-h:] = B[h + 1:] @ x[-F:]
    return y


def rsst_analysis(data, fs, wave_param=4, y_min=0.3, y_max=160, fit_fmax=None):
    """Everything RSSTAnalysis.m computes with its default settings (SSWT, Potential,
    removeCOI On, filterAC Off, sensitivity 1). Returns a dict of arrays."""
    y = np.asarray(data, float)
    y = y - y.mean()
    b, a = signal.butter(3, [0.3 / (0.5 * fs), (fs / 2 - 1) / (0.5 * fs)], btype="band")
    y2 = signal.filtfilt(b, a, y, padlen=3 * (max(len(a), len(b)) - 1))
    Nr = len(y2)
    zl = int(np.ceil(Nr / 3))
    y2 = np.concatenate([np.zeros(zl), y2, np.zeros(zl)])  # the lab's zero padding ("removeCOI")
    cfs, freqs = rsst_m(y2, wave_param, fs)
    cfs = cfs[:, zl:zl + Nr]
    c = np.real(cfs)                                         # the R in RSST
    c1 = c / np.max(np.abs(c))                               # signed log colour scale, 2 decades
    disp = np.zeros_like(c1)
    pos, neg = c1 > 0.01, c1 < -0.01
    disp[pos] = np.log10(100 * c1[pos]) / 2
    disp[neg] = -np.log10(100 * np.abs(c1[neg])) / 2
    fd = np.mean(np.abs(c), axis=1)                          # RSST spectrum
    i0 = np.nonzero(freqs > y_min)[0][0]
    i1 = np.nonzero(freqs < y_max)[0][-1]
    fd = fd / np.max(fd[i0:i1 + 1])
    L = len(y2)                                              # FFT spectrum, smoothed as in the lab code
    n = (2 ** _nextpow2(L)) * 16
    P1 = np.abs(np.fft.fft(y2, n) / n)[:n // 2 + 1].copy()
    P1[1:-1] *= 2
    f = fs * np.arange(n // 2 + 1) / n
    j0 = np.nonzero(f > y_min)[0][0]
    j1 = np.nonzero(f < y_max)[0][-1]
    P1 = P1 / np.max(P1[j0:j1 + 1])
    step_value = y_min
    steps = _nextpow2(y_max / step_value) - 1
    step_smooth = (j1 - j0) / (steps * 20)
    P = _sgolayfilt(P1, steps, 101)
    for i in range(1, steps + 1):
        ssi = int(np.round(n / fs * step_value * 1.5))
        win = 101 + 2 * int(np.floor(step_smooth * i / 2))
        seg = P1[ssi - 1:]
        if len(seg) > win:
            P = np.concatenate([P[:ssi - 1], _sgolayfilt(seg, steps - i, win)])
        step_value *= 2
    fmax = y_max if fit_fmax is None else fit_fmax

    def power1(xx, aa, bb):
        return aa * xx ** bb

    def fitpow(xx, yy):
        k = np.polyfit(np.log(xx), np.log(np.clip(yy, 1e-12, None)), 1)
        p, _ = curve_fit(power1, xx, yy, p0=[np.exp(k[1]), k[0]], maxfev=20000)
        return p

    mF = (f > 5) & (f < fmax)
    mR = (freqs > 5) & (freqs < fmax)
    pF = fitpow(f[mF], P[mF])
    pR = fitpow(freqs[mR], fd[mR])
    return dict(y=y, disp=disp, freqs=freqs, Tx=cfs, rsst_spectrum=fd, f=f, fft_spectrum=P,
                fft_fit=pF, rsst_fit=pR, alpha_fft=-pF[1], alpha_rsst=-pR[1], fit_mask_fft=mF, fit_mask_rsst=mR)


def band_signal(R, lo=8, hi=12):
    """Sum of Re RSST over a band: the band's own oscillation (up to the transform's scale factor)."""
    m = (R["freqs"] >= lo) & (R["freqs"] <= hi)
    return np.real(R["Tx"][m].sum(axis=0))


# --------------------------------------------------------------------------- figure
def rsst_figure(x, fs, t=None, markers=(), title="", fit_fmax=None, save=None):
    """The lab's three-panel RSST figure: waveform (top), scalogram (bottom right),
    FFT and RSST spectra with power-law fits (left). Returns (figure, result dict)."""
    import matplotlib.pyplot as plt
    from matplotlib.colors import LinearSegmentedColormap
    R = rsst_analysis(x, fs, fit_fmax=fit_fmax)
    t = np.arange(len(x)) / fs if t is None else np.asarray(t)
    cmap = LinearSegmentedColormap.from_list("rsst", [(0, "#0d366b"), (0.25, "#2a78d6"), (0.47, "#cde2fb"),
                                                      (0.495, "#ffffff"), (0.505, "#ffffff"), (0.53, "#f8c9c8"),
                                                      (0.75, "#e34948"), (1, "#7a1414")])
    fig = plt.figure(figsize=(11, 6.6))
    axw = fig.add_axes([0.25, 0.71, 0.63, 0.19])
    axs = fig.add_axes([0.25, 0.10, 0.63, 0.60], sharex=axw)
    axp = fig.add_axes([0.08, 0.10, 0.155, 0.60], sharey=axs)
    axc = fig.add_axes([0.895, 0.10, 0.012, 0.60])
    axw.plot(t, R["y"], color="k", lw=0.6)
    axw.set_ylabel("Potential (µV)")
    axw.set_title(title, loc="left")
    plt.setp(axw.get_xticklabels(), visible=False)
    st = max(1, len(t) // 3000)
    pm = axs.pcolormesh(t[::st], R["freqs"], R["disp"][:, ::st], cmap=cmap, vmin=-1, vmax=1,
                        shading="gouraud", rasterized=True)
    axs.set_yscale("log")
    axs.set_ylim(0.3, min(160, fs / 2))
    axs.set_xlim(t[0], t[-1])
    axs.set_xlabel("Time (s)")
    plt.setp(axs.get_yticklabels(), visible=False)
    for tm, val in markers:
        axs.axvline(tm, color="k", lw=1)
        axw.axvline(tm, color="k", lw=1)
    cb = fig.colorbar(pm, cax=axc)
    r = [1, 0.3, 0.1, 0.03]
    p = [np.log10(100 * v) / 2 for v in r]
    cb.set_ticks([-q for q in p] + [0] + p[::-1])
    cb.set_ticklabels([f"-{v:g}" for v in r] + ["0"] + [f"{v:g}" for v in r[::-1]])
    cb.set_label("Re RSST, fraction of max (signed log scale)")
    f, P = R["f"], R["fft_spectrum"]
    mk = (f > 0.3) & (f < min(160, fs / 2)) & (P > 0)
    axp.loglog(P[mk], f[mk], color="#2a78d6", lw=0.9, label="FFT")
    axp.loglog(R["rsst_spectrum"], R["freqs"], color="k", lw=1.3, label="RSST")
    aF, bF = R["fft_fit"]
    aR, bR = R["rsst_fit"]
    xf = f[R["fit_mask_fft"]]
    xr = R["freqs"][R["fit_mask_rsst"]]
    axp.loglog(aF * xf ** bF, xf, color="#eb6834", ls="--", lw=1.5, label=f"FFT α = {-bF:.2f}")
    axp.loglog(aR * xr ** bR, xr, color="#6250d6", ls="--", lw=1.5, label=f"RSST α = {-bR:.2f}")
    axp.set_xlim(0.01, 1.5)
    yt = [v for v in [0.3, 0.625, 1.25, 2.5, 5, 10, 20, 40, 80] if v < fs / 2]
    axp.set_yticks(yt)
    axp.set_yticklabels([f"{v:g}" for v in yt])
    axp.minorticks_off()
    axp.set_ylabel("Frequency (Hz)")
    axp.legend(frameon=False, fontsize=7.5, loc="lower left", bbox_to_anchor=(-0.55, 1.01))
    if save:
        fig.savefig(save, dpi=180)
    return fig, R


if __name__ == "__main__":
    import sys
    rec = load_emotiv(sys.argv[1])
    ch = sys.argv[2] if len(sys.argv) > 2 else "O2"
    print(f"fs = {rec['fs']} Hz, contact = {rec['contact']}, markers = {rec['markers'][:6]}")
    fig, R = rsst_figure(rec["eeg"][ch], rec["fs"], t=rec["t"], markers=rec["markers"], title=ch, save=f"rsst_{ch}.png")
    print(f"alpha FFT {R['alpha_fft']:.2f}, RSST {R['alpha_rsst']:.2f}")

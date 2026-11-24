using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/*
 * AlphaPerturbTask (Group A): alpha entrainment with frequency perturbations.
 *
 * Instead of a sweep, the patch flickers continuously. Each block:
 *   1. Baseline: baseHz (10 Hz) for baselineSeconds, so the alpha rhythm can lock to the stimulus.
 *   2. Perturbations: the frequency steps to baseHz * (1 + offset) for stepSeconds (5 s), then
 *      back to baseHz for stepSeconds, then the next offset, in random order.
 * The 5 s steps leave time for the alpha phase to settle after each change (expected delay ~5 s).
 *
 * The stimulus phase is CONTINUOUS across frequency changes (a phase accumulator, in cycles):
 * each frame the phase advances by f / refreshHz. So any frequency works, not only refreshHz / n,
 * and a frequency change never makes the stimulus jump in phase. Only the slope changes.
 *   Sine:   level = 0.5 * (1 + m sin(2 pi phase))           exact at any frequency (default)
 *   Square: on while frac(phase) < 0.5                       edges rounded to frames, so cycle
 *                                                            lengths alternate at non-integer n
 *
 * The scene needs the same objects as SSVEPTask: "patch" (Quad, HDRP/Unlit) and "fixation".
 *
 * Events logged (Trial column = segment number within the session, 1, 2, 3, ...):
 *   settings        all parameters
 *   no_flicker_start / no_flicker_end   optional fixation-only rest (for the individual alpha freq.)
 *   block_start     value: "block=1;order=+0.20 -0.10 ..."   (Trial = block number)
 *   segment         first rendered frame of each 5 s segment
 *                   value: "f=10.000Hz;offset=+0.000;kind=baseline;block=1;phase=0.0000"
 *                   kind = baseline | perturb | return;  phase = stimulus phase in cycles at that frame
 *   segment_end     value: "long_frames=<k>"   (k = frames longer than 1.5 frame periods)
 *   flick_frame     every frame: "level=0.734;phase=123.4567"
 *   flicker_off     first frame after the block's flicker
 *   block_end, break_start / break_end
 */
public class AlphaPerturbTask : ExperimentBase
{
    public enum Waveform { Sine, Square }

    [Header("Display")]
    [Tooltip("Headset refresh rate in Hz. Check the exact value in Varjo Base.")]
    public float refreshHz = 90f;

    [Header("Frequencies")]
    [Tooltip("Baseline (entrainment) frequency in Hz.")]
    public float baseHz = 10f;
    [Tooltip("Perturbations as fractions of baseHz: 0.2 = +20% (12 Hz), -0.1 = -10% (9 Hz).")]
    public float[] offsets = { -0.2f, -0.1f, 0.1f, 0.2f };
    public Waveform waveform = Waveform.Sine;
    [Range(0f, 1f)] public float sineDepth = 1f;

    [Header("Timing (seconds)")]
    [Tooltip("Length of every segment; also the spacing of the alpha-phase measurements.")]
    public float stepSeconds = 5f;
    [Tooltip("10 Hz flicker at the start of each block, before the first perturbation. Rounded to whole steps.")]
    public float baselineSeconds = 30f;
    [Tooltip("Go back to baseHz for one step after each perturbation.")]
    public bool returnToBase = true;
    [Tooltip("Fixation only, no flicker, before the first block (0 = skip). Gives each person's own alpha frequency.")]
    public float noFlickerSeconds = 0f;

    [Header("Blocks")]
    public int blocks = 3;
    [Tooltip("Each offset appears this many times per block.")]
    public int repsPerBlock = 2;

    [Header("Patch")]
    public Color onColor = Color.white;
    public Color offColor = Color.black;
    public string patchName = "patch";
    public string fixationName = "fixation";

    struct Segment { public float hz, offset; public string kind; }

    Material patchMaterial;
    GameObject patch, fixation;
    int segmentNumber = 0;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    protected override void Start()
    {
        patch = GameObject.Find(patchName);       // find them before hiding: Find skips inactive objects
        fixation = GameObject.Find(fixationName);
        ShowStimulus(false);
        base.Start();
    }

    void ShowStimulus(bool show)
    {
        if (patch != null) patch.SetActive(show);
        if (fixation != null) fixation.SetActive(show);
    }

    int FramesPerStep => Mathf.Max(1, Mathf.RoundToInt(stepSeconds * refreshHz));
    int BaselineSteps => Mathf.Max(1, Mathf.RoundToInt(baselineSeconds / stepSeconds));

    public float SessionSeconds()
    {
        int perBlock = BaselineSteps + offsets.Length * repsPerBlock * (returnToBase ? 2 : 1);
        return noFlickerSeconds + blocks * perBlock * FramesPerStep / refreshHz;
    }

    protected override IEnumerator RunExperiment()
    {
        if (patch == null)
        {
            Debug.LogError("AlphaPerturbTask: no object called '" + patchName + "' in the scene.");
            yield break;
        }
        patchMaterial = patch.GetComponent<Renderer>().material;
        if (fixation == null)
            Debug.LogWarning("AlphaPerturbTask: no fixation object called '" + fixationName + "'.");
        SetLevel(0f);
        ShowStimulus(true);

        string offs = string.Join(" ", System.Array.ConvertAll(offsets, o => o.ToString("+0.000;-0.000", Inv)));
        EventLogger.Log("settings", "refreshHz=" + refreshHz.ToString(Inv) +
                                    ";baseHz=" + baseHz.ToString(Inv) +
                                    ";offsets=" + offs +
                                    ";stepSeconds=" + stepSeconds.ToString(Inv) +
                                    ";framesPerStep=" + FramesPerStep +
                                    ";baselineSteps=" + BaselineSteps +
                                    ";returnToBase=" + returnToBase +
                                    ";blocks=" + blocks + ";repsPerBlock=" + repsPerBlock +
                                    ";waveform=" + waveform + ";sineDepth=" + sineDepth.ToString(Inv) +
                                    ";noFlickerSeconds=" + noFlickerSeconds.ToString(Inv));
        Debug.Log("AlphaPerturbTask: about " + (SessionSeconds() / 60f).ToString("0.0", Inv) +
                  " min of stimulus, plus breaks.");

        if (noFlickerSeconds > 0f)
        {
            yield return new WaitForEndOfFrame();
            EventLogger.Log("no_flicker_start");
            yield return new WaitForSecondsRealtime(noFlickerSeconds);
            EventLogger.Log("no_flicker_end");
        }

        for (int b = 1; b <= blocks; b++)
        {
            if (b > 1)
            {
                EventLogger.Log("break_start", "", b - 1);
                ShowStimulus(false);
                ShowText("Short break.\nPress Space to continue.");
                yield return null;
                while (!Input.GetKeyDown(KeyCode.Space)) yield return null;
                HideText();
                ShowStimulus(true);
                EventLogger.Log("break_end", "", b - 1);
                yield return new WaitForSecondsRealtime(2f);   // settle before the flicker restarts
            }

            List<float> order = new List<float>();
            for (int r = 0; r < repsPerBlock; r++) order.AddRange(offsets);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                float tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }

            List<Segment> segs = new List<Segment>();
            for (int i = 0; i < BaselineSteps; i++)
                segs.Add(new Segment { hz = baseHz, offset = 0f, kind = "baseline" });
            foreach (float o in order)
            {
                segs.Add(new Segment { hz = baseHz * (1f + o), offset = o, kind = "perturb" });
                if (returnToBase) segs.Add(new Segment { hz = baseHz, offset = 0f, kind = "return" });
            }

            EventLogger.Log("block_start", "block=" + b + ";order=" +
                string.Join(" ", order.ConvertAll(o => o.ToString("+0.000;-0.000", Inv))), b);
            yield return StartCoroutine(RunBlock(segs, b));
            EventLogger.Log("block_end", "block=" + b, b);
        }
        yield return new WaitForSecondsRealtime(2f);
        ShowStimulus(false);   // out of the way of the end message
    }

    // Continuous flicker through all segments of one block. The phase starts at 0 for each block.
    IEnumerator RunBlock(List<Segment> segs, int block)
    {
        double phase = 0.0;                   // stimulus phase in cycles
        int framesPerStep = FramesPerStep;
        float longFrame = 1.5f / refreshHz;   // a frame this long means one was dropped

        yield return null;                    // start at the beginning of a frame

        foreach (Segment s in segs)
        {
            segmentNumber++;
            int trial = segmentNumber;
            int longFrames = 0;
            int segStart = Time.frameCount;
            double step = s.hz / refreshHz;   // cycles per frame

            while (true)
            {
                int k = Time.frameCount - segStart;
                if (k >= framesPerStep) break;
                if (k > 0 && Time.unscaledDeltaTime > longFrame) longFrames++;

                double frac = phase - System.Math.Floor(phase);
                float level = waveform == Waveform.Sine
                    ? 0.5f * (1f + sineDepth * (float)System.Math.Sin(2.0 * System.Math.PI * frac))
                    : (frac < 0.5 ? 1f : 0f);
                SetLevel(level);

                yield return new WaitForEndOfFrame();   // the frame is rendered; now log it
                string ph = phase.ToString("0.0000", Inv);
                if (k == 0)
                    EventLogger.Log("segment", "f=" + s.hz.ToString("0.000", Inv) + "Hz" +
                                               ";offset=" + s.offset.ToString("+0.000;-0.000;+0.000", Inv) +
                                               ";kind=" + s.kind + ";block=" + block + ";phase=" + ph, trial);
                EventLogger.Log("flick_frame", "level=" + level.ToString("0.000", Inv) + ";phase=" + ph, trial);

                yield return null;                      // on to the next frame
                phase += step * (Time.frameCount - segStart - k);   // normally one frame
            }

            EventLogger.Log("segment_end", "long_frames=" + longFrames, trial);
            if (longFrames > 0)
                Debug.LogWarning("Segment " + trial + " (" + s.hz.ToString("0.00", Inv) + " Hz): " +
                                 longFrames + " long frame(s)");
        }

        SetLevel(0f);
        yield return new WaitForEndOfFrame();
        EventLogger.Log("flicker_off", "block=" + block, segmentNumber);
    }

    // Drive the patch through emission with Exposure Weight = 0, so its brightness does not depend
    // on the camera exposure (as in SSVEPTask).
    //
    // Linear in light: the project uses linear colour space, where Material.SetColor treats the
    // colour as sRGB and converts it to linear (level 0.5 would become ~0.21). So we interpolate in
    // linear space and write with SetVector, which is not converted: the shader gets exactly
    // L = lerp(off, on, level). For the light at the eye to follow that, the volume profile also
    // needs Tonemapping = None (HDRP's default is ACES, which is nonlinear). See the README.
    void SetLevel(float level)
    {
        Vector4 c = Color.Lerp(offColor.linear, onColor.linear, level);
        if (patchMaterial.HasProperty("_EmissiveColor"))
        {
            if (patchMaterial.HasProperty("_EmissiveExposureWeight")) patchMaterial.SetFloat("_EmissiveExposureWeight", 0f);
            if (patchMaterial.HasProperty("_UnlitColor")) patchMaterial.SetColor("_UnlitColor", Color.black);
            if (patchMaterial.HasProperty("_BaseColor")) patchMaterial.SetColor("_BaseColor", Color.black);
            patchMaterial.SetVector("_EmissiveColor", c);
            return;
        }
        if (patchMaterial.HasProperty("_UnlitColor")) patchMaterial.SetVector("_UnlitColor", c);
        if (patchMaterial.HasProperty("_BaseColor")) patchMaterial.SetVector("_BaseColor", c);
        if (patchMaterial.HasProperty("_Color")) patchMaterial.SetVector("_Color", c);
    }
}

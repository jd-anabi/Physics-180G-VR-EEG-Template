using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/*
 * SSVEPTask (Group A): flickers a patch at several frequencies, one per trial, in random order.
 *
 * The flicker is driven by the frame counter, not a timer. At refresh rate R, a cycle of n frames
 * gives a frequency of R/n Hz. Square wave: on for n/2 frames, off for the rest.
 *
 * The scene needs:
 *   "patch"     a Quad with an HDRP/Unlit material (its color is changed every frame)
 *   "fixation"  a small dot just in front of the patch's center
 *
 * Events logged (Trial column = trial number):
 *   settings     refreshHz, stimSeconds, waveform
 *   order        the frequency order for this session
 *   stim_on      first rendered frame of the flicker      value: "15Hz;n=6"
 *   flick_on / flick_off   every on/off change (square)   value: "15Hz"
 *   flick_frame  every frame (sine)                       value: level 0-1
 *   stim_off     flicker ended                            value: "15Hz;long_frames=0"
 *   break_start / break_end
 */
public class SSVEPTask : ExperimentBase
{
    public enum Waveform { Square, Sine }

    [Header("Display")]
    [Tooltip("Headset refresh rate in Hz. Check the exact value in Varjo Base.")]
    public float refreshHz = 90f;

    [Header("Conditions")]
    [Tooltip("Frames per cycle. Frequency = refreshHz / n. At 90 Hz: 15=6, 12=7.5, 10=9, 9=10, 8=11.25, 6=15, 5=18, 4=22.5, 3=30, 2=45 Hz")]
    public int[] framesPerCycle = { 15, 12, 10, 9, 8, 6, 5, 4, 3, 2 };
    public int repetitions = 4;
    public Waveform waveform = Waveform.Square;
    [Range(0f, 1f)] public float sineDepth = 1f;   // m in L = L0 * (1 + m sin(2 pi f t))

    [Header("Timing (seconds)")]
    public float stimSeconds = 8f;
    public float restMin = 3f;
    public float restMax = 4f;
    public float firstRest = 5f;
    [Tooltip("Pause for Space after this many trials (0 = never).")]
    public int breakEvery = 10;

    [Header("Patch")]
    public Color onColor = Color.white;
    public Color offColor = Color.black;
    public string patchName = "patch";
    public string fixationName = "fixation";

    Material patchMaterial;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    protected override IEnumerator RunExperiment()
    {
        GameObject patch = GameObject.Find(patchName);
        if (patch == null)
        {
            Debug.LogError("SSVEPTask: no object called '" + patchName + "' in the scene.");
            yield break;
        }
        patchMaterial = patch.GetComponent<Renderer>().material;
        if (GameObject.Find(fixationName) == null)
            Debug.LogWarning("SSVEPTask: no fixation object called '" + fixationName + "'.");
        SetLevel(0f);

        // Build and shuffle the trial order (each frequency 'repetitions' times).
        List<int> order = new List<int>();
        for (int r = 0; r < repetitions; r++) order.AddRange(framesPerCycle);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }

        EventLogger.Log("settings", "refreshHz=" + refreshHz.ToString(Inv) +
                                    ";stimSeconds=" + stimSeconds.ToString(Inv) +
                                    ";waveform=" + waveform);
        EventLogger.Log("order", string.Join(" ", order));

        for (int i = 0; i < order.Count; i++)
        {
            int trial = i + 1;
            float rest = (i == 0) ? firstRest : Random.Range(restMin, restMax);
            yield return new WaitForSecondsRealtime(rest);

            yield return StartCoroutine(Flicker(order[i], trial));

            if (breakEvery > 0 && trial % breakEvery == 0 && trial < order.Count)
            {
                EventLogger.Log("break_start", "", trial);
                ShowText("Short break.\nPress Space to continue.");
                yield return null;
                while (!Input.GetKeyDown(KeyCode.Space)) yield return null;
                HideText();
                EventLogger.Log("break_end", "", trial);
            }
        }
        yield return new WaitForSecondsRealtime(restMin);
    }

    IEnumerator Flicker(int n, int trial)
    {
        string cond = (refreshHz / n).ToString("0.##", Inv) + "Hz";
        int totalFrames = Mathf.Max(1, Mathf.RoundToInt(stimSeconds * refreshHz / n)) * n; // whole cycles
        int onFrames = Mathf.Max(1, n / 2);
        float longFrame = 1.5f / refreshHz;   // a frame this long means one was dropped
        int longFrames = 0;
        bool lastOn = false;

        yield return null;                    // start at the beginning of a frame
        int startFrame = Time.frameCount;

        while (true)
        {
            int k = Time.frameCount - startFrame;
            if (k >= totalFrames) break;
            if (k > 0 && Time.unscaledDeltaTime > longFrame) longFrames++;

            float level;
            bool on;
            if (waveform == Waveform.Square)
            {
                on = (k % n) < onFrames;
                level = on ? 1f : 0f;
            }
            else
            {
                level = 0.5f * (1f + sineDepth * Mathf.Sin(2f * Mathf.PI * k / n));
                on = level >= 0.5f;
            }
            SetLevel(level);

            yield return new WaitForEndOfFrame();   // the frame is rendered; now log it
            if (k == 0) EventLogger.Log("stim_on", cond + ";n=" + n, trial);
            if (waveform == Waveform.Square)
            {
                if (k == 0 || on != lastOn) EventLogger.Log(on ? "flick_on" : "flick_off", cond, trial);
            }
            else
            {
                EventLogger.Log("flick_frame", level.ToString("0.000", Inv), trial);
            }
            lastOn = on;
            yield return null;                      // on to the next frame
        }

        SetLevel(0f);
        yield return new WaitForEndOfFrame();
        EventLogger.Log("stim_off", cond + ";long_frames=" + longFrames, trial);
        if (longFrames > 0) Debug.LogWarning("Trial " + trial + " (" + cond + "): " + longFrames + " long frame(s)");
    }

    // level 0 = offColor, 1 = onColor
    void SetLevel(float level)
    {
        Color c = Color.Lerp(offColor, onColor, level);
        if (patchMaterial.HasProperty("_UnlitColor")) patchMaterial.SetColor("_UnlitColor", c); // HDRP Unlit
        if (patchMaterial.HasProperty("_BaseColor")) patchMaterial.SetColor("_BaseColor", c);   // HDRP Lit
        if (patchMaterial.HasProperty("_Color")) patchMaterial.SetColor("_Color", c);
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Varjo.XR;

/*
 * ButtonSearchTask (Group B): find one of n lettered buttons on a virtual panel.
 *
 * Each block uses one number of buttons n (default 2, 4, 8, 16) and one layout:
 *   random  new positions and letters every trial, so the participant has to search (T linear in n)
 *   fixed   one layout for the whole block, shown first in a study period, so the participant can learn
 *           where each letter is and only has to choose (Hick: T linear in log2(n+1))
 * Each trial: fixation cross at the panel's center -> the target letter at the center together with
 *   the n buttons around it -> the participant looks at the target button and presses Space.
 *   The button closest to the gaze direction is the answer. The letter and the buttons appear at the
 *   same moment, so the choice can't be made before RT zero: with the letter shown first, in a fixed
 *   block the participant would already know where to look when the buttons appear.
 * Without the headset (Use Eye Tracking unticked), the mouse pointer stands in for the gaze.
 *
 * The panel and buttons are built by this script when the experiment starts, so the scene only
 * needs the TaskTemplate objects. The panel sits at textPos. Sizes and positions are in degrees
 * of visual angle, converted to meters with the camera-to-panel distance measured at the start.
 * Buttons sit on a grid (gridColumns x gridRows cells), at most one per cell, jittered inside it.
 * The cells around the center (the middle 2 x 2 of a 6 x 4 grid) stay empty, so the target letter
 * never overlaps a button.
 *
 * Brightness: the panel is grey, the buttons slightly lighter and their letters dark, so a button
 * with its letter is about as bright as the panel behind it and the mean luminance hardly changes
 * with n (pupil size follows luminance). Colors are drawn as emission with Exposure Weight 0, so
 * the exposure setting doesn't change them (the same approach as Group A's SSVEPTask).
 *
 * Block order: all levels with one layout, then all levels with the other. The participant number p
 * (the digits of the participant code) sets the order: layouts start at layouts[p mod 2], so odd numbers
 * start with fixed, and the levels follow row (p mod number of levels) of a balanced Latin square,
 * the same in both halves. So P07 gets fixed first and row 3. Block 0 is a short practice block (random).
 *
 * Events logged (Trial column = trial number in the session, practice included):
 *   settings        geometry, timing, colors and input source
 *   order           "row=3;layouts=fixed random;n=16 2 8 4"
 *   block_start     "block=1;n=8;layout=fixed"   (block 0 = practice)
 *   baseline_start / baseline_end   fixation cross only, before the block's trials (pupil baseline)
 *   study_start     fixed blocks only: "block=1;n=8;layout=fixed;buttons=K:-7.6:2.4 B:4.9:-7.3 ..."
 *   study_end       (the buttons are shown without a target letter in between)
 *   fixation_on     "block=1;n=8;layout=fixed;i=3"   (i = trial number in the block)
 *   buttons_on      "block=1;n=8;layout=fixed;i=3;target=K;rep=1;tx=-7.6;ty=2.4;buttons=K:-7.6:2.4 B:4.9:-7.3 ..."
 *                   (the target letter and the buttons appear: RT time zero;
 *                   rep, fixed blocks only = how often K was the target earlier in this block;
 *                   positions in degrees from the panel's center, + = right / up)
 *   response        "chosen=K;correct=1;rt_ms=812;px=-7.1;py=2.0;err_deg=0.8;src=gaze"
 *                   (px, py = where the gaze or mouse hit the panel; err_deg = angle to the chosen button)
 *   press_ignored   Space with no valid gaze, or gaze far from every button; the trial goes on
 *   timeout         no answer within responseWindow
 *   buttons_off
 *   block_end, break_start, break_end
 */
public class ButtonSearchTask : ExperimentBase
{
    public enum LayoutMode { RandomEachTrial, FixedEachBlock }

    [Header("Conditions")]
    [Tooltip("Number of buttons in each block. With an even number of levels the level order is a balanced Latin square.")]
    public int[] levels = { 2, 4, 8, 16 };
    [Tooltip("Each layout is run with every level. RandomEachTrial: new positions and letters every trial (visual search). " +
             "FixedEachBlock: one layout per block, learned in a study period (Hick). The starting layout alternates with the participant number.")]
    public LayoutMode[] layouts = { LayoutMode.RandomEachTrial, LayoutMode.FixedEachBlock };
    public int trialsPerBlock = 16;
    public int practiceTrials = 4;
    public int practiceLevel = 4;

    [Header("Geometry (degrees of visual angle)")]
    public int gridColumns = 6;
    public int gridRows = 4;
    public float cellDeg = 5f;
    public float buttonDeg = 3.5f;
    [Tooltip("Height of the letter's text box. The capital letter itself is about 70% of it.")]
    public float letterDeg = 2.5f;
    public float marginDeg = 2f;

    [Header("Timing (seconds)")]
    public float baselineSeconds = 5f;
    public float fixationMin = 0.8f;
    public float fixationMax = 1.2f;
    [Tooltip("Fixed blocks start by showing the buttons for this long per button (at least studyMinSeconds).")]
    public float studySecondsPerButton = 0.6f;
    public float studyMinSeconds = 3f;
    public float responseWindow = 5f;
    public float interTrial = 0.5f;

    [Header("Response")]
    public KeyCode selectKey = KeyCode.Space;
    [Tooltip("A press counts only if the gaze is within this angle of a button's center.")]
    public float maxPointerErrorDeg = 4f;
    [Tooltip("During a blink, use the last valid gaze sample if it is at most this old (s).")]
    public float gazeMaxAge = 0.2f;

    [Header("Appearance")]
    [Tooltip("An HDRP/Unlit material. Each object gets its own copy.")]
    public Material baseMaterial;
    public Color panelColor = new Color(0.30f, 0.30f, 0.30f);
    public Color buttonColor = new Color(0.36f, 0.36f, 0.36f);
    public Color letterColor = new Color(0.05f, 0.05f, 0.05f);

    // No I, O, Q or W: I and O look like 1 and 0, Q like O, and W is much wider than the rest.
    const string Letters = "ABCDEFGHJKLMNPRSTUVXYZ";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    Transform cam;
    float viewDist;                 // camera to panel, meters
    GameObject panel;
    TextMeshPro centerText;         // fixation cross and target letter
    readonly List<int> cells = new List<int>();   // grid cells buttons may use (all but the center)
    readonly List<GameObject> buttons = new List<GameObject>();
    readonly List<TextMeshPro> labels = new List<TextMeshPro>();
    int nShown;
    Vector2[] shownDeg;             // positions of the buttons in use, degrees
    string[] shownLetters;
    int trialCount = 0;

    Vector3 lastGazeDir;
    float lastGazeTime = -1f;

    protected override IEnumerator RunExperiment()
    {
        if (!BuildPanel()) yield break;

        string order;
        List<Block> blocks = Schedule(out order);
        EventLogger.Log("settings",
            "viewDist=" + viewDist.ToString("0.000", Inv) +
            ";grid=" + gridColumns + "x" + gridRows +
            ";emptyCenterCells=" + (gridColumns * gridRows - cells.Count) +
            ";cellDeg=" + cellDeg.ToString(Inv) + ";buttonDeg=" + buttonDeg.ToString(Inv) +
            ";letterDeg=" + letterDeg.ToString(Inv) +
            ";trialsPerBlock=" + trialsPerBlock + ";layouts=" + LayoutNames(layouts) +
            ";studySecondsPerButton=" + studySecondsPerButton.ToString(Inv) + ";studyMinSeconds=" + studyMinSeconds.ToString(Inv) +
            ";responseWindow=" + responseWindow.ToString(Inv) +
            ";panel=" + panelColor.r.ToString(Inv) + ";button=" + buttonColor.r.ToString(Inv) +
            ";letter=" + letterColor.r.ToString(Inv) +
            ";input=" + (eyeTracking != null ? "gaze" : "mouse"));
        EventLogger.Log("order", order);

        if (practiceTrials > 0)
            yield return StartCoroutine(RunBlock(0, practiceLevel, LayoutMode.RandomEachTrial, practiceTrials));
        for (int b = 0; b < blocks.Count; b++)
        {
            string intro = b > 0 ? "Short break.\n" : practiceTrials > 0 ? "Practice done. The real blocks start now.\n" : "";
            yield return StartCoroutine(Pause(intro + Preview(blocks[b].mode) + "\nPress Space to continue."));
            yield return StartCoroutine(RunBlock(b + 1, blocks[b].n, blocks[b].mode, trialsPerBlock));
        }
        panel.SetActive(false);   // out of the way of the end message
    }

    IEnumerator RunBlock(int block, int n, LayoutMode mode, int nTrials)
    {
        string cond = "block=" + block + ";n=" + n + ";layout=" + LayoutName(mode);
        EventLogger.Log("block_start", cond);

        // Pupil baseline: the panel with only the fixation cross, so the luminance matches the trials.
        ShowButtons(false);
        centerText.text = "+";
        yield return new WaitForEndOfFrame();
        EventLogger.Log("baseline_start", cond);
        yield return new WaitForSecondsRealtime(baselineSeconds);
        EventLogger.Log("baseline_end", cond);

        bool fixedLayout = mode == LayoutMode.FixedEachBlock;
        if (fixedLayout)
        {
            // Study period: the block's buttons, without a cue, so the participant can learn where the letters are.
            NewLayout(n);
            centerText.text = "";
            ShowButtons(true);
            yield return new WaitForEndOfFrame();
            EventLogger.Log("study_start", cond + ";buttons=" + LayoutString());
            yield return new WaitForSecondsRealtime(Mathf.Max(studyMinSeconds, studySecondsPerButton * n));
            ShowButtons(false);
            yield return new WaitForEndOfFrame();
            EventLogger.Log("study_end", cond);
        }

        // In a fixed block the targets are drawn without replacement (refilled when empty), so each button
        // is the target about equally often; rep counts how often this target came before in the block.
        List<int> bag = new List<int>();
        Dictionary<string, int> timesTarget = new Dictionary<string, int>();
        for (int i = 0; i < nTrials; i++)
        {
            trialCount++;
            string target;
            int rep = -1;
            if (fixedLayout)
            {
                if (bag.Count == 0) bag = Shuffled(n);
                target = shownLetters[bag[bag.Count - 1]];
                bag.RemoveAt(bag.Count - 1);
                timesTarget.TryGetValue(target, out rep);
                timesTarget[target] = rep + 1;
            }
            else
            {
                NewLayout(n);
                target = shownLetters[Random.Range(0, nShown)];
            }
            yield return StartCoroutine(RunTrial(trialCount, cond + ";i=" + (i + 1), target, rep));
        }
        centerText.text = "";
        EventLogger.Log("block_end", cond);
    }

    // rep: how often this target came before in the block (fixed layout), or -1 (random layout).
    IEnumerator RunTrial(int trial, string cond, string target, int rep)
    {
        string targetCond = cond + ";target=" + target + (rep >= 0 ? ";rep=" + rep : "");

        centerText.text = "+";
        yield return new WaitForEndOfFrame();
        EventLogger.Log("fixation_on", cond, trial);
        yield return new WaitForSecondsRealtime(Random.Range(fixationMin, fixationMax));

        // The target letter and the buttons appear in the same frame (see the header).
        centerText.text = target;
        ShowButtons(true);
        yield return new WaitForEndOfFrame();
        int ti = System.Array.IndexOf(shownLetters, target);
        long onset = EventLogger.Log("buttons_on", targetCond +
                                                   ";tx=" + F(shownDeg[ti].x) + ";ty=" + F(shownDeg[ti].y) +
                                                   ";buttons=" + LayoutString(), trial);

        // Key presses are checked once per frame (about every 11 ms at 90 Hz).
        float t0 = Time.realtimeSinceStartup;
        bool answered = false;
        while (Time.realtimeSinceStartup - t0 < responseWindow)
        {
            UpdateGaze();
            if (Input.GetKeyDown(selectKey))
            {
                Ray ray;
                string src;
                if (!TryGetPointer(out ray, out src))
                {
                    EventLogger.Log("press_ignored", "reason=no_gaze;src=" + src, trial);
                }
                else
                {
                    float err;
                    int k = Closest(ray, out err);
                    Vector2 p = PointerDeg(ray);
                    string where = "px=" + F(p.x) + ";py=" + F(p.y) + ";err_deg=" + F(err) + ";src=" + src;
                    if (k < 0 || err > maxPointerErrorDeg)
                    {
                        EventLogger.Log("press_ignored", "reason=off_buttons;" + where, trial);
                    }
                    else
                    {
                        bool correct = shownLetters[k] == target;
                        long rt = SessionInfo.NowUnixMs() - onset;
                        EventLogger.Log("response", "chosen=" + shownLetters[k] + ";correct=" + (correct ? 1 : 0) +
                                                    ";rt_ms=" + rt + ";" + where, trial);
                        Debug.Log("Trial " + trial + " (" + cond + "): " + (correct ? "correct" : "wrong") +
                                  ", " + rt + " ms");
                        answered = true;
                        break;
                    }
                }
            }
            yield return null;
        }
        if (!answered) EventLogger.Log("timeout", targetCond, trial);

        ShowButtons(false);
        centerText.text = "";
        yield return new WaitForEndOfFrame();
        EventLogger.Log("buttons_off", cond, trial);
        yield return new WaitForSecondsRealtime(interTrial);
    }

    IEnumerator Pause(string message)
    {
        EventLogger.Log("break_start");
        panel.SetActive(false);
        ShowText(message);
        yield return null;   // skip the frame of the last key press
        while (!Input.GetKeyDown(KeyCode.Space)) yield return null;
        HideText();
        panel.SetActive(true);
        EventLogger.Log("break_end");
    }

    // ------------------------------------------------------------------ panel and layout
    bool BuildPanel()
    {
        Camera c = (eyeTracking != null && eyeTracking.xrCamera != null) ? eyeTracking.xrCamera : Camera.main;
        if (c == null)
        {
            Debug.LogError("ButtonSearchTask: no camera found. The XR camera needs the MainCamera tag.");
            return false;
        }
        if (baseMaterial == null && Shader.Find("HDRP/Unlit") == null)
        {
            Debug.LogError("ButtonSearchTask: set Base Material to an HDRP/Unlit material.");
            return false;
        }
        if (levels.Length == 0 || layouts.Length == 0)
        {
            Debug.LogError("ButtonSearchTask: Levels and Layouts each need at least one entry.");
            return false;
        }
        // Cells whose center is less than one cell from the panel's center (in both directions) stay empty
        // for the target letter: the middle 2 x 2 of the 6 x 4 grid.
        cells.Clear();
        for (int i = 0; i < gridColumns * gridRows; i++)
        {
            Vector2 c0 = CellCenterDeg(i);
            if (Mathf.Abs(c0.x) >= cellDeg || Mathf.Abs(c0.y) >= cellDeg) cells.Add(i);
        }
        int maxN = practiceTrials > 0 ? practiceLevel : 0;
        foreach (int n in levels) maxN = Mathf.Max(maxN, n);
        if (maxN > cells.Count || maxN > Letters.Length)
        {
            Debug.LogError("ButtonSearchTask: " + maxN + " buttons don't fit the " + cells.Count + " cells of the " +
                           gridColumns + "x" + gridRows + " grid outside the center, or the " + Letters.Length + " letters.");
            return false;
        }

        cam = c.transform;
        viewDist = Mathf.Max(0.2f, Mathf.Abs(textPos.position.z - cam.position.z));

        // Facing the participant like the instruction text: the visible side of a Quad faces -z.
        panel = new GameObject("GroupB panel");
        panel.transform.position = textPos.position;
        float w = gridColumns * cellDeg + 2f * marginDeg;
        float h = gridRows * cellDeg + 2f * marginDeg;
        MakeQuad("background", panel.transform, new Vector3(0f, 0f, 0.01f), new Vector2(Size(w), Size(h)), panelColor);
        centerText = MakeLetter("center text", panel.transform);

        for (int i = 0; i < maxN; i++)
        {
            GameObject b = new GameObject("button " + i);
            b.transform.SetParent(panel.transform, false);
            MakeQuad("face", b.transform, new Vector3(0f, 0f, 0.005f), new Vector2(Size(buttonDeg), Size(buttonDeg)), buttonColor);
            labels.Add(MakeLetter("letter", b.transform));
            b.SetActive(false);
            buttons.Add(b);
        }
        return true;
    }

    // Picks n different cells (outside the center) and n different letters. Jitter inside a cell keeps
    // neighboring buttons at least (cellDeg - buttonDeg) / 2 apart.
    void NewLayout(int n)
    {
        List<int> pick = Shuffled(cells.Count);
        List<int> letters = Shuffled(Letters.Length);
        float jitter = 0.25f * (cellDeg - buttonDeg);
        nShown = n;
        shownDeg = new Vector2[n];
        shownLetters = new string[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 c0 = CellCenterDeg(cells[pick[i]]);
            float x = c0.x + Random.Range(-jitter, jitter);
            float y = c0.y + Random.Range(-jitter, jitter);
            shownDeg[i] = new Vector2(x, y);
            shownLetters[i] = Letters[letters[i]].ToString();
            buttons[i].transform.localPosition = new Vector3(Meters(x), Meters(y), 0f);
            labels[i].text = shownLetters[i];
        }
    }

    // Center of grid cell i (row-major, row 0 at the bottom), degrees from the panel's center.
    Vector2 CellCenterDeg(int i)
    {
        return new Vector2((i % gridColumns - (gridColumns - 1) / 2f) * cellDeg,
                           (i / gridColumns - (gridRows - 1) / 2f) * cellDeg);
    }

    void ShowButtons(bool show)
    {
        for (int i = 0; i < buttons.Count; i++) buttons[i].SetActive(show && i < nShown);
    }

    string LayoutString()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < nShown; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(shownLetters[i]).Append(':').Append(F(shownDeg[i].x)).Append(':').Append(F(shownDeg[i].y));
        }
        return sb.ToString();
    }

    static string LayoutName(LayoutMode mode)
    {
        return mode == LayoutMode.RandomEachTrial ? "random" : "fixed";
    }

    static string LayoutNames(LayoutMode[] modes)
    {
        string[] names = new string[modes.Length];
        for (int i = 0; i < modes.Length; i++) names[i] = LayoutName(modes[i]);
        return string.Join(" ", names);
    }

    // What the pause screen says about the next block.
    static string Preview(LayoutMode mode)
    {
        return mode == LayoutMode.RandomEachTrial
            ? "In the next block the buttons move every time."
            : "In the next block the buttons stay in the same places.\nFirst you see them for a few seconds: learn where each letter is.";
    }

    struct Block
    {
        public int n;
        public LayoutMode mode;
    }

    // All levels with one layout, then all levels with the next. With participant number p, the layouts start
    // at layouts[p mod layouts] and the levels follow row (p mod levels) of a balanced Latin square
    // (Williams design: row 0 is 0, 1, k-1, 2, k-2, ...; row r adds r to each entry), the same in each half.
    // Without a number in the participant code, p is random and the row is logged as e.g. "2(random)".
    List<Block> Schedule(out string order)
    {
        int k = levels.Length;
        int m = layouts.Length;
        string digits = "";
        foreach (char ch in SessionInfo.ParticipantID) if (char.IsDigit(ch)) digits += ch;
        int p = 0;
        bool known = digits.Length > 0 && int.TryParse(digits, out p);
        if (!known) p = Random.Range(0, k * m);
        int r = p % k;

        int[] ns = new int[k];
        for (int i = 0; i < k; i++)
        {
            int j = (i % 2 == 1) ? (i + 1) / 2 : (k - i / 2) % k;
            ns[i] = levels[(j + r) % k];
        }
        LayoutMode[] modes = new LayoutMode[m];
        for (int i = 0; i < m; i++) modes[i] = layouts[(i + p) % m];

        List<Block> blocks = new List<Block>();
        foreach (LayoutMode mode in modes)
            foreach (int n in ns) blocks.Add(new Block { n = n, mode = mode });
        order = "row=" + r + (known ? "" : "(random)") + ";layouts=" + LayoutNames(modes) + ";n=" + string.Join(" ", ns);
        return blocks;
    }

    // ------------------------------------------------------------------ gaze and pointer
    void UpdateGaze()
    {
        if (eyeTracking == null) return;
        VarjoEyeTracking.GazeData g = VarjoEyeTracking.GetGaze();
        if (g.status == VarjoEyeTracking.GazeStatus.Invalid) return;
        lastGazeDir = cam.TransformDirection(g.gaze.forward);
        lastGazeTime = Time.realtimeSinceStartup;
    }

    bool TryGetPointer(out Ray ray, out string src)
    {
        if (eyeTracking != null)
        {
            src = "gaze";
            ray = new Ray(cam.position, lastGazeDir);
            return lastGazeTime >= 0f && Time.realtimeSinceStartup - lastGazeTime <= gazeMaxAge;
        }
        src = "mouse";
        ray = cam.GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
        return true;
    }

    int Closest(Ray ray, out float errDeg)
    {
        int best = -1;
        errDeg = 180f;
        for (int i = 0; i < nShown; i++)
        {
            float a = Vector3.Angle(ray.direction, buttons[i].transform.position - ray.origin);
            if (a < errDeg)
            {
                errDeg = a;
                best = i;
            }
        }
        return best;
    }

    // Where the ray hits the panel, in degrees from its center (NaN if it misses the panel's plane).
    Vector2 PointerDeg(Ray ray)
    {
        Plane plane = new Plane(panel.transform.forward, panel.transform.position);
        float dist;
        if (!plane.Raycast(ray, out dist)) return new Vector2(float.NaN, float.NaN);
        Vector3 local = panel.transform.InverseTransformPoint(ray.GetPoint(dist));
        return new Vector2(Mathf.Atan2(local.x, viewDist) * Mathf.Rad2Deg, Mathf.Atan2(local.y, viewDist) * Mathf.Rad2Deg);
    }

    // ------------------------------------------------------------------ helpers
    // Offset on the panel that appears deg degrees from its center.
    float Meters(float deg)
    {
        return viewDist * Mathf.Tan(deg * Mathf.Deg2Rad);
    }

    // Width of something centered on the panel that spans deg degrees.
    float Size(float deg)
    {
        return 2f * Meters(deg / 2f);
    }

    static string F(float v)
    {
        return v.ToString("0.0", Inv);
    }

    static List<int> Shuffled(int count)
    {
        List<int> list = new List<int>();
        for (int i = 0; i < count; i++) list.Add(i);
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
        return list;
    }

    GameObject MakeQuad(string name, Transform parent, Vector3 localPos, Vector2 size, Color color)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = localPos;
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        Renderer r = q.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.material = MakeMaterial(color);
        return q;
    }

    // HDRP multiplies an Unlit material's base color by the camera exposure, so with Fixed Exposure
    // a grey base color renders almost black. Emission with Exposure Weight 0 is not affected.
    Material MakeMaterial(Color color)
    {
        Material m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("HDRP/Unlit"));
        if (m.HasProperty("_EmissiveColor"))
        {
            if (m.HasProperty("_EmissiveExposureWeight")) m.SetFloat("_EmissiveExposureWeight", 0f);
            if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", Color.black);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black);
            m.SetColor("_EmissiveColor", color);
        }
        else if (m.HasProperty("_UnlitColor"))
        {
            m.SetColor("_UnlitColor", color);
        }
        return m;
    }

    // A one-letter text box letterDeg high; auto-sizing fits the letter into it.
    TextMeshPro MakeLetter(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        t.rectTransform.sizeDelta = new Vector2(Size(letterDeg), Size(letterDeg));
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.margin = Vector4.zero;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.001f;
        t.fontSizeMax = 100f;
        t.color = letterColor;
        t.text = "";
        return t;
    }
}

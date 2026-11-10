using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using Varjo.XR;

/*
 * ExperimentBase: the part of every experiment that is the same for all groups.
 *
 * It runs this sequence, then hands over to your code:
 *   1. Waits for eye-tracking calibration (EyeTrackingControl starts it; Backslash restarts it)
 *      and starts the eye-tracking log.
 *   2. Asks for the participant code (press Space to select the box, type, press Enter).
 *   3. Opens the event log (EventLogger) under that code.
 *   4. Shows your instructions and waits for Space, then counts down.
 *   5. Runs YOUR RunExperiment() coroutine.
 *   6. Shows the end message and waits for Space (do the closing sync gesture first),
 *      then closes the logs and stops Play mode.
 *   Escape stops at any time (an "aborted" row is logged).
 *
 * To use it: write a class that inherits from ExperimentBase and fills in RunExperiment().
 * See Examples/ExampleTask.cs and docs/03-build-your-scene.md.
 *
 * The scene must contain these objects (copy Examples/TaskTemplate.unity and they are already there):
 *   instrText (TextMeshPro), textInputCanvas, nameInputField (TMP input field),
 *   textPos, disappearPos, eyeTracking (with EyeTrackingControl), EventSystem.
 * Objects are "hidden" by moving them to disappearPos, behind the scene's background plane.
 */
public abstract class ExperimentBase : MonoBehaviour
{
    [Header("Session")]
    [Tooltip("Untick to test on a computer without the headset: no calibration, no eye log.")]
    public bool useEyeTracking = true;

    [TextArea(3, 8)]
    public string instructions = "Press Space when you are ready.";

    public int countdownSeconds = 3;

    [TextArea(2, 4)]
    public string endMessage = "Thank you! This part is done.\n(Operator: sync gesture, then press Space)";

    protected GameObject instrText;
    protected TextMeshPro instrTMP;
    protected GameObject textInputCanvas;
    protected TMP_InputField nameInputField;
    protected Transform textPos;
    protected Transform disappearPos;
    protected EyeTrackingControl eyeTracking;

    protected virtual void Awake()
    {
        // Awake runs before any Start, so this stops EyeTrackingControl from calling the headset.
        if (!useEyeTracking)
        {
            GameObject et = GameObject.Find("eyeTracking");
            if (et != null) et.SetActive(false);
        }
    }

    protected virtual void Start()
    {
        instrText = GameObject.Find("instrText");
        if (instrText != null) instrTMP = instrText.GetComponent<TextMeshPro>();
        textInputCanvas = GameObject.Find("textInputCanvas");
        GameObject field = GameObject.Find("nameInputField");
        if (field != null) nameInputField = field.GetComponent<TMP_InputField>();
        GameObject tp = GameObject.Find("textPos");
        if (tp != null) textPos = tp.transform;
        GameObject dp = GameObject.Find("disappearPos");
        if (dp != null) disappearPos = dp.transform;
        GameObject et = GameObject.Find("eyeTracking");
        if (et != null) eyeTracking = et.GetComponent<EyeTrackingControl>();

        if (instrTMP == null || textInputCanvas == null || nameInputField == null || textPos == null || disappearPos == null)
        {
            Debug.LogError("ExperimentBase: the scene is missing instrText, textInputCanvas, nameInputField, " +
                           "textPos or disappearPos. Start from a copy of Examples/TaskTemplate.unity.");
            enabled = false;
            return;
        }

        HideInputCanvas();
        StartCoroutine(RunSession());
    }

    // ------------------------------------------------------------------ your part
    // Put your trials or blocks here. Use EventLogger.Log(...) for every event you will analyze.
    protected abstract IEnumerator RunExperiment();

    // ------------------------------------------------------------------ session flow
    IEnumerator RunSession()
    {
        // 1. Eye-tracking calibration and eye log
        if (useEyeTracking && eyeTracking != null)
        {
            int dots = 0;
            while (!VarjoEyeTracking.IsGazeCalibrated())
            {
                ShowText("Eye tracking calibration" + new string('.', dots) + "\n(Backslash restarts calibration)");
                dots = (dots + 1) % 4;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (!eyeTracking.logging) eyeTracking.StartLogging();
        }
        else if (useEyeTracking)
        {
            Debug.LogWarning("ExperimentBase: no 'eyeTracking' object found, so there will be no eye log.");
        }

        // 2. Participant code
        HideText();
        ShowInputCanvas();
        string id = "";
        while (id.Length == 0)
        {
            if (Input.GetKeyDown(KeyCode.Space) && EventSystem.current != null)
            {
                // Selecting the field from code works around a Unity focus bug in VR scenes.
                EventSystem.current.SetSelectedGameObject(nameInputField.gameObject, null);
                nameInputField.ActivateInputField();
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                id = nameInputField.text.Trim();
            }
            yield return null;
        }
        HideInputCanvas();

        // 3. Event log
        SessionInfo.Begin(id, SceneManager.GetActiveScene().name);
        EventLogger.Begin();
        EventLogger.Log("session_start", "eyeTracking=" + (useEyeTracking && eyeTracking != null));

        // 4. Instructions and countdown
        ShowText(instructions);
        yield return null; // skip the frame of the Enter press
        while (!Input.GetKeyDown(KeyCode.Space)) yield return null;
        for (int s = countdownSeconds; s > 0; s--)
        {
            ShowText("Starting in " + s);
            yield return new WaitForSecondsRealtime(1f);
        }
        HideText();

        // 5. The group's experiment
        EventLogger.Log("experiment_start");
        yield return StartCoroutine(RunExperiment());
        EventLogger.Log("experiment_end");

        // 6. End: wait for the operator, so the closing sync gesture is still in the eye log
        ShowText(endMessage);
        yield return null;
        while (!Input.GetKeyDown(KeyCode.Space)) yield return null;
        EventLogger.Log("session_end");
        EventLogger.End();
        StopPlaying();
    }

    protected virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (EventLogger.IsOpen) EventLogger.Log("aborted");
            StopPlaying();
        }
    }

    // ------------------------------------------------------------------ helpers
    protected void ShowText(string s)
    {
        instrTMP.text = s;
        instrText.transform.position = textPos.position;
    }

    protected void HideText()
    {
        instrTMP.text = "";
        instrText.transform.position = disappearPos.position;
    }

    void ShowInputCanvas()
    {
        textInputCanvas.transform.position = textPos.position;
    }

    void HideInputCanvas()
    {
        textInputCanvas.transform.position = disappearPos.position;
    }

    // Moves an object out of view (to disappearPos, behind the background plane).
    protected void Hide(GameObject obj)
    {
        obj.transform.position = disappearPos.position;
    }

    protected static void StopPlaying()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

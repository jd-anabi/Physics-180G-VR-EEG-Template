using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Varjo.XR;

[RequireComponent(typeof(AudioSource))]
public class GroupCMentalArithmetic : ExperimentBase
{
    [Header("Block schedule")]
    [Min(1)]
    public int taskRestPairs = 1;

    [Min(0.1f)]
    public float minBlockDuration = 20f;

    [Min(0.1f)]
    public float maxBlockDuration = 30f;

    [Min(0f)]
    public float speechMarkerTime = 12f;

    public KeyCode speechMarkerKey = KeyCode.Return;

    [Min(0)]
    public int arithmeticStep = 7;

    [Header("Audio")]
    public AudioClip startingNumberClip;

    GameObject fixationCanvasObject;
    AudioSource audioSource;
    string submittedParticipantId = "";

    protected override void Awake()
    {
        base.Awake();

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("GroupCMentalArithmetic: the scene needs a MainCamera.");
            return;
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.white;

        HideTemplateStimuli();

        TMP_Text[] textObjects = FindObjectsOfType<TMP_Text>(true);
        foreach (TMP_Text textObject in textObjects)
        {
            textObject.color = Color.black;
            if (textObject.gameObject.name == "instrText")
            {
                textObject.text = "";
            }
        }

        fixationCanvasObject = CreateFixationCross(mainCamera);
        fixationCanvasObject.SetActive(false);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("GroupCMentalArithmetic: Stim Control needs an AudioSource component.");
            enabled = false;
            return;
        }

        audioSource.clip = startingNumberClip;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    protected override void Start()
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

        if (instrTMP == null || textInputCanvas == null || nameInputField == null ||
            textPos == null || disappearPos == null)
        {
            Debug.LogError("GroupCMentalArithmetic: the scene is missing a required session UI object. " +
                           "Start from a copy of Examples/TaskTemplate.unity.");
            enabled = false;
            return;
        }

        textInputCanvas.SetActive(false);
        StartCoroutine(RunGroupCSession());
    }

    IEnumerator RunGroupCSession()
    {
        if (useEyeTracking && eyeTracking != null)
        {
            Debug.Log("Group C: complete eye-tracking calibration. The fixation X appears after ID entry.");
            while (!VarjoEyeTracking.IsGazeCalibrated())
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            if (!eyeTracking.logging) eyeTracking.StartLogging();
        }
        else if (useEyeTracking)
        {
            Debug.LogWarning("GroupCMentalArithmetic: no 'eyeTracking' object found, so there will be no eye log.");
        }

        submittedParticipantId = "";
        nameInputField.onSubmit.AddListener(CaptureParticipantId);
        textInputCanvas.SetActive(true);

        while (submittedParticipantId.Length == 0)
        {
            if (Input.GetKeyDown(KeyCode.Space) && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(nameInputField.gameObject, null);
                nameInputField.ActivateInputField();
            }

            yield return null;
        }

        nameInputField.onSubmit.RemoveListener(CaptureParticipantId);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        textInputCanvas.SetActive(false);

        SessionInfo.Begin(submittedParticipantId, SceneManager.GetActiveScene().name);
        EventLogger.Begin();
        EventLogger.Log("session_start", "eyeTracking=" + (useEyeTracking && eyeTracking != null));

        ShowText(instructions);
        yield return null;
        while (!Input.GetKeyDown(KeyCode.Space)) yield return null;
        for (int seconds = countdownSeconds; seconds > 0; seconds--)
        {
            ShowText(seconds.ToString());
            yield return new WaitForSecondsRealtime(1f);
        }

        HideText();
        EventLogger.Log("experiment_start");
        yield return StartCoroutine(RunExperiment());
        EventLogger.Log("experiment_end");

        Debug.Log("Group C session complete. Press Space to close the session.");
        yield return null;
        while (!Input.GetKeyDown(KeyCode.Space)) yield return null;
        EventLogger.Log("session_end");
        EventLogger.End();
        StopPlaying();
    }

    void CaptureParticipantId(string value)
    {
        string participantId = value.Trim();
        if (participantId.Length > 0) submittedParticipantId = participantId;
    }

    protected override IEnumerator RunExperiment()
    {
        if (startingNumberClip == null)
        {
            Debug.LogError("GroupCMentalArithmetic: assign a starting-number audio clip before running the experiment.");
            yield break;
        }

        if (taskRestPairs < 1 || minBlockDuration <= 0f ||
            maxBlockDuration < minBlockDuration ||
            speechMarkerTime < 0f || speechMarkerTime >= minBlockDuration)
        {
            Debug.LogError("GroupCMentalArithmetic: check the block count and duration settings. " +
                           "The speech marker time must be shorter than the minimum block duration.");
            yield break;
        }

        if (fixationCanvasObject == null)
        {
            Debug.LogError("GroupCMentalArithmetic: the fixation target could not be created.");
            yield break;
        }

        fixationCanvasObject.SetActive(true);

        for (int pair = 1; pair <= taskRestPairs; pair++)
        {
            float scheduledTaskDuration = Random.Range(minBlockDuration, maxBlockDuration);
            float taskDuration = Mathf.Max(scheduledTaskDuration, startingNumberClip.length);
            string taskValue = "task step=" + arithmeticStep +
                               " duration_s=" + taskDuration.ToString("F2", CultureInfo.InvariantCulture) +
                               " audio_s=" + startingNumberClip.length.ToString("F2", CultureInfo.InvariantCulture);

            EventLogger.Log("block_start", taskValue, pair);
            audioSource.time = 0f;
            audioSource.Play();
            yield return WaitForBlock(taskDuration);
            audioSource.Stop();
            Debug.Log("Group C pair " + pair + ": starting-number audio finished; task block ended.");
            EventLogger.Log("block_end", taskValue, pair);

            float restDuration = Random.Range(minBlockDuration, maxBlockDuration);
            string restValue = "rest duration_s=" +
                               restDuration.ToString("F2", CultureInfo.InvariantCulture);
            EventLogger.Log("block_start", restValue, pair);

            float restStart = Time.realtimeSinceStartup;
            bool markerLogged = false;
            bool markerTargetLogged = false;
            while (Time.realtimeSinceStartup - restStart < restDuration)
            {
                float elapsed = Time.realtimeSinceStartup - restStart;
                if (!markerTargetLogged && elapsed >= speechMarkerTime)
                {
                    EventLogger.Log(
                        "speech_marker_target",
                        "rest target_elapsed_s=" + speechMarkerTime.ToString("F2", CultureInfo.InvariantCulture),
                        pair);
                    Debug.Log("Group C pair " + pair +
                              ": expected final-number speech timing; press " + speechMarkerKey +
                              " when the participant speaks.");
                    markerTargetLogged = true;
                }

                if (!markerLogged && Input.GetKeyDown(speechMarkerKey))
                {
                    string markerValue = "rest elapsed_s=" +
                                         elapsed.ToString("F2", CultureInfo.InvariantCulture);
                    EventLogger.Log("speech_marker", markerValue, pair);
                    markerLogged = true;
                }

                yield return null;
            }

            if (!markerLogged)
            {
                Debug.LogWarning("GroupCMentalArithmetic: no speech marker keypress was recorded in rest pair " +
                                 pair + ".");
                EventLogger.Log("speech_marker_missing", "rest", pair);
            }

            EventLogger.Log("block_end", restValue, pair);
        }
    }

    static GameObject CreateFixationCross(Camera mainCamera)
    {
        GameObject canvasObject = new GameObject(
            "GroupC Fixation Canvas",
            typeof(RectTransform),
            typeof(Canvas));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(mainCamera.transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 0f, 2f);
        canvasObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.001f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = mainCamera;
        canvas.sortingOrder = 100;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1000f, 1000f);

        CreateCrossBar(canvasRect, "Fixation X /", 40f, 6f, -45f);
        CreateCrossBar(canvasRect, "Fixation X \\", 40f, 6f, 45f);
        return canvasObject;
    }

    static void CreateCrossBar(
        RectTransform parent,
        string objectName,
        float width,
        float height,
        float rotation)
    {
        GameObject barObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        barObject.layer = parent.gameObject.layer;
        barObject.transform.SetParent(parent, false);

        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(width, height);
        barRect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Image barImage = barObject.GetComponent<Image>();
        barImage.color = Color.black;
        barImage.raycastTarget = false;
    }

    IEnumerator WaitForBlock(float duration)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < duration)
        {
            yield return null;
        }
    }

    static void HideTemplateStimuli()
    {
        string[] names =
        {
            "-25 deg mark",
            "25 deg mark",
            "Left Edge",
            "Right Edge",
            "redSphere",
            "blackSphere",
            "blueSphere",
            "cue",
            "reference 8° size",
            "reference 4° size",
            "plane",
            "Placeholder"
        };

        foreach (string objectName in names)
        {
            GameObject sceneObject = GameObject.Find(objectName);
            if (sceneObject != null) sceneObject.SetActive(false);
        }
    }
}

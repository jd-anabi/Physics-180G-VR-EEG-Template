using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.HighDefinition;

[System.Serializable]
public class GroupCAudioCue
{
    [Tooltip("Cue onset in seconds from the beginning of the audio track.")]
    [Min(0f)]
    public float timeSeconds;

    [Tooltip("Short analysis label, such as starting_number, level_1, stop, or report_prompt.")]
    public string meaning;
}

[RequireComponent(typeof(AudioSource))]
public class GroupCMentalArithmetic : ExperimentBase
{
    [Header("Continuous audio cues")]
    public GroupCAudioCue[] cueTimeline = new GroupCAudioCue[0];

    [Header("Operator response marker")]
    public KeyCode speechMarkerKey = KeyCode.Return;

    [Header("Audio")]
    [Tooltip("The continuous recording that contains the full sequence of prompts.")]
    public AudioClip audioTrack;

    GameObject fixationCanvasObject;
    AudioSource audioSource;

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
        Color backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        mainCamera.backgroundColor = backgroundColor;
        HDAdditionalCameraData hdCamera = mainCamera.GetComponent<HDAdditionalCameraData>();
        if (hdCamera != null)
        {
            hdCamera.backgroundColorHDR = backgroundColor;
        }

        AttachSessionUiToCamera(mainCamera);
        HideTemplateStimuli();

        TMP_Text[] textObjects = FindObjectsOfType<TMP_Text>(true);
        foreach (TMP_Text textObject in textObjects)
        {
            textObject.color = Color.white;
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

        audioSource.clip = audioTrack;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;
    }

    static void AttachSessionUiToCamera(Camera mainCamera)
    {
        Transform textPosition = GameObject.Find("textPos")?.transform;
        if (textPosition != null)
        {
            textPosition.SetParent(mainCamera.transform, false);
            textPosition.localPosition = new Vector3(0f, 0f, 2f);
        }

        Transform disappearPosition = GameObject.Find("disappearPos")?.transform;
        if (disappearPosition != null)
        {
            disappearPosition.SetParent(mainCamera.transform, false);
            disappearPosition.localPosition = new Vector3(0f, 0f, -2f);
        }

        Transform inputCanvas = GameObject.Find("textInputCanvas")?.transform;
        if (inputCanvas != null)
        {
            inputCanvas.SetParent(mainCamera.transform, true);
        }

        TMP_Text[] textObjects = FindObjectsOfType<TMP_Text>(true);
        foreach (TMP_Text textObject in textObjects)
        {
            if (textObject.gameObject.name == "instrText")
            {
                textObject.transform.SetParent(mainCamera.transform, true);
            }
        }
    }

    protected override IEnumerator RunExperiment()
    {
        if (fixationCanvasObject == null)
        {
            Debug.LogError("GroupCMentalArithmetic: the fixation target could not be created.");
            yield break;
        }

        fixationCanvasObject.SetActive(true);

        if (audioTrack == null)
        {
            Debug.LogError("GroupCMentalArithmetic: assign the continuous audio track before running the session.");
            fixationCanvasObject.SetActive(false);
            yield break;
        }

        if (audioTrack.loadState == AudioDataLoadState.Unloaded &&
            !audioTrack.LoadAudioData())
        {
            Debug.LogError("GroupCMentalArithmetic: Unity could not load the continuous audio track.");
            fixationCanvasObject.SetActive(false);
            yield break;
        }

        while (audioTrack.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
        }

        if (audioTrack.loadState != AudioDataLoadState.Loaded ||
            audioTrack.length <= 0f || audioTrack.frequency <= 0)
        {
            Debug.LogError("GroupCMentalArithmetic: the continuous audio track failed to load correctly.");
            fixationCanvasObject.SetActive(false);
            yield break;
        }

        if (!ValidateCueTimeline())
        {
            fixationCanvasObject.SetActive(false);
            yield break;
        }

        audioSource.clip = audioTrack;
        audioSource.timeSamples = 0;
        audioSource.loop = false;
        double scheduledStart = AudioSettings.dspTime + 0.5d;
        audioSource.PlayScheduled(scheduledStart);

        string trackValue = "duration_s=" +
                            audioTrack.length.ToString("F3", CultureInfo.InvariantCulture);
        EventLogger.Log("audio_track_scheduled", trackValue);

        while (AudioSettings.dspTime < scheduledStart)
        {
            yield return null;
        }

        EventLogger.Log("audio_track_start", trackValue);
        if (!audioSource.isPlaying)
        {
            Debug.LogError("GroupCMentalArithmetic: audio playback did not start as scheduled.");
            fixationCanvasObject.SetActive(false);
            yield break;
        }

        int nextCueIndex = 0;
        while (audioSource.isPlaying)
        {
            float audioTime = audioSource.timeSamples / (float)audioTrack.frequency;
            while (nextCueIndex < cueTimeline.Length &&
                   cueTimeline[nextCueIndex].timeSeconds <= audioTime)
            {
                GroupCAudioCue cue = cueTimeline[nextCueIndex];
                string cueValue = "audio_s=" +
                                  cue.timeSeconds.ToString("F3", CultureInfo.InvariantCulture) +
                                  " meaning=" + cue.meaning;
                EventLogger.Log("audio_cue", cueValue, nextCueIndex + 1);
                nextCueIndex++;
            }

            if (Input.GetKeyDown(speechMarkerKey))
            {
                string markerValue = "audio_s=" +
                                     audioTime.ToString("F3", CultureInfo.InvariantCulture);
                int currentCue = nextCueIndex;
                if (currentCue > 0)
                {
                    markerValue += " after_cue=" + cueTimeline[currentCue - 1].meaning;
                }

                EventLogger.Log("speech_marker", markerValue, currentCue);
            }

            yield return null;
        }

        while (nextCueIndex < cueTimeline.Length)
        {
            GroupCAudioCue cue = cueTimeline[nextCueIndex];
            string cueValue = "audio_s=" +
                              cue.timeSeconds.ToString("F3", CultureInfo.InvariantCulture) +
                              " meaning=" + cue.meaning;
            EventLogger.Log("audio_cue", cueValue, nextCueIndex + 1);
            nextCueIndex++;
        }

        audioSource.Stop();
        EventLogger.Log(
            "audio_track_end",
            "duration_s=" + audioTrack.length.ToString("F3", CultureInfo.InvariantCulture));
        fixationCanvasObject.SetActive(false);
    }

    bool ValidateCueTimeline()
    {
        if (cueTimeline == null)
        {
            Debug.LogError("GroupCMentalArithmetic: Cue Timeline cannot be null.");
            return false;
        }

        if (cueTimeline.Length == 0)
        {
            Debug.LogWarning("GroupCMentalArithmetic: Cue Timeline is empty. The run will log the audio track " +
                             "and operator markers, but no cue events.");
            return true;
        }

        float previousTime = -1f;
        for (int i = 0; i < cueTimeline.Length; i++)
        {
            GroupCAudioCue cue = cueTimeline[i];
            if (cue == null || string.IsNullOrWhiteSpace(cue.meaning) ||
                float.IsNaN(cue.timeSeconds) || float.IsInfinity(cue.timeSeconds) ||
                cue.timeSeconds < 0f || cue.timeSeconds >= audioTrack.length ||
                cue.timeSeconds < previousTime)
            {
                Debug.LogError("GroupCMentalArithmetic: cue " + (i + 1) +
                               " is invalid. Add a meaning and a time in seconds within the clip, " +
                               "in chronological order.");
                return false;
            }

            previousTime = cue.timeSeconds;
        }

        return true;
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

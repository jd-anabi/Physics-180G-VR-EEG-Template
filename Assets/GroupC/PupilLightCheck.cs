using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

/*
 * PupilLightCheck: does the eye tracker see the pupil change size?
 *
 * The whole view turns white for lightSeconds, then black for darkSeconds, and repeats
 * `cycles` times. A small red dot stays in the center the whole time; the participant
 * looks at it (the measured pupil size depends a little on where the eye points).
 * The pupil should be small in the light (~2-4 mm) and large in the dark (~5-8 mm).
 *
 * Events logged (Unix ms, same clock as the eye log):
 *   block_start / block_end   value "light" or "dark", trial = cycle number
 * Check the result with:  python analysis/pupil_check.py "<folder with the files>"
 *
 * The white/black panel is a canvas fixed in front of the headset camera, so it covers
 * the whole view whatever the scene behind it looks like. A global Volume with Fixed
 * exposure is added while the check runs, so HDRP's automatic exposure cannot dim the
 * white or brighten the black.
 *
 * Built by Group C/Make Pupil Check Scene (Editor/MakePupilCheckScene.cs).
 */
public class PupilLightCheck : ExperimentBase
{
    [Header("Pupil light check")]
    [Min(1)] public int cycles = 6;
    [Min(1f)] public float lightSeconds = 10f;
    [Min(1f)] public float darkSeconds = 10f;
    [Tooltip("Seconds of dark before the first light phase, so the first light step starts from a dilated pupil.")]
    [Min(0f)] public float startDarkSeconds = 10f;
    public Color lightColor = Color.white;
    public Color darkColor = Color.black;
    public Color fixationColor = new Color(0.8f, 0.1f, 0.1f);
    [Tooltip("Fixed exposure (EV100) used while the check runs. Only matters if the white looks grey or the black looks grey in the headset.")]
    public float fixedExposureEV = 0f;

    GameObject panelCanvas;
    Image panel;
    GameObject exposureVolume;

    protected override IEnumerator RunExperiment()
    {
        BuildPanel();
        AddFixedExposure();
        EventLogger.Log("pupil_check_settings",
            "cycles=" + cycles + " light_s=" + lightSeconds + " dark_s=" + darkSeconds);

        if (startDarkSeconds > 0f)
        {
            yield return Phase("dark", darkColor, startDarkSeconds, 0);
        }
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            yield return Phase("light", lightColor, lightSeconds, cycle);
            yield return Phase("dark", darkColor, darkSeconds, cycle);
        }

        // Remove the panel so the end message (behind it) is visible again.
        Destroy(panelCanvas);
        Destroy(exposureVolume);
    }

    IEnumerator Phase(string name, Color color, float seconds, int cycle)
    {
        panel.color = color;
        yield return new WaitForEndOfFrame();          // log after the frame with the new color
        EventLogger.Log("block_start", name, cycle);
        yield return new WaitForSecondsRealtime(seconds);
        EventLogger.Log("block_end", name, cycle);
    }

    void BuildPanel()
    {
        Camera cam = Camera.main;
        panelCanvas = new GameObject("PupilCheck Canvas", typeof(RectTransform), typeof(Canvas));
        panelCanvas.layer = 5; // UI
        panelCanvas.transform.SetParent(cam.transform, false);
        panelCanvas.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        panelCanvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        panelCanvas.transform.localScale = Vector3.one * 0.001f;  // 1 canvas unit = 1 mm

        Canvas canvas = panelCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvas.sortingOrder = 200;
        panelCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(4000f, 4000f);

        // 4 m x 4 m at 0.5 m covers about +-76 degrees, wider than the headset's view.
        panel = MakeImage(panelCanvas.transform, "Light/Dark Panel", 4000f, darkColor);
        // 8 mm at 0.5 m is about 0.9 degrees.
        MakeImage(panelCanvas.transform, "Fixation Dot", 8f, fixationColor);
    }

    static Image MakeImage(Transform parent, string name, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    void AddFixedExposure()
    {
        exposureVolume = new GameObject("PupilCheck Fixed Exposure");
        Volume vol = exposureVolume.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1000f;  // wins over the scene's Sky and Fog Volume
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Exposure exposure = profile.Add<Exposure>(true);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(fixedExposureEV);
        vol.profile = profile;
    }
}

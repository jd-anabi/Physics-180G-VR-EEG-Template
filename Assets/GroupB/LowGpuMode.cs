using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

/*
 * LowGpuMode (Group B): turns the sky and the fog off, for trying the scene on a laptop.
 *
 * The template's physically based sky needs a long GPU computation that integrated graphics
 * (e.g. Intel UHD 620 on Linux) can't finish in time: the driver resets the GPU and Unity hangs as soon
 * as it draws the scene, even before Play. When Low-GPU Mode is on, this script (in Edit mode and Play mode)
 *   - adds a hidden global volume with no sky and no fog, which wins over the Sky and Fog Volume
 *     (the fixed exposure from GroupB_SkyandFog stays), and
 *   - switches off the scene's StaticLightingSky (the sky used for ambient light), switching it back on
 *     while the scene is being saved, so the scene file always keeps it.
 * The task's colors are emission that ignores the lighting, so the panel and buttons look the same;
 * only the surroundings go black.
 *
 * The setting belongs to the computer, not the project: it is stored in EditorPrefs and switched with
 * Tools > Group B > Low-GPU Mode, so it can't be committed. Until it has been set, it is on for Intel
 * graphics (so the first launch on such a laptop doesn't hang before the menu can be reached) and off
 * otherwise. It is always off in a build.
 */
[ExecuteAlways]
public class LowGpuMode : MonoBehaviour
{
    const string MenuPath = "Tools/Group B/Low-GPU Mode (this computer)";
    const string PrefKey = "GroupB.LowGpuMode";
    const int IntelVendorId = 0x8086;

    static bool On
    {
        get
        {
#if UNITY_EDITOR
            return EditorPrefs.GetBool(PrefKey, SystemInfo.graphicsDeviceVendorID == IntelVendorId);
#else
            return false;
#endif
        }
    }

    bool applied;
    GameObject overrideObject;
    VolumeProfile overrideProfile;
    readonly List<StaticLightingSky> switchedOff = new List<StaticLightingSky>();

    void OnEnable()
    {
        if (On) Apply();
    }

    void OnDisable()
    {
        if (applied) Restore();
    }

    void Apply()
    {
        applied = true;
        Debug.Log("LowGpuMode: sky and fog are off on this computer (Tools > Group B > Low-GPU Mode).");

        overrideProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        overrideProfile.hideFlags = HideFlags.HideAndDontSave;
        overrideProfile.Add<VisualEnvironment>().skyType.Override(0);   // 0 = no sky
        Fog fog = overrideProfile.Add<Fog>();
        fog.enabled.Override(false);
        fog.enableVolumetricFog.Override(false);

        overrideObject = new GameObject("GroupB low-GPU volume");
        overrideObject.hideFlags = HideFlags.HideAndDontSave;
        Volume volume = overrideObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000f;
        volume.sharedProfile = overrideProfile;

        // All objects of the scene exist by now, so this also catches a StaticLightingSky whose OnEnable hasn't run yet.
        foreach (StaticLightingSky s in FindObjectsOfType<StaticLightingSky>())
        {
            if (!s.enabled) continue;
            s.enabled = false;
            switchedOff.Add(s);
        }
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
#endif
    }

    void Restore()
    {
        applied = false;
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
#endif
        SetStaticSkies(true);
        switchedOff.Clear();
        if (overrideObject != null) DestroyImmediate(overrideObject);
        if (overrideProfile != null)
        {
            foreach (VolumeComponent c in overrideProfile.components) DestroyImmediate(c);
            DestroyImmediate(overrideProfile);
        }
    }

    void SetStaticSkies(bool on)
    {
        foreach (StaticLightingSky s in switchedOff)
            if (s != null) s.enabled = on;
    }

#if UNITY_EDITOR
    void OnSceneSaving(Scene scene, string path)
    {
        SetStaticSkies(true);
    }

    void OnSceneSaved(Scene scene)
    {
        SetStaticSkies(false);
    }

    // Switching it off on a laptop that needs it makes Unity hang, like opening the scene without it.
    [MenuItem(MenuPath)]
    static void Toggle()
    {
        EditorPrefs.SetBool(PrefKey, !On);
        foreach (LowGpuMode m in FindObjectsOfType<LowGpuMode>())
        {
            if (m.applied) m.Restore();
            if (On && m.isActiveAndEnabled) m.Apply();
        }
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, On);
        return true;
    }
#endif
}

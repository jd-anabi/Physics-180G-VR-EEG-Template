using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/*
 * Builds Assets/GroupC/GroupC_PupilCheck.unity from the course's TaskTemplate scene:
 * copies the template, swaps ExampleTask for PupilLightCheck on "Stim Control", saves.
 *
 * In the Unity editor: menu  Group C > Make Pupil Check Scene
 * From a terminal (Unity closed), Windows PowerShell:
 *   & "C:\Program Files\Unity\Hub\Editor\2022.3.12f1\Editor\Unity.exe" -batchmode -quit `
 *     -projectPath "C:\path\to\Physics-180G-VR-EEG-Template" `
 *     -executeMethod MakePupilCheckScene.Make -logFile -
 */
public static class MakePupilCheckScene
{
    const string TemplatePath = "Assets/Examples/TaskTemplate.unity";
    const string ScenePath = "Assets/GroupC/GroupC_PupilCheck.unity";

    [MenuItem("Group C/Make Pupil Check Scene")]
    public static void Make()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Debug.Log("MakePupilCheckScene: " + ScenePath + " already exists; opening it. " +
                      "Delete it first if you want a fresh copy.");
            if (!Application.isBatchMode) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return;
        }
        if (!AssetDatabase.CopyAsset(TemplatePath, ScenePath))
        {
            Debug.LogError("MakePupilCheckScene: could not copy " + TemplatePath);
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject stim = GameObject.Find("Stim Control");
        if (stim == null)
        {
            Debug.LogError("MakePupilCheckScene: no 'Stim Control' object in the template.");
            return;
        }
        foreach (ExperimentBase old in stim.GetComponents<ExperimentBase>())
        {
            Object.DestroyImmediate(old);
        }
        PupilLightCheck check = stim.AddComponent<PupilLightCheck>();
        check.useEyeTracking = true;
        check.instructions =
            "Pupil check.\nThe screen will turn white and black every 10 seconds.\n" +
            "Keep looking at the red dot in the middle.\n\nPress Space to start.";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("MakePupilCheckScene: saved " + ScenePath);
    }
}

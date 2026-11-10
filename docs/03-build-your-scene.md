# 3. Build your scene

You start from a working template and change only what your experiment needs.

## Make your copy

1. In Unity's Project window, select `Assets/Examples/TaskTemplate.unity` and press **Ctrl+D** (duplicate). Drag the copy into your group's folder and rename it, for example `GroupA_SSVEP`. The scene name ends up in every file name, so keep it short and without spaces.
2. Do the same with `Assets/Examples/ExampleTask.cs`: duplicate it, move the copy to your folder, rename it (e.g. `SSVEPTask.cs`). Open it and change `public class ExampleTask` to the same name as the file (`public class SSVEPTask`). Unity requires the class name and file name to match.
3. Open your scene. Select the **Stim Control** object, remove the `Example Task` component (⋮ menu → Remove Component), then Add Component → your script.
4. Fill in **Instructions** in the Inspector. Press Play with **Use Eye Tracking** unticked to check that it runs.

Do not edit `TaskTemplate.unity` or anything in `Assets/Scripts/Shared` directly; the other groups depend on them.

## Write your experiment

Everything your experiment does goes in one method:

```csharp
protected override IEnumerator RunExperiment()
{
    for (int trial = 1; trial <= nTrials; trial++)
    {
        // change something on screen
        patch.SetActive(true);
        yield return new WaitForEndOfFrame();          // wait until that frame is rendered
        EventLogger.Log("stim_on", "15Hz", trial);     // then log it

        yield return new WaitForSecondsRealtime(10f);  // let it run

        patch.SetActive(false);
        yield return new WaitForEndOfFrame();
        EventLogger.Log("stim_off", "15Hz", trial);
    }
}
```

`RunExperiment` is a *coroutine*: `yield return` pauses it without freezing Unity. The three you will use:

| Line | Waits for |
|---|---|
| `yield return null;` | the next frame |
| `yield return new WaitForSecondsRealtime(t);` | at least `t` seconds (rounded up to the next frame) |
| `yield return new WaitForEndOfFrame();` | the end of the current frame, after it has been rendered |

To find objects in your scene, use `GameObject.Find("name")` (the name must match exactly). `ExperimentBase` gives you `ShowText("...")`, `HideText()`, and `Hide(obj)`, which moves an object behind the background plane; `obj.SetActive(false)` works too.

If your class needs its own `Start()` or `Update()`, write `protected override void Start()` and call `base.Start();` inside it. Otherwise the session flow won't run.

## Logging

`EventLogger.Log(eventName, value, trial)` writes one row: Unix time in ms, Unity frame number, trial, event name, value. It returns the time it wrote.

- **Log every event you will analyze**: onsets and offsets, condition changes, responses, block starts. If it is not in the log, you cannot epoch on it later.
- Put the condition in `value` (`"15Hz"`, `"n=8"`, `"step=7"`), so each row is self-explanatory.
- Use the same event names throughout and write them down in your group README.
- Log *after* `WaitForEndOfFrame()` for anything visual, so the time refers to the frame that showed it.

## Timing: what the numbers mean

- The Varjo headset runs at **90 frames per second** (check the exact rate in Varjo Base). Everything Unity does happens on frame boundaries, about **11.1 ms** apart.
- Key presses are noticed once per frame, so a response time has about ±11 ms of jitter from that alone.
- The logged time is when Unity finished the frame. The image reaches the eye one to two frames later, a roughly fixed delay. Fixed delays cancel when you compare conditions, but they add to absolute latencies (Group A's τ).
- All files share the lab PC's clock, so event times can be compared directly with EEG and eye-tracker times ([Data and sync](05-data-and-sync.md)).

## Brightness: turn off automatic exposure

The scenes use HDRP's **automatic exposure**: like a camera, the renderer brightens or darkens the whole image depending on what is on screen. That changes the luminance the participant sees, slowly and by itself. That matters for Group A (it can partly cancel a flicker), Group B (clutter changes average brightness) and Group C (pupil size follows luminance).

In your scene: select **Sky and Fog Volume** → in its Volume component click **Clone** next to the profile (so you don't change the other groups' scenes) → save the copy in your group folder → under **Exposure**, set **Mode** to **Fixed** and adjust **Fixed Exposure** until the scene looks right. Check it in the headset, not only in the Game view.

## Notes for each group

### Group A: flicker

- Change the patch in `Update()` based on `Time.frameCount`, not on a timer. A frequency of 90/n Hz means "on for some frames, off for the rest, repeat every n frames". Timers drift and produce uneven cycles.
- Log every on/off change (or every frame) with `EventLogger.Log`. Afterwards, check the `UnixMs` and `Frame` columns: a jump of more than one frame, or a gap well above 11 ms, is a dropped frame, and it shifts the flicker frequency for that trial.
- For sinusoidal modulation, set the patch brightness every frame from the frame count, e.g. `L = L0 * (1 + m * sin(2π f k / 90))`. The display's response is not linear in the color value (gamma), so the light that reaches the eye is not exactly sinusoidal unless you correct for it.
- Keep a small fixation point at the center of the patch.

### Group B: buttons

- Input options in the current setup: keys on a keyboard the participant holds (as in `Examples/SimpleButton.unity`, keys V and B), or **gaze plus a key**: the participant looks at a button and presses Space, and the script picks the button closest to the current gaze direction. `Varjo.XR.VarjoEyeTracking.GetGaze().gaze.forward` gives that direction in headset coordinates; `Camera.main.transform.TransformDirection(...)` turns it into scene coordinates. Ask the TA before planning around hand controllers.
- Randomize button positions every trial with `Random.Range`, and log the layout (positions in degrees) and the target in `value`, so the analysis can compute distances.
- Keep the total bright area on screen roughly constant across levels (e.g. dim buttons as you add more), so luminance does not change with `n`.

### Group C: blocks

- Keep the display identical during rest and task. Give the starting number by audio (an `AudioSource` with recorded clips). The headset has no built-in speakers, so use earbuds or the PC speakers.
- Draw block lengths with `Random.Range(20f, 30f)` and log `block_start` and `block_end` with `value` such as `"rest"` or `"task step=7"`. The logged onset is time zero for your τ fits.
- Have the participant say the final number about 12 s into the next rest block, and log that moment (the operator presses a key) so the speech can be cut from the EEG.

## Before you commit

- **Use Eye Tracking** is ticked on your Stim Control object.
- The scene plays from start to end without errors in the Console.
- GitHub Desktop lists only files in your group folder (plus their `.meta` files), and no recordings.

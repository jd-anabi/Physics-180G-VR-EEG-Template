# 1. Setup

You can build and test most of your scene on your own Windows laptop. Only recordings need the lab PC with the Varjo headset and the Emotiv EEG.

## Install (once)

1. **Git for Windows** ([git-scm.com](https://git-scm.com/download/win)). Use the default options. Unity needs it to download the Varjo plugin, which comes straight from Varjo's GitHub. GitHub Desktop alone is not enough, because Unity looks for `git` on the system PATH.
2. **GitHub Desktop** ([desktop.github.com](https://desktop.github.com)). Sign in with the GitHub account the TA added to this repository.
3. **Unity Hub** ([unity.com/download](https://unity.com/download)), then **Unity 2022.3.12f1** exactly. In Unity Hub: Installs → Install Editor → Archive → "download archive", find 2022.3.12f1 and click "Unity Hub". A different version will try to upgrade the project; don't let it.
4. **Python 3.10 or newer** with the packages in `analysis/requirements.txt` (see [Analysis](06-analysis.md)).

## Get the project

1. GitHub Desktop → File → Clone repository → this repository → choose a local folder **outside** OneDrive, Proton Drive or Dropbox (sync tools fight with Unity's files).
2. Unity Hub → Projects → Add → Add project from disk → select the cloned folder.
3. Open it. **The first open takes 5 to 15 minutes**: Unity downloads the packages (including the Varjo plugin) and compiles the HDRP shaders. Later opens are fast.
4. In the Project window, open `Assets/Examples/TaskTemplate.unity`.

If the Console shows errors about `Varjo.XR` or a package that could not be resolved, Git is probably not on the PATH: close Unity, reinstall Git for Windows with the default options, restart the computer, and open the project again.

## Testing without the headset

Select the **Stim Control** object in your scene and untick **Use Eye Tracking** in the Inspector. Press Play: the scene skips calibration and the eye log, and everything else (participant code, instructions, your trials, the event log) runs in the Game view. Type in the participant code box after pressing Space, then press Enter.

Tick **Use Eye Tracking** again before committing, so the lab PC records eye data.

On a Mac, the Unity editor runs, but the Varjo plugin is Windows-only and we have not tested the project there. Expect to do your headset testing on the lab PC.

## Working with Git

- **Pull** (GitHub Desktop: Fetch origin, then Pull) before you start working.
- Work only in your group's folder. To avoid merge conflicts in scene files, decide inside your group who edits the scene at any given time.
- **Commit** small steps with a message that says what changed ("Add flicker frequency list", not "update"). **Push** when you stop.
- Never commit anything from `Assets/Data` or `Assets/Logs`. The `.gitignore` already excludes them; if GitHub Desktop ever lists a recording file as a change, stop and ask.
- Unity creates a `.meta` file next to every asset. Always commit the `.meta` file together with its asset.

## On the lab PC

Before each recording session, someone opens GitHub Desktop on the lab PC and pulls the latest version, then opens the project in Unity Hub. Recordings stay on the lab PC until they are moved to the shared data folder ([Data and sync](05-data-and-sync.md)).

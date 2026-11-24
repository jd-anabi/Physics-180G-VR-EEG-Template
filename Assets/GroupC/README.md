# Group C: mental arithmetic, alpha suppression and pupil dilation

Your scene(s) and scripts go in this folder. Start with docs/03-build-your-scene.md.

Keep this file up to date. The analysis depends on it.

## Scene

- Scene file: `GroupC_MentalArithmetic.unity`
- Script: `GroupCMentalArithmetic.cs`
- Who edits the scene: Group C

The scene is copied from the shared task template so it retains the XR rig,
eye-tracking setup, participant-ID flow, instructions, and session/event logging.
It uses the shared `ExperimentBase` session flow. During audio playback the
camera background is neutral gray, a black X is fixed in the center of the
headset view, and session text is white for contrast. Template demo objects are
hidden. The scene has its own HDRP volume profile: exposure is fixed, while sky and fog are disabled. The X is
drawn with two black bars on a world-space UI canvas attached to the headset
camera. The shared session prompts and participant-ID panel are also positioned
relative to the headset camera so they remain in view in VR.

Check the gray background brightness in the headset before collecting data.
Adjust it only after a test run with the actual headset, since perceived
brightness depends on the headset display and exposure configuration.

## Continuous audio and cue timeline

1. Open `GroupC_MentalArithmetic.unity` in Unity.
2. Select **Stim Control** and confirm `audioTrack` is assigned to
   `finalfinal_script_interleaved.mp3`. The scene has a persistent AudioSource on this object,
   and the clip is configured to preload so playback can be scheduled without
   the first-play loading delay.
3. The **Cue Timeline** on Stim Control has 87 entries, converted from the
   provided `mm:ss:hundredths` timestamps to seconds from the start of the
   audio. It logs each task start/end, recovery-rest start/end, and
   post-speech-rest start/end, plus the intro, initial rest start, and final
   prompt. The final prompt cue is at 814.5 s, the reported audio onset, before
   the last post-speech-rest end cue at 814.7 s. Phase windows use the explicit
   start and end times from the supplied schedule; the short gaps between
   windows are preserved rather than treated as overlap.
4. Set `speechMarkerKey` to the operator's marker key (default **Return**).
   The operator presses it when the participant finishes each spoken answer.
   Every press during playback is logged with the audio position and the most
   recent cue. For analysis, exclude the first 1.5 seconds after this marker
   from the post-speech rest window to reduce speech and jaw-muscle
   contamination. Use the later of the scheduled rest start and marker time
   plus 1.5 seconds as the analysis-window start. This is analysis guidance;
   the repository does not yet include a Group C arithmetic analysis script
   that applies this exclusion automatically.
5. In Play mode, focus the ID field (press Space or click it), type a
   non-empty participant ID, and submit it with Enter. The shared session flow
   moves the ID canvas out of view after submission. Instructions appear with
   the X hidden. Press Space for the 3-2-1 countdown; the X appears as the
   experiment starts and stays visible for the full recording. It is hidden
   before the shared end prompt. Press Space at the end prompt to close the
   session; Escape aborts early.
6. Test the cue times and audio in the headset before collecting data. The
   headset has no built-in speakers, so use earbuds or PC speakers.

## Events logged

| Event | Value | When it is logged |
|---|---|---|
| `audio_track_scheduled` | Track duration | Immediately before scheduled playback |
| `audio_track_start` | Track duration | When scheduled playback begins |
| `audio_cue` | `audio_s=... meaning=...` | When playback reaches each configured cue time; Trial is the cue's 1-based entry number |
| `speech_marker` | `audio_s=... after_cue=...` | Every time the operator presses the marker key during the track |
| `audio_track_end` | Track duration | When the full recording finishes |

## Conditions and trial counts

- One run plays the continuous recording once. Cue events divide it into
  meaningful analysis segments; this avoids treating a 13-minute recording as
  one task block and then repeating it for a rest block.
- The operator should press the marker key at each spoken answer. Unity logs
  each marker while the audio is playing and associates it with the most recent
  configured cue.
- Cue event timestamps are detected on Unity's frame updates as the audio
  playback position passes the configured time, so they have frame-level timing
  precision. The log also stores the intended audio position.
- Confirm cue onset times and meanings against the recording/protocol before
  collecting data. Stop early with Escape if needed.

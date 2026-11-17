# Group C: mental arithmetic, alpha suppression and pupil dilation

Your scene(s) and scripts go in this folder. Start with docs/03-build-your-scene.md.

Keep this file up to date. The analysis depends on it.

## Scene

- Scene file: `GroupC_MentalArithmetic.unity`
- Script: `GroupCMentalArithmetic.cs`
- Who edits the scene: Group C

The scene is copied from the shared task template so it retains the XR rig,
eye-tracking setup, participant-ID flow, instructions, and session/event logging.
During the experiment the camera background is white and a black X is fixed in
the center of the headset view. Template demo objects are hidden. The fixation
display stays the same during task and rest blocks. The scene's copied HDRP sky
volume is disabled so its atmospheric ground/horizon cannot replace the white
camera background. The X is drawn with two black bars on a world-space UI canvas
attached to the headset camera, rather than reusing the template's 3D text.

## Running the starter task

1. Open `GroupC_MentalArithmetic.unity` in Unity.
2. Select **Stim Control** and assign `startingNumberClip` to a recorded audio
   clip containing the starting number. Keep the audio clip in this folder so
   project changes stay within `Assets/GroupC`. The scene has one persistent
   AudioSource on Stim Control; the script copies this clip to it at startup,
   so Play mode should not create a new audio component.
3. Set `taskRestPairs`, `arithmeticStep`, `minBlockDuration`, and
   `maxBlockDuration` to the values approved for the study. The starter scene
   is configured for one task/rest pair, a step of 7, and independently
   randomized 20–30 second blocks; these are example settings, not a finalized
   study protocol.
4. Set `speechMarkerKey` to the operator's key (default **Return**) and
   `speechMarkerTime` to the intended speech timing (default **12 seconds**
   after rest begins).
5. In Play mode, focus the ID field (press Space or click it), type a
   non-empty participant ID, and submit it with Enter. The Group C scene listens
   to the input field's submit event and disables the ID canvas once submitted.
   The instructions appear after ID submission, with the X hidden. Press Space
   to show the 3-2-1 countdown; when it finishes, the X appears and the first
   task block's starting-number audio plays. The X remains visible through all
   task/rest blocks and the end-of-session prompt. At the end, press Space again
   to close the session.
   At each task onset the assigned audio clip plays from its beginning. The task
   block lasts at least as long as the clip, even if its randomized duration
   would otherwise be shorter, so the clip is not cut off. During the following
   rest, the Console notes the expected speech timing; the operator presses the
   marker key when the participant says the final number.
6. Test the scene and audio in the headset before collecting data. The headset
   has no built-in speakers, so use earbuds or PC speakers.

## Events logged

| Event | Value | When it is logged |
|---|---|---|
| `block_start` | `task step=7 duration_s=... audio_s=...` | At each task block onset, as the starting-number audio starts |
| `block_end` | Matching task value | After the full audio clip has played and the task duration has elapsed |
| `block_start` | `rest duration_s=...` | At each rest block onset |
| `speech_marker_target` | `rest target_elapsed_s=12.00` | At the configured target time after rest begins |
| `speech_marker` | `rest elapsed_s=...` | When the operator presses the marker key during rest |
| `speech_marker_missing` | `rest` | At rest end if no marker key was pressed |
| `block_end` | Matching rest value | At the end of each rest block |

## Conditions and trial counts

- The run is finite: one task block followed by one rest block per
  `taskRestPairs` setting, then an end-of-session prompt. Keep a defined block
  count and duration for interpretable EEG/event data; stop earlier with Escape
  if needed.
- Each block duration is independently drawn with `Random.Range(minBlockDuration, maxBlockDuration)`.
- If the starting-number clip is longer than the randomized task duration, the
  task block is extended to the clip length. The audio is stopped at task-block
  end; the rest block then begins.
- The operator should mark the participant's spoken final number about 12 seconds
  into the rest block. Compare `speech_marker` with `speech_marker_target` in
  the event log; the keypress timestamp marks the actual speech.
- Confirm the block count, starting numbers, arithmetic rule, and marker timing
  with the study protocol before recording participants.

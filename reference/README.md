# Reference: the lab's original material

These files come unchanged from the Elegant Mind Club's VRMovementTrackingChoice project, the experiment used in the VR + EEG week.

- `VRMovementTrackingChoice-README.md`: the lab's description of the ShapeChoice / SimpleButton experiments (`Assets/Examples`), their rtData and eyeTr files, and their MATLAB analysis.
- `mvmtChoiceAnalysis.m`: the lab's MATLAB analysis of one ShapeChoice session.

The rtData / eyeTr formats described there are what `StimControl.cs` and `StimControlButton.cs` write. Scenes built on `ExperimentBase` write an events file instead, and their eye log has four extra columns (pupil size and eye openness); see `docs/05-data-and-sync.md`.

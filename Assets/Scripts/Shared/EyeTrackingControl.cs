using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo.XR;
using UnityEngine.XR;
using System.Linq;

/*
EyeTrackingControl: Varjo eye-tracking calibration and logging (from the Elegant Mind Club's
VRMovementTrackingChoice project, extended for Physics 180G with pupil size and eye openness).

public methods:
CalibrateGaze() -- calibrates using set mode (also: Backslash key)
StartLogging()  -- start logging eye tracking data (ExperimentBase calls this after calibration)
Right Ctrl toggles logging on/off by hand.

Output: Assets/Logs/<ParticipantID><SceneName>eyeTr-<date-time>.csv, semicolon-separated,
one row per eye-tracker sample (~200 Hz). The file is written as eyeTr-<time>.csv during the
session and renamed when Play mode stops. Columns:
  CaptureTime            Unix ms (same clock as EmotivPRO's Timestamp and EventLogger)
  CalcXEccentricity      horizontal gaze angle, degrees (+ = right); NaN when invalid
  CalcYEccentricity      vertical gaze angle, degrees (+ = up); NaN when invalid
  CombinedGazeForward    gaze direction vector (x, y, z)
  Valid                  VALID / INVALID (blink or lost tracking)
  LeftPupilDiameterMM    pupil diameter, mm (0 when the eye is not tracked)
  RightPupilDiameterMM
  LeftEyeOpenness        0 (closed) to 1 (open)
  RightEyeOpenness
The first five columns are unchanged from the lab's version, so older analysis code still works.
*/

public class EyeTrackingControl : MonoBehaviour
{

    [Header("Main camera (will be set automatically)")]
    public Camera xrCamera;

    // lets you see how many eye tracking datapoints are logged per second. Runs once
    // every second and prints to debug console if true
    public bool printFramerate = false;
    int gazeDataCount = 0;
    float gazeTimer = 0f;

    // calibration mode fast is the most accurate for now, unfortunately the legacy mode is deprecated
    [Header("Gaze calibration settings")]
    [Tooltip("Legacy - 10 dots without priors; Fast: 5 dots; One Dot: quickest, least accurate")]
    public VarjoEyeTracking.GazeCalibrationMode gazeCalibrationMode = VarjoEyeTracking.GazeCalibrationMode.Fast;
    [Tooltip("Calibrate when the scene starts. Untick to keep the current calibration (headset not removed).")]
    public bool calibrateOnStart = true;

    // keys for calibration and logging
    [Tooltip("Keyboard shortcut to request calibration")]
    public KeyCode calibrationKey = KeyCode.Backslash;
    public KeyCode loggingToggleKey = KeyCode.RightControl;

    // we don't want to filter our data to look smoother since we lose accuracy
    [Header("Gaze output filter")]
    [Tooltip("Standard: smoothing on gaze data; None: raw data")]
    public VarjoEyeTracking.GazeOutputFilterType gazeFilterType = VarjoEyeTracking.GazeOutputFilterType.None;
    
    // frequency of eye tracking logging
    private VarjoEyeTracking.GazeOutputFrequency frequency = VarjoEyeTracking.GazeOutputFrequency.MaximumSupported;

    // stuff for logging data
    private static readonly string[] Columns = { "CaptureTime", "CalcXEccentricity", 
        "CalcYEccentricity", "CombinedGazeForward", "Valid",
        "LeftPupilDiameterMM", "RightPupilDiameterMM", "LeftEyeOpenness", "RightEyeOpenness"};
    private static string calcPrecision = "F6"; // precision after decimal for calculations and tracking
    private const string ValidString = "VALID";
    private const string InvalidString = "INVALID";
    [Header("Logging path (defaults to Logs)")]
    public bool useCustomLogPath = false;
    public string customLogPath = "";
    private List<VarjoEyeTracking.GazeData> dataSinceLastUpdate;
    private List<VarjoEyeTracking.EyeMeasurements> eyeMeasurementsSinceLastUpdate;
    private StreamWriter writer = null;
    public bool logging = false;
    public string fileName = "";

    // Start is called before the first frame update
    private void Start()
    {
        xrCamera = Camera.main;
        // sets eye tracking parameters
        VarjoEyeTracking.SetGazeOutputFrequency(frequency);
        VarjoEyeTracking.SetGazeOutputFilterType(gazeFilterType);
        // starts calibration (untick calibrateOnStart to reuse the calibration from the previous scene,
        // as long as the headset has not been taken off)
        if (calibrateOnStart || !VarjoEyeTracking.IsGazeCalibrated())
        {
            CalibrateGaze();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // prints framerate if set to true
        if (logging && printFramerate)
        {
            gazeTimer += Time.deltaTime;
            if (gazeTimer >= 1.0f)
            {
                Debug.Log("Gaze data rows per second: " + gazeDataCount);
                gazeDataCount = 0;
                gazeTimer = 0f;
            }
        }

        // runs calibration if calibration key is pressed
        if (Input.GetKeyDown(calibrationKey))
        {
            CalibrateGaze();
        }

        // starts or stops logging if logging key is pressed
        if (Input.GetKeyDown(loggingToggleKey))
        {
            // Check if gaze is calibrated
            if (!VarjoEyeTracking.IsGazeCalibrated())
            {
                Debug.Log("GAZE IS NOT CALIBRATED - isgazecalibrated:" + VarjoEyeTracking.IsGazeCalibrated());
            }
            if (!logging)
                StartLogging();
            else
                StopLogging();
            return;
        }

        // logs every loop if logging has started
        if (logging)
        {
            int dataCount = VarjoEyeTracking.GetGazeList(out dataSinceLastUpdate, out eyeMeasurementsSinceLastUpdate);
            for (int i = 0; i < dataCount; ++i)
            {
                LogGazeData(dataSinceLastUpdate[i], eyeMeasurementsSinceLastUpdate[i]);
            }
            if (printFramerate)
            {
                gazeDataCount += dataCount;
            }
        }
    }

    void LogGazeData(VarjoEyeTracking.GazeData data, VarjoEyeTracking.EyeMeasurements eye)
    {
        // if data isn't valid, warn user
        if (data.status == VarjoEyeTracking.GazeStatus.Invalid)
        {
            //Debug.Log("GAZE IS INVALID");
        }
        string[] logData = new string[Columns.Length];
    
        // capture time (Unix ms timestamp)
        logData[0] = ((DateTimeOffset)VarjoTime.ConvertVarjoTimestampToDateTime(data.captureTime)).ToUnixTimeMilliseconds().ToString();

        // calculated X, Y eccentricities
        float x = data.gaze.forward.x;
        float y = data.gaze.forward.y;
        float z = data.gaze.forward.z;
        logData[1] = (Math.Atan(x / z) / Math.PI * 180).ToString(calcPrecision);
        logData[2] = (Math.Atan(y / z) / Math.PI * 180).ToString(calcPrecision);
        logData[3]= data.gaze.forward.ToString(calcPrecision);
        bool invalid = data.status == VarjoEyeTracking.GazeStatus.Invalid;
        logData[4] = invalid ? InvalidString : ValidString;

        // pupil diameter (mm) and eye openness (0-1) for each eye
        logData[5] = eye.leftPupilDiameterInMM.ToString(calcPrecision);
        logData[6] = eye.rightPupilDiameterInMM.ToString(calcPrecision);
        logData[7] = eye.leftEyeOpenness.ToString(calcPrecision);
        logData[8] = eye.rightEyeOpenness.ToString(calcPrecision);

        Log(logData);
    }

    // Write given values in the log file
    void Log(string[] values)
    {
        if (!logging || writer == null)
            return;

        string line = "";
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = values[i].Replace("\r", "").Replace("\n", ""); // Remove new lines so they don't break csv
            line += values[i] + (i == (values.Length - 1) ? "" : ";"); // Do not add semicolon to last data string
        }
        writer.WriteLine(line);
    }

    public void StartLogging()
    {
        if (logging)
        {
            Debug.LogWarning("Logging was on when StartLogging was called. No new log was started.");
            return;
        }

        logging = true;

        string logPath = useCustomLogPath ? customLogPath : Application.dataPath + "/Logs/";
        Directory.CreateDirectory(logPath);

        DateTime now = DateTime.Now;
        fileName = "eyeTr-"+ System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        string path = logPath + fileName + ".csv";
        writer = new StreamWriter(path);

        Log(Columns);
        Debug.Log("Log file started at: " + path);
        gazeTimer += Time.deltaTime;
    }

    void StopLogging()
    {
        if (!logging)
            return;

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
        logging = false;
        Debug.Log("Logging ended");
    }

    public void CalibrateGaze()
    {
        VarjoEyeTracking.RequestGazeCalibration(gazeCalibrationMode);
    }

    void OnApplicationQuit()
    {
        // renames the file with the inputted participant name so that it's comparable with the rtData file
        // Check if logging was enabled and if a custom path was used
        if (!logging || (writer == null && !useCustomLogPath))
        {
            return;
        }
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
        logging = false;
        Debug.Log("Logging ended");

        // Define the old and new file names
        string logPath = useCustomLogPath ? customLogPath : Application.dataPath + "/Logs/";
        string oldFileName = fileName + ".csv";
        string newFileName = null;

        if (SessionInfo.IsStarted)
        {
            // Scenes built on ExperimentBase: same name pattern as the event log.
            newFileName = SessionInfo.FileName("eyeTr");
        }
        else
        {
            // The lab's original scenes (StimControl / StimControlButton).
            GameObject stimControlObj = GameObject.Find("Stim Control");
            if (stimControlObj != null && stimControlObj.TryGetComponent(out StimControlButton stimButton))
            {
                newFileName = stimButton.logFile;
            }
            else if (stimControlObj != null && stimControlObj.TryGetComponent(out StimControl stimControl))
            {
                newFileName = stimControl.logFile;
            }
        }
        if (string.IsNullOrEmpty(newFileName))
        {
            Debug.LogWarning("No participant ID was entered, so the eye log keeps its original name: " + fileName + ".csv");
            return;
        }

        int lastIndex = Math.Max(newFileName.LastIndexOf('/'), newFileName.LastIndexOf('\\'));
        // If a slash or backslash is found, return the substring from just after it
        if (lastIndex != -1)
        {
            newFileName = newFileName.Substring(lastIndex + 1);
        }
        newFileName = newFileName.Replace("rtData", "eyeTr");

        string oldFilePath = Path.Combine(logPath, oldFileName);
        string newFilePath = Path.Combine(logPath, newFileName);

        // Check if the old file exists
        if (File.Exists(oldFilePath))
        {
            // Rename the file
            File.Move(oldFilePath, newFilePath);
            Debug.Log("Log file renamed to: " + newFilePath);
            // Deletes the meta file created for it the old filepath. Unity should automatically create a new one.
            string oldMetaFilePath = oldFilePath + ".meta";
            if (File.Exists(oldMetaFilePath))
            {
                File.Delete(oldMetaFilePath);
            }
        }
        else
        {
            Debug.LogError("Log file not found: " + oldFilePath);
        }
    }
}

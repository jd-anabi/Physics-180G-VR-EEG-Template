using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Random = System.Random;
using System.IO;
using System;
using TMPro;
using Varjo.XR;

/* Requirements:
 * - This script must be attached to only one GameObject in the scene.
 * - All names in the first section of variables must correspond to real objects in the scene,
 *   and their names must match exactly, including case sensitivity.
 * - An object called "textInputCanvas" must exist in the scene.
 * - An object called "cue" must exist in the scene.
 */

public class StimControlButton : MonoBehaviour
{
    public bool cueOn = true;
    // Instruction text values
    public string[] instrTextValues = {
    // instruction 1
    @"You will be watching a sphere move and change directions. Please try to
    keep your eyes on the sphere for the duration of the protocol. Press
    Spacebar when ready."
    };

    // Counter for finishing the program
    public int currentTrial = 1;
    public int trials = 50;
    // Global variables for time
    public float preCue_time = 0.5f; // Wait time before cue is shown after trial ends
    public float cue_time = 0.2f; // Time that the cue is on screen
    public float time_min = 0.5f; // Minimum time between cue disappearing and stimulus    
    public float time_max = 1.5f; // Maximum time between cue disappearing and stimulus
    public float cueToStim_time = 0f; // Randomly set later in the code

    public int countdownTime = 5; // Time between training and experiment phase
    public int calibrationPeriodCycle = 0; // Time between training and experiment phase

    // Phase of the experiment
    public int phase = 1;
    private bool in_use = false;    // Avoid user clicking multiple buttons at the same time
    /*
     * Phase -1, -2, -3... = In-between phase 1, 2, or 3, while co-routines are running
     * Phase 0 = *Unused for now*
     * Phase 1 = Waiting for eye tracking to calibrate
     * Phase 2 = Name input
     * Phase 3 = Start / Instructions
     * Phase 4 = Training phase
     * Phase 5 = Break 
     * Phase 6 = Data taking phase
     * Phase 7 = Thank you screen / Demographics survey reminder
     * in_use = Currently going through the change coroutine, has not shown the next stimulus yet
     */

    // Misc variables
    static string dataPath = Directory.GetCurrentDirectory() + "/Assets/Data/";
    public string logFile; // File name, set in phase 0 after getting participant name
    Random rnd = new Random();
    private string responseKey = "";
    private string log; // New line of data
    private int instrNum = 0; // Index used to increment instructions
    private int posIndex, stimIndex; // Indices for position and stimuli respectively randomized later in code (need global scope since they're used in multiple functions)
    public GameObject instrText; // Text object for instructions
    public TMP_InputField nameInputField; // UI object for name input
    public string participantID;
    public EventSystem eventSystem;


    // ball variables
    public GameObject blackSphere, redSphere, blueSphere;
    
    public float minDistance = 5f; // Minimum distance for first ball to travel    
    public float maxDistance = 15f; // Maximum distance for first ball to travel
    public float secondDistance = 10f; // distance for choice balls to travel
    public Vector3 center = new Vector3(0, 0, -0.5f); // Position of the center point (e.g., the camera)

    public float radius = 0.5f; // distance from ball to camera in meters
    public float angularSpeed = 10f; //angular speed of the ball in degrees

    double getPosfromDeg(double x)
    {
        // Convert degrees to radians since Math.Tan expects radians
        double radians = x * Math.PI / 180.0;
        // Calculate tan(x) and then divide by 2
        double result = Math.Tan(radians) / 2.0;
        Debug.Log(x);
        Debug.Log(result);
        return result;
    }


    IEnumerator phase1() // Eye Tracking Calibration
    {
        phase *= -1;
        // Update the text with the current number of periods
        instrText.GetComponent<TextMeshPro>().text = 
        @"(Press Backslash to restart calibration)
        Eye tracking calibration" + new string('.', calibrationPeriodCycle);
        // Increment the period count, reset if it reaches 4
        calibrationPeriodCycle = (calibrationPeriodCycle + 1) % 4;

        yield return new WaitForSecondsRealtime(1);
        // If calibration ends, sets up the next phase
        if (VarjoEyeTracking.IsGazeCalibrated())
        {
            // Removes text
            instrText.GetComponent<TextMeshPro>().text = "";
            instrText.transform.position = GameObject.Find("disappearPos").transform.position;
            GameObject.Find("eyeTracking").GetComponent<EyeTrackingControl>().StartLogging();
            // Sets up for phase 2
            GameObject.Find("textInputCanvas").transform.position = GameObject.Find("textPos").transform.position;
            phase = 2;
            yield break;
        }
        phase *= -1;
    }

    void phase2() // Participant name/ID input phase
    {
        // Selects Input Field if space is pressed. Workaround for Unity Bug
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Set the input field as the currently selected object
            EventSystem.current.SetSelectedGameObject(nameInputField.gameObject, null);
            // Optionally, focus on the input field to start typing immediately
            nameInputField.ActivateInputField();
        }
        // Creates data file and sets participant name
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            participantID = nameInputField.text;
            // Creates data folder / file
            string currentSceneName = SceneManager.GetActiveScene().name;
            Debug.Log("Current Scene: " + currentSceneName);

            logFile = dataPath + participantID + currentSceneName + "rtData-" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            File.WriteAllText(logFile, "CueShowTime,moveStartTime,direction,SplitTime,xEcc,x,y,z,endTime,redDirection,redXEcc,redYEcc,buttonPressTime,buttonPressed\n");
            Debug.Log($"Data file started for {nameInputField.text}");
            // Moves canvas to behind the plane
            GameObject.Find("textInputCanvas").transform.position = GameObject.Find("disappearPos").transform.position; // Canvas disappears

            // Warns if logging has not started
            bool loggingStarted = GameObject.Find("eyeTracking").GetComponent<EyeTrackingControl>().logging;
            if (!loggingStarted)
            {
                Debug.Log("Eye tracking was not started.");
            }
            // Sets things up for phase 3, showing instruction 1
            phase = 3;
            instrText.GetComponent<TextMeshPro>().text = instrTextValues[instrNum];
            instrText.transform.position = GameObject.Find("textPos").transform.position;
            return;
        }
    }

    void phase3() // Start and instruction phase
    {
        // moves onto next phase since there's only one instruction screen
        if (Input.GetKeyDown(KeyCode.Space) && instrNum == 0)
        {
            instrNum++;
            instrText.transform.position = GameObject.Find("disappearPos").transform.position;
            phase = 4;
        }
    }

    IEnumerator phase4() // Break phase
    {
        phase *= -1;
        // Shows and updates text for the break
        instrText.GetComponent<TextMeshPro>().text = $"The experiment will begin in {countdownTime} seconds";
        instrText.transform.position = GameObject.Find("textPos").transform.position;
        yield return new WaitForSecondsRealtime(1f);
        countdownTime -= 1;
        // If countdown reaches 0, sets up the next phase
        if (countdownTime == 0)
        {
            // Removes text
            instrText.GetComponent<TextMeshPro>().text = "";
            instrText.transform.position = GameObject.Find("disappearPos").transform.position;
            phase = 5;
            yield break;
        }
        phase *= -1;
    }

    // Move a single object horizontally around the camera
    public IEnumerator MoveObject(GameObject obj, int direction, float distance)
    {
        float movedDistance = 0f;
        while (movedDistance < distance)
        {
            float step = Mathf.Min(angularSpeed * Time.deltaTime, distance - movedDistance);
            movedDistance += step;
            // Calculate new position around the camera
            float angle = direction * step;
            obj.transform.RotateAround(center, Vector3.up, angle);
            yield return null;
        }
    }

    // Modified: Check if blueSphere exists before setting its position
    public IEnumerator MoveTwoObjects(GameObject redSphere, GameObject blueSphere, int redDirection, float distance)
    {
        float totalAngleMoved = 0f; // Track total movement along the sphere in degrees
        // Convert initial position to spherical coordinates
        Vector3 initialPosition = redSphere.transform.position - center; // Relative to sphere center
        float initialTheta = Mathf.Atan2(initialPosition.x, initialPosition.z); // Horizontal angle (azimuth)
        float currentPhi = Mathf.Acos(initialPosition.y / radius); // Vertical angle (polar angle)
        while (totalAngleMoved < distance)
        {
            // Calculate the angle step for this frame
            float angleStep = angularSpeed * Time.deltaTime; // Step size in degrees
            totalAngleMoved += angleStep;
            // Update the polar angle (vertical angle)
            currentPhi += Mathf.Deg2Rad * angleStep * redDirection;
            // Ensure the polar angle stays within bounds (0 to π)
            currentPhi = Mathf.Clamp(currentPhi, 0f, Mathf.PI);
            // Convert spherical coordinates back to Cartesian
            float newX = radius * Mathf.Sin(currentPhi) * Mathf.Sin(initialTheta);
            float newY = radius * Mathf.Cos(currentPhi);
            float newZ = radius * Mathf.Sin(currentPhi) * Mathf.Cos(initialTheta);
            // Update the positions of the spheres
            Vector3 newPosition = center + new Vector3(newX, newY, newZ);
            redSphere.transform.position = newPosition;
            // Only update blueSphere if it exists
            if (blueSphere != null)
            {
                Vector3 newPositionBlue = center + new Vector3(newX, -newY, newZ);
                blueSphere.transform.position = newPositionBlue;
            }
            yield return null; // Wait for the next frame
        }
    }
//–– at class scope ––
private bool isListening    = false;
private bool buttonDetected = false;
private long buttonTimestamp = 0;
private string buttonPressed = "None";  // Track which button was pressed

IEnumerator ListenForButtonPress()
{
    while (isListening)
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            buttonDetected = true;
            buttonTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            buttonPressed = "V";
            isListening = false;
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            buttonDetected = true;
            buttonTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            buttonPressed = "B";
            isListening = false;
        }
        yield return null;
    }
    }

IEnumerator phase5() // Data taking phase
    {
        // Checks if a trial is currently running
        if (!in_use)
        {
            in_use = true;
            currentTrial++;

            yield return new WaitForSecondsRealtime(preCue_time); // Wait before trial starts

            if (cueOn == true)
            {
                GameObject.Find("cue").transform.position = GameObject.Find("ballStartPos").transform.position; // Cue appears at center
                log = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ","; // CueShowTime
                yield return new WaitForSecondsRealtime(cue_time); // Cue stays there for this long
            }
            else
            {
                log = DateTimeOffset.Now.ToUnixTimeMilliseconds() + ",";
            }

            cueToStim_time = (float)((rnd.NextDouble() * (time_max - time_min)) + time_min);
            // Waits before showing stimulus
            yield return new WaitForSecondsRealtime(cueToStim_time);

            // Hides cue
            GameObject.Find("cue").transform.position = GameObject.Find("disappearPos").transform.position;
            log += DateTimeOffset.Now.ToUnixTimeMilliseconds() + ",";
            blackSphere.transform.position = GameObject.Find("ballStartPos").transform.position;

            // Move ball in a random direction for a random distance
            int randomDirection = (int)(rnd.Next(2) == 0 ? 1f : -1f);
            float randomDistance = (float)((rnd.NextDouble() * (maxDistance - minDistance)) + minDistance);
            yield return StartCoroutine(MoveObject(blackSphere, randomDirection, randomDistance));

            // Stop ball, disappear sphere, spawn red and blue spheres, log time
            // Check if blueSphere exists before setting its position
            if (blueSphere != null)
            {
                blueSphere.transform.position = blackSphere.transform.position;
            }
            redSphere.transform.position = blackSphere.transform.position;
            log += randomDirection + ",";
            log += DateTimeOffset.Now.ToUnixTimeMilliseconds() + ",";
            // Log eccentricity and position of blackSphere
            float deltaX = blackSphere.transform.position.x;
            float deltaY = blackSphere.transform.position.y;
            float deltaZ = blackSphere.transform.position.z + 0.5f; // Camera at z = -0.5
            float angleRadians = Mathf.Atan2(deltaX, deltaZ);
            float angleDegrees = angleRadians * Mathf.Rad2Deg;
            log += angleDegrees + ",";
            float distanceXZ = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            float angleYRadians = Mathf.Atan2(deltaY, distanceXZ);
            float angleYDegrees = angleYRadians * Mathf.Rad2Deg;
            log += blackSphere.transform.position.x + "," + blackSphere.transform.position.y + "," + blackSphere.transform.position.z + ",";
            blackSphere.transform.position = GameObject.Find("disappearPos").transform.position;

                // begin listening for button press
            isListening      = true;
            buttonDetected   = false;
            buttonTimestamp  = 0;
            StartCoroutine(ListenForButtonPress());


            // Move red and blue spheres in random opposite directions
            int redDirection = (int)(rnd.Next(2) == 0 ? 1f : -1f);
            yield return StartCoroutine(MoveTwoObjects(redSphere, blueSphere, redDirection, secondDistance));

            // Stop red and blue spheres, disappear
            deltaX = redSphere.transform.position.x;
            deltaY = redSphere.transform.position.y;
            deltaZ = redSphere.transform.position.z + 0.5f; // Camera at z = -0.5
            angleRadians = Mathf.Atan2(deltaX, deltaZ);
            angleDegrees = angleRadians * Mathf.Rad2Deg;
            distanceXZ = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            angleYRadians = Mathf.Atan2(deltaY, distanceXZ);
            angleYDegrees = angleYRadians * Mathf.Rad2Deg;
            log += DateTimeOffset.Now.ToUnixTimeMilliseconds() + ",";
            log += redDirection + ",";
            log += angleDegrees + ",";
            log += angleYDegrees + ",";

            // stop listening now (in case no press ever happened)
            isListening = false;
            log += buttonDetected
                ? buttonTimestamp + "," + buttonPressed
                : "0,None";

            log += "\n";



            // Only update blueSphere if it exists
            if (blueSphere != null)
            {
                blueSphere.transform.position = GameObject.Find("disappearPos").transform.position;
            }
            redSphere.transform.position = GameObject.Find("disappearPos").transform.position;

            // log all times from trial
            File.AppendAllText(logFile, log);
            log = "";
            // If the number of experimental trials specified has been reached, go to the next phase
            if (currentTrial > trials)
            {
                phase = 6;
                yield return null;
            }
            in_use = false;
        }
    }

    IEnumerator phase6() // Thank you screen / demographics survey reminder
    {
        phase *= -1;
        // Shows text for 2 seconds and ends the protocol
        instrText.GetComponent<TextMeshPro>().text = "Thank you for taking data for us! Please take your demographics survey now";
        instrText.transform.position = GameObject.Find("textPos").transform.position;
        yield return new WaitForSecondsRealtime(2f);
        UnityEditor.EditorApplication.isPlaying = false;
        phase *= -1;
    }

    void Start()
    {
        // Initializes variables
        instrText = GameObject.Find("instrText"); // Text object used
        blackSphere = GameObject.Find("blackSphere"); 
        redSphere = GameObject.Find("redSphere");
        // blueSphere may or may not exist in this scene
        blueSphere = GameObject.Find("blueSphere"); 
        nameInputField = GameObject.Find("nameInputField").GetComponent<TMP_InputField>();
        eventSystem = GameObject.Find("EventSystem").GetComponent<EventSystem>();
        instrText.transform.position = GameObject.Find("textPos").transform.position;
    }

    void Update()
    {
        // Checks if escape is pressed to end the protocol
        if (Input.GetKey(KeyCode.Escape))
        {
            // This only works in editor view
            UnityEditor.EditorApplication.isPlaying = false;
            // This only works for built programs
            // Application.Quit();
        }
        // Runs code depending on which phase is currently ongoing
        else if (phase < 0)
        {
            return;
        }
        else if (phase == 1)
        {
            StartCoroutine(phase1());
        }
        else if (phase == 2)
        {
            phase2();
        }
        else if (phase == 3)
        {
            phase3();
        }
        else if (phase == 4)
        {
            StartCoroutine(phase4());
        }
        else if (phase == 5)
        {
            StartCoroutine(phase5());
        }
        else if (phase == 6)
        {
            StartCoroutine(phase6());
        }
    }

    void OnApplicationQuit()
    {
        // Adds PC info to the data file
        string pcDataFilePath = Directory.GetCurrentDirectory() + "/Assets/Data/runData.csv";
        if (!File.Exists(pcDataFilePath))
        {
            using (StreamWriter writer = new StreamWriter(pcDataFilePath))
            {
                writer.WriteLine("cpuID,file,Trials");
            }
        }
        using (StreamWriter writer = File.AppendText(pcDataFilePath))
        {
            string computerName = SystemInfo.deviceName;
            string pcID = SystemInfo.deviceUniqueIdentifier;
            currentTrial--;
            string nameAndTime = logFile;
            int lastIndex = Math.Max(logFile.LastIndexOf('/'), logFile.LastIndexOf('\\'));
            if (lastIndex != -1)
            {
                nameAndTime = logFile.Substring(lastIndex + 1);
            }
            writer.WriteLine($"{computerName},{pcID},{nameAndTime},{currentTrial}");
        }
    }
}

using System.IO;
using UnityEngine;

/*
 * EventLogger: writes one CSV row per event, timestamped in Unix milliseconds.
 *
 * File: Assets/Data/<ParticipantID><SceneName>events-<date-time>.csv
 * Columns:
 *   UnixMs  milliseconds since 1970 on the PC clock (same clock as EmotivPRO and the eye tracker)
 *   Frame   Unity's frame counter (Time.frameCount) when the row was written
 *   Trial   trial or block number you pass in (-1 if not given)
 *   Event   a short name you choose, e.g. "stim_on", "block_start", "button_press"
 *   Value   anything extra, e.g. "15Hz", "n=8", "level=7"
 *
 * Usage (from any script, after ExperimentBase has started the session):
 *   long t = EventLogger.Log("stim_on", "15Hz", trial);
 *
 * Commas and line breaks inside Event or Value are replaced so the CSV stays valid.
 * The file is flushed after every row, so a crash loses at most the current row.
 */
public static class EventLogger
{
    static StreamWriter writer;
    static bool quitHooked = false;

    public static string FilePath { get; private set; } = "";

    public static bool IsOpen
    {
        get { return writer != null; }
    }

    public static void Begin()
    {
        if (!SessionInfo.IsStarted)
        {
            Debug.LogError("EventLogger.Begin: call SessionInfo.Begin(participantID, sceneName) first.");
            return;
        }
        End(); // close a previous file if one is still open

        string dir = Path.Combine(Application.dataPath, "Data");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, SessionInfo.FileName("events"));
        writer = new StreamWriter(FilePath, false);
        writer.AutoFlush = true;
        writer.WriteLine("UnixMs,Frame,Trial,Event,Value");
        Debug.Log("Event log started: " + FilePath);

        if (!quitHooked)
        {
            // In the Editor this also runs when Play mode is stopped.
            Application.quitting += End;
            quitHooked = true;
        }
    }

    // Writes one row and returns the timestamp it used.
    public static long Log(string eventName, string value = "", int trial = -1)
    {
        long t = SessionInfo.NowUnixMs();
        if (writer == null)
        {
            Debug.LogWarning("EventLogger.Log called before Begin(): " + eventName);
            return t;
        }
        writer.WriteLine(t + "," + Time.frameCount + "," + trial + "," + Clean(eventName) + "," + Clean(value));
        return t;
    }

    public static void End()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
            Debug.Log("Event log closed: " + FilePath);
        }
    }

    static string Clean(string s)
    {
        if (s == null) return "";
        return s.Replace(",", ";").Replace("\r", " ").Replace("\n", " ");
    }
}

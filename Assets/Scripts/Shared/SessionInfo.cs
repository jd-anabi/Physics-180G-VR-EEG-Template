using System;
using System.IO;

/*
 * SessionInfo: one place that knows who is being recorded and how to name their files.
 *
 * ExperimentBase calls SessionInfo.Begin(...) after the participant ID is typed in.
 * Every data file from that session then shares one name pattern:
 *
 *     <ParticipantID><SceneName><kind>-<yyyy-MM-dd-HH-mm-ss>.csv
 *     e.g. P07GroupC_Arithmeticevents-2026-11-19-13-05-12.csv
 *          P07GroupC_ArithmeticeyeTr-2026-11-19-13-05-12.csv
 *
 * so the event file and the eye-tracking file of one session can be paired by name,
 * the same way the lab's rtData / eyeTr files are paired.
 */
public static class SessionInfo
{
    public static string ParticipantID { get; private set; } = "";
    public static string SceneName { get; private set; } = "";
    public static string StartStamp { get; private set; } = "";

    public static bool IsStarted
    {
        get { return ParticipantID.Length > 0; }
    }

    public static void Begin(string participantID, string sceneName)
    {
        ParticipantID = Sanitize(participantID);
        SceneName = Sanitize(sceneName);
        StartStamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
    }

    // File name for one kind of output, e.g. FileName("events") or FileName("eyeTr").
    public static string FileName(string kind)
    {
        return ParticipantID + SceneName + kind + "-" + StartStamp + ".csv";
    }

    // Milliseconds since 1 Jan 1970 (UTC), from the PC clock.
    // EmotivPRO's Timestamp column and the eye tracker's CaptureTime use the same clock,
    // so these numbers can be compared directly across all files.
    public static long NowUnixMs()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    static string Sanitize(string s)
    {
        if (s == null) return "";
        s = s.Trim().Replace(" ", "");
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            s = s.Replace(c.ToString(), "");
        }
        return s;
    }
}

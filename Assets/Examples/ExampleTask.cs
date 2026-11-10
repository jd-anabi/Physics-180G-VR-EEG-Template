using System.Collections;
using UnityEngine;

/*
 * ExampleTask: the smallest useful experiment built on ExperimentBase.
 * It is attached to the "Stim Control" object in Examples/TaskTemplate.unity.
 *
 * Each trial: wait a random 1-2 s, show the "cue" dot at the center, log when it appeared,
 * wait up to 1.5 s for Space, log the response (or no response), hide the dot.
 *
 * Copy this file into your group folder, rename the class (the class name must match the
 * file name), and replace the trial loop with your own design.
 */
public class ExampleTask : ExperimentBase
{
    [Header("Example task")]
    public int trials = 10;
    public float minWait = 1.0f;
    public float maxWait = 2.0f;
    public float responseWindow = 1.5f;

    protected override IEnumerator RunExperiment()
    {
        GameObject cue = GameObject.Find("cue");
        Transform center = GameObject.Find("ballStartPos").transform;

        for (int trial = 1; trial <= trials; trial++)
        {
            yield return new WaitForSecondsRealtime(Random.Range(minWait, maxWait));

            // Show the stimulus, wait until that frame has been rendered, then log it.
            cue.transform.position = center.position;
            yield return new WaitForEndOfFrame();
            long onset = EventLogger.Log("cue_on", "", trial);

            // Key presses are checked once per frame (about every 11 ms at 90 Hz).
            float t0 = Time.realtimeSinceStartup;
            bool responded = false;
            while (Time.realtimeSinceStartup - t0 < responseWindow)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    long t = EventLogger.Log("response", "space", trial);
                    Debug.Log("Trial " + trial + ": response after " + (t - onset) + " ms");
                    responded = true;
                    break;
                }
                yield return null;
            }
            if (!responded) EventLogger.Log("no_response", "", trial);

            Hide(cue);
            yield return new WaitForEndOfFrame();
            EventLogger.Log("cue_off", "", trial);
        }
    }
}

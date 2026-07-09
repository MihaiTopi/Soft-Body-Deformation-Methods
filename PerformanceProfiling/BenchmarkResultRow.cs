using TMPro;
using UnityEngine;

public class BenchmarkResultRow : MonoBehaviour
{
    public TMP_Text testTypeText;
    public TMP_Text methodText;
    public TMP_Text totalMillisecondsText;
    public TMP_Text stepMillisecondsText;
    public TMP_Text fpsText;
    public TMP_Text samplesText;

    public void SetData(
        string testType,
        string method,
        double totalMilliseconds,
        double stepMilliseconds,
        double fps,
        int samples)
    {
        if (testTypeText != null)
        {
            testTypeText.text = testType;
        }

        if (methodText != null)
        {
            methodText.text = method;
        }

        if (totalMillisecondsText != null)
        {
            totalMillisecondsText.text = totalMilliseconds.ToString("F2");
        }

        if (stepMillisecondsText != null)
        {
            stepMillisecondsText.text = stepMilliseconds.ToString("F4");
        }

        if (fpsText != null)
        {
            fpsText.text = fps.ToString("F1");
        }

        if (samplesText != null)
        {
            samplesText.text = samples.ToString();
        }
    }
}
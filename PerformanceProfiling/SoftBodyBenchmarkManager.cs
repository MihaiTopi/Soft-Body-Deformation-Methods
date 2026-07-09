using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SoftBodyBenchmarkManager : MonoBehaviour
{
    [System.Serializable]
    public class BenchmarkTarget
    {
        public string testName;
        public string methodName;
        public GameObject sphereObject;
        public Vector3 startPosition = new Vector3(4.044f, 4.84f, -5.55f);
        public Vector3 startRotationEuler = Vector3.zero;

        [Header("Per-Test Timing")]
        public float warmupTime = 0f;
        public float testDuration = 5f;
    }

    private struct BenchmarkResult
    {
        public string TestName;
        public string MethodName;
        public double TotalSimulationMilliseconds;
        public double AverageMilliseconds;
        public double AverageRealFps;
        public int Samples;
    }

    [Header("Benchmark Targets")]
    public List<BenchmarkTarget> targets = new List<BenchmarkTarget>();

    [Header("Global Timing")]
    public float pauseBetweenTests = 2f;

    [Header("UI")]
    public TMP_Text statusText;
    public GameObject runButton;
    public GameObject exitButton;
    public GameObject resultsPanel;

    [Header("Result Row UI")]
    public Transform resultsRowParent;
    public BenchmarkResultRow resultRowTemplate;
    public float resultRowVerticalSpacing = 200f;

    private readonly List<BenchmarkResult> results = new List<BenchmarkResult>();
    private readonly List<GameObject> generatedResultRows = new List<GameObject>();
    private bool isRunning;

    private void Start()
    {
        DisableOriginalTargets();

        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }

        if (resultRowTemplate != null)
        {
            resultRowTemplate.gameObject.SetActive(true);
        }

        if (statusText != null)
        {
            statusText.text = "Ready.";
        }

        if (runButton != null)
        {
            runButton.SetActive(true);
        }

        if (exitButton != null)
        {
            exitButton.SetActive(true);
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    public void RunTest()
    {
        if (isRunning)
        {
            return;
        }

        StartCoroutine(RunBenchmarkRoutine());
    }

    private IEnumerator RunBenchmarkRoutine()
    {
        isRunning = true;
        results.Clear();
        ClearGeneratedRows();

        if (resultsPanel != null)
        {
            resultsPanel.SetActive(false);
        }

        if (runButton != null)
        {
            runButton.SetActive(false);
        }

        DisableOriginalTargets();

        foreach (BenchmarkTarget target in targets)
        {
            if (target == null || target.sphereObject == null)
            {
                continue;
            }

            yield return RunSingleTest(target);
        }

        DisableOriginalTargets();
        DisplayResults();

        if (statusText != null)
        {
            statusText.text = "Benchmark finished.";
        }

        if (runButton != null)
        {
            runButton.SetActive(true);
        }

        isRunning = false;
    }

    private IEnumerator RunSingleTest(BenchmarkTarget target)
    {
        DisableOriginalTargets();

        if (statusText != null)
        {
            statusText.text =
                "Preparing " + target.methodName +
                " - " + target.testName + "...";
        }

        GameObject testInstance = CreateTestInstance(target);

        SimulationProfiler profiler =
            testInstance.GetComponent<SimulationProfiler>();

        if (profiler == null)
        {
            profiler = testInstance.AddComponent<SimulationProfiler>();
        }

        profiler.ResetStats();

        if (target.warmupTime > 0f)
        {
            float warmupStartTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - warmupStartTime < target.warmupTime)
            {
                float warmupElapsed = Time.realtimeSinceStartup - warmupStartTime;

                if (statusText != null)
                {
                    statusText.text =
                        "Warmup " + target.methodName +
                        " - " + target.testName +
                        " (" + warmupElapsed.ToString("F1") +
                        " / " + target.warmupTime.ToString("F1") + " s)";
                }

                yield return null;
            }

            profiler.ResetStats();
        }

        float startRealTime = Time.realtimeSinceStartup;
        double fpsSum = 0;
        int fpsSamples = 0;

        while (Time.realtimeSinceStartup - startRealTime < target.testDuration)
        {
            float elapsed = Time.realtimeSinceStartup - startRealTime;

            if (Time.unscaledDeltaTime > 0)
            {
                fpsSum += 1.0 / Time.unscaledDeltaTime;
                fpsSamples++;
            }

            if (statusText != null)
            {
                statusText.text =
                    "Testing " + target.methodName +
                    " - " + target.testName +
                    " (" + elapsed.ToString("F1") +
                    " / " + target.testDuration.ToString("F1") + " s)";
            }

            yield return null;
        }

        BenchmarkResult result = new BenchmarkResult
        {
            TestName = target.testName,
            MethodName = target.methodName,
            TotalSimulationMilliseconds = profiler.TotalSimulationMilliseconds,
            AverageMilliseconds = profiler.AverageMilliseconds,
            AverageRealFps = fpsSamples > 0 ? fpsSum / fpsSamples : 0,
            Samples = profiler.Samples
        };

        results.Add(result);

        Destroy(testInstance);

        yield return new WaitForSecondsRealtime(pauseBetweenTests);
    }

    private GameObject CreateTestInstance(BenchmarkTarget target)
    {
        Quaternion startRotation = Quaternion.Euler(target.startRotationEuler);

        GameObject instance = Instantiate(
            target.sphereObject,
            target.startPosition,
            startRotation
        );

        instance.name =
            target.sphereObject.name + "_BenchmarkInstance";

        instance.SetActive(true);

        Rigidbody rigidbodyComponent = instance.GetComponent<Rigidbody>();

        if (rigidbodyComponent != null)
        {
            rigidbodyComponent.linearVelocity = Vector3.zero;
            rigidbodyComponent.angularVelocity = Vector3.zero;
            rigidbodyComponent.Sleep();
            rigidbodyComponent.WakeUp();
        }

        return instance;
    }

    private void DisplayResults()
    {
        if (resultsPanel != null)
        {
            resultsPanel.SetActive(true);
        }

        ClearGeneratedRows();

        if (resultRowTemplate == null || resultsRowParent == null)
        {
            return;
        }

        resultRowTemplate.gameObject.SetActive(true);

        Vector3 templatePosition =
            resultRowTemplate.transform.localPosition;

        for (int i = 0; i < results.Count; i++)
        {
            BenchmarkResult result = results[i];

            BenchmarkResultRow row =
                Instantiate(resultRowTemplate, resultsRowParent);

            row.gameObject.SetActive(true);

            row.transform.localPosition =
                templatePosition +
                new Vector3(0f, -resultRowVerticalSpacing * (i + 1), 0f);

            row.SetData(
                result.TestName,
                ShortenMethodName(result.MethodName),
                result.TotalSimulationMilliseconds,
                result.AverageMilliseconds,
                result.AverageRealFps,
                result.Samples
            );

            generatedResultRows.Add(row.gameObject);
        }
    }

    private void ClearGeneratedRows()
    {
        foreach (GameObject row in generatedResultRows)
        {
            if (row != null)
            {
                Destroy(row);
            }
        }

        generatedResultRows.Clear();
    }

    private string ShortenMethodName(string methodName)
    {
        if (methodName.Contains("Position"))
        {
            return "PBD";
        }

        if (methodName.Contains("Mass"))
        {
            return "MSS";
        }

        if (methodName.Contains("Cluster"))
        {
            return "CSM";
        }

        return methodName;
    }

    private void DisableOriginalTargets()
    {
        foreach (BenchmarkTarget target in targets)
        {
            if (target != null && target.sphereObject != null)
            {
                target.sphereObject.SetActive(false);
            }
        }
    }

    public void ExitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
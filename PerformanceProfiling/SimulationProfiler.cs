using UnityEngine;

public class SimulationProfiler : MonoBehaviour
{
    public double TotalSimulationMilliseconds { get; private set; }
    public int Samples { get; private set; }

    public double AverageMilliseconds
    {
        get
        {
            if (Samples == 0)
            {
                return 0;
            }

            return TotalSimulationMilliseconds / Samples;
        }
    }

    public double EstimatedSimulationFps
    {
        get
        {
            if (AverageMilliseconds <= 0)
            {
                return 0;
            }

            return 1000.0 / AverageMilliseconds;
        }
    }

    public void Record(float milliseconds)
    {
        TotalSimulationMilliseconds += milliseconds;
        Samples++;
    }

    public void ResetStats()
    {
        TotalSimulationMilliseconds = 0;
        Samples = 0;
    }
}
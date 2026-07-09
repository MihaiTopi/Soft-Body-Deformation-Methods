using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#region Custom Editor

#if UNITY_EDITOR
[CustomEditor(typeof(SoftBodyPBD)), CanEditMultipleObjects]
public class SoftBodyPBDEditor : Editor
{
    SerializedProperty impactForceMultiplier,
        impactForceOffset,
        springForce,
        damping,
        minimumCollisionForce,
        lastCollisionForce;

    void OnEnable()
    {
        impactForceMultiplier = serializedObject.FindProperty("impactForceMultiplier");
        impactForceOffset = serializedObject.FindProperty("impactForceOffset");
        springForce = serializedObject.FindProperty("springForce");
        damping = serializedObject.FindProperty("damping");
        minimumCollisionForce = serializedObject.FindProperty("minimumCollisionForce");
        lastCollisionForce = serializedObject.FindProperty("lastCollisionForce");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUILayout.Slider(impactForceMultiplier, -2, 2);
        EditorGUILayout.Slider(impactForceOffset, 0, 1);
        EditorGUILayout.Slider(springForce, 0, 100);
        EditorGUILayout.Slider(damping, 0, 100);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Minimum Collision Force");
        minimumCollisionForce.floatValue = EditorGUILayout.FloatField(minimumCollisionForce.floatValue);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Last Collision Force");
        EditorGUILayout.LabelField(lastCollisionForce.floatValue.ToString());
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

#endregion

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshCollider)), AddComponentMenu("Physics/SoftBodyPBD")]
public class SoftBodyPBD : MonoBehaviour
{
    [Header("Physics Settings")] public float impactForceMultiplier = 0.1f;
    public float impactForceOffset = 0;
    public float springForce = 20f;
    public float damping = 1;
    public float minimumCollisionForce = 10;
    public float lastCollisionForce;

    [Header("Performance Profiling")] SimulationProfiler simulationProfiler;

    private struct SpringEdge
    {
        public int vA;
        public int vB;
        public float restLength;
    }

    Mesh filterMesh;
    Vector3[] filterOriginalVertices, filterDisplacedVertices, filterVertexVelocities, filterPredicted;
    SpringEdge[] filterEdges;
    int[] filterVertexMap;

    float uniformScale = 1f;
    int solverIterations = 3;

    void Start()
    {
        filterMesh = GetComponent<MeshFilter>().mesh;
        filterOriginalVertices = filterMesh.vertices;
        filterDisplacedVertices = new Vector3[filterOriginalVertices.Length];
        filterPredicted = new Vector3[filterOriginalVertices.Length];
        filterVertexVelocities = new Vector3[filterOriginalVertices.Length];

        simulationProfiler = GetComponent<SimulationProfiler>();

        if (simulationProfiler == null)
        {
            simulationProfiler = gameObject.AddComponent<SimulationProfiler>();
        }

        for (int i = 0; i < filterOriginalVertices.Length; i++)
        {
            filterDisplacedVertices[i] = filterOriginalVertices[i];
        }

        filterVertexMap = BuildVertexMap(filterOriginalVertices);

        filterEdges = ExtractEdges(filterMesh.triangles, filterOriginalVertices);
    }

    void FixedUpdate()
    {
        float benchmarkStartTime = Time.realtimeSinceStartup;
        uniformScale = transform.localScale.x;

        SimulatePBD(filterOriginalVertices, filterDisplacedVertices, filterPredicted, filterVertexVelocities,
            filterEdges);

        for (int i = 0; i < filterDisplacedVertices.Length; i++)
        {
            if (filterVertexMap[i] != i)
            {
                filterDisplacedVertices[i] = filterDisplacedVertices[filterVertexMap[i]];
            }
        }

        filterMesh.vertices = filterDisplacedVertices;
        filterMesh.RecalculateNormals();

        float benchmarkElapsedMilliseconds =
            (Time.realtimeSinceStartup - benchmarkStartTime) * 1000f;

        simulationProfiler.Record(benchmarkElapsedMilliseconds);
    }

    void SimulatePBD(Vector3[] original, Vector3[] displaced, Vector3[] predicted, Vector3[] velocities,
        SpringEdge[] edges)
    {
        float dt = Time.fixedDeltaTime;
        float scaledDt = dt / uniformScale;

        for (int i = 0; i < displaced.Length; i++)
        {
            predicted[i] = displaced[i] + velocities[i] * scaledDt;
        }

        for (int iter = 0; iter < solverIterations; iter++)
        {
            for (int i = 0; i < edges.Length; i++)
            {
                int vA = edges[i].vA;
                int vB = edges[i].vB;
                Vector3 delta = predicted[vB] - predicted[vA];
                float currentDist = delta.magnitude;

                if (currentDist > 0.0001f)
                {
                    float error = currentDist - edges[i].restLength;
                    Vector3 correction = delta.normalized * (error * 0.5f);

                    predicted[vA] += correction;
                    predicted[vB] -= correction;
                }
            }
        }

        float stiffness = Mathf.Clamp01(springForce * dt);
        float friction = 1f - Mathf.Clamp01(damping * dt);

        for (int i = 0; i < displaced.Length; i++)
        {
            predicted[i] = Vector3.Lerp(predicted[i], original[i], stiffness);
            velocities[i] = (predicted[i] - displaced[i]) / scaledDt;
            velocities[i] *= friction;
            displaced[i] = predicted[i];
        }
    }

    int[] BuildVertexMap(Vector3[] vertices)
    {
        int[] map = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            map[i] = i;
            for (int j = 0; j < i; j++)
            {
                if (Vector3.Distance(vertices[i], vertices[j]) < 0.0001f)
                {
                    map[i] = j;
                    break;
                }
            }
        }

        return map;
    }

    SpringEdge[] ExtractEdges(int[] triangles, Vector3[] vertices)
    {
        List<SpringEdge> edgeList = new List<SpringEdge>();
        HashSet<string> existingEdges = new HashSet<string>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            AddEdge(triangles[i], triangles[i + 1], vertices, edgeList, existingEdges);
            AddEdge(triangles[i + 1], triangles[i + 2], vertices, edgeList, existingEdges);
            AddEdge(triangles[i + 2], triangles[i], vertices, edgeList, existingEdges);
        }

        return edgeList.ToArray();
    }

    void AddEdge(int v1, int v2, Vector3[] vertices, List<SpringEdge> edgeList, HashSet<string> existingEdges)
    {
        int min = Mathf.Min(v1, v2);
        int max = Mathf.Max(v1, v2);
        string key = min + "_" + max;

        if (!existingEdges.Contains(key))
        {
            existingEdges.Add(key);
            float dist = Vector3.Distance(vertices[v1], vertices[v2]);
            edgeList.Add(new SpringEdge { vA = min, vB = max, restLength = dist });
        }
    }

    public void AddDeformingForce(Vector3 point, float force)
    {
        point = transform.InverseTransformPoint(point);
        for (int i = 0; i < filterDisplacedVertices.Length; i++)
        {
            Vector3 ptToVert = filterDisplacedVertices[i] - point;
            ptToVert *= uniformScale;
            float attenForce = force / (1f + ptToVert.sqrMagnitude);
            filterVertexVelocities[i] += ptToVert.normalized * (attenForce * Time.fixedDeltaTime);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        float collisionForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        lastCollisionForce = collisionForce;

        if (collisionForce > minimumCollisionForce)
        {
            foreach (ContactPoint cp in collision.contacts)
            {
                Vector3 point = cp.point;
                point += cp.normal * impactForceOffset;
                AddDeformingForce(point, -impactForceMultiplier * collisionForce / collision.contactCount);
            }
        }
    }
}
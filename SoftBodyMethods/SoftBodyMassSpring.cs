using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#region Custom Editor

#if UNITY_EDITOR
[CustomEditor(typeof(SoftBodyMassSpring)), CanEditMultipleObjects]
public class SoftBodyMassSpringEditor : Editor
{
    private SerializedProperty impactForceMultiplier,
        impactForceOffset,
        impactRadius,
        springForce,
        shapeStiffness,
        damping,
        vertexMass,
        useVelocityClamp,
        maxVertexVelocity,
        minimumCollisionForce,
        lastCollisionForce;

    void OnEnable()
    {
        impactForceMultiplier = serializedObject.FindProperty("impactForceMultiplier");
        impactForceOffset = serializedObject.FindProperty("impactForceOffset");
        impactRadius = serializedObject.FindProperty("impactRadius");
        springForce = serializedObject.FindProperty("springForce");
        shapeStiffness = serializedObject.FindProperty("shapeStiffness");
        damping = serializedObject.FindProperty("damping");
        vertexMass = serializedObject.FindProperty("vertexMass");
        useVelocityClamp = serializedObject.FindProperty("useVelocityClamp");
        maxVertexVelocity = serializedObject.FindProperty("maxVertexVelocity");
        minimumCollisionForce = serializedObject.FindProperty("minimumCollisionForce");
        lastCollisionForce = serializedObject.FindProperty("lastCollisionForce");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Mass-Spring Settings", EditorStyles.boldLabel);
        EditorGUILayout.Slider(impactForceMultiplier, -2, 2);
        EditorGUILayout.Slider(impactForceOffset, 0, 1);
        EditorGUILayout.Slider(impactRadius, 0.01f, 2f);
        EditorGUILayout.Slider(springForce, 0, 50);
        EditorGUILayout.Slider(shapeStiffness, 0, 50);
        EditorGUILayout.Slider(damping, 0, 20);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stability Hacks", EditorStyles.boldLabel);
        EditorGUILayout.Slider(vertexMass, 0.05f, 2f);
        EditorGUILayout.PropertyField(useVelocityClamp);
        if (useVelocityClamp.boolValue) EditorGUILayout.Slider(maxVertexVelocity, 5f, 100f);

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

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshCollider)),
 AddComponentMenu("Physics/SoftBodyMassSpring")]
public class SoftBodyMassSpring : MonoBehaviour
{
    [Header("Physics Settings")] public float impactForceMultiplier = 0.5f;
    public float impactForceOffset = 0;
    public float impactRadius = 0.5f;
    public float springForce = 15f;
    public float shapeStiffness = 5f;
    public float damping = 2f;

    [Header("Stability Hacks")] public float vertexMass = 0.2f;
    public bool useVelocityClamp = false;
    public float maxVertexVelocity = 30f;

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
    Vector3[] filterOriginalVertices, filterDisplacedVertices, filterVertexVelocities, filterForces;
    SpringEdge[] filterEdges;
    int[] filterVertexMap;

    float uniformScale = 1f;

    void Start()
    {
        filterMesh = GetComponent<MeshFilter>().mesh;
        filterOriginalVertices = filterMesh.vertices;
        filterDisplacedVertices = new Vector3[filterOriginalVertices.Length];
        filterForces = new Vector3[filterOriginalVertices.Length];
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
        if (uniformScale <= 0.001f) uniformScale = 1f;

        SimulateMassSpring(filterOriginalVertices, filterDisplacedVertices, filterForces,
            filterVertexVelocities, filterEdges);

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

    void SimulateMassSpring(Vector3[] original, Vector3[] displaced, Vector3[] forces,
        Vector3[] velocities, SpringEdge[] edges)
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0.0001f) return;

        float scaledDt = dt / uniformScale;

        float k_edge = springForce * 20f;
        float k_home = shapeStiffness * 10f;
        float damp = damping * 0.1f;

        for (int i = 0; i < forces.Length; i++)
        {
            forces[i] = Vector3.zero;
        }

        for (int i = 0; i < edges.Length; i++)
        {
            int vA = edges[i].vA;
            int vB = edges[i].vB;

            Vector3 delta = displaced[vB] - displaced[vA];
            float currentDist = delta.magnitude;

            if (currentDist > 0.0001f)
            {
                float error = currentDist - edges[i].restLength;
                Vector3 dir = delta / currentDist;

                Vector3 springF = dir * (error * k_edge);
                Vector3 relVel = velocities[vB] - velocities[vA];
                Vector3 dampF = dir * (Vector3.Dot(relVel, dir) * damp);

                Vector3 totalForce = springF + dampF;

                forces[vA] += totalForce;
                forces[vB] -= totalForce;
            }
        }

        float friction = 1f - Mathf.Clamp01((damping * 0.5f) * dt);

        for (int i = 0; i < displaced.Length; i++)
        {
            Vector3 homeDelta = displaced[i] - original[i];
            forces[i] -= homeDelta * k_home;

            Vector3 acceleration = forces[i] / vertexMass;
            velocities[i] += acceleration * scaledDt;
            if (useVelocityClamp)
            {
                velocities[i] = Vector3.ClampMagnitude(
                    velocities[i],
                    maxVertexVelocity
                );
            }

            velocities[i] *= friction;
            displaced[i] += velocities[i] * scaledDt;
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
            float distance = ptToVert.magnitude;

            if (distance < impactRadius)
            {
                float normalizedDist = distance / impactRadius;
                float falloff = 1f - (normalizedDist * normalizedDist);

                Vector3 impulse = ptToVert.normalized * (force * falloff / vertexMass);
                filterVertexVelocities[i] += impulse * Time.fixedDeltaTime;
            }
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
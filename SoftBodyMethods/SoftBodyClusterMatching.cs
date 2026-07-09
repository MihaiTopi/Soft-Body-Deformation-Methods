using UnityEngine;
using System.Collections.Generic;

#region Custom Editor

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(SoftBodyClusterMatching)), CanEditMultipleObjects]
public class SoftBodyClusterMatchingEditor : Editor
{
    SerializedProperty impactForceMultiplier,
        impactForceOffset,
        shapeStiffness,
        anchorStiffness,
        damping,
        minimumCollisionForce,
        clusterRadius;

    void OnEnable()
    {
        impactForceMultiplier = serializedObject.FindProperty("impactForceMultiplier");
        impactForceOffset = serializedObject.FindProperty("impactForceOffset");
        shapeStiffness = serializedObject.FindProperty("shapeStiffness");
        anchorStiffness = serializedObject.FindProperty("anchorStiffness");
        damping = serializedObject.FindProperty("damping");
        minimumCollisionForce = serializedObject.FindProperty("minimumCollisionForce");
        clusterRadius = serializedObject.FindProperty("clusterRadius");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Cluster Matching Settings", EditorStyles.boldLabel);
        EditorGUILayout.Slider(impactForceMultiplier, 0, 5);
        EditorGUILayout.Slider(impactForceOffset, 0, 1);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Algorithm Tuning", EditorStyles.boldLabel);
        EditorGUILayout.Slider(clusterRadius, 0.1f, 5f, "Cluster Radius (Overlap)");
        EditorGUILayout.Slider(shapeStiffness, 0, 100, "Shape Snap (Rigidity)");
        EditorGUILayout.Slider(anchorStiffness, 0, 100, "Base Anchor Strength");
        EditorGUILayout.Slider(damping, 0, 100, "Velocity Damping");

        EditorGUILayout.PropertyField(minimumCollisionForce);
        serializedObject.ApplyModifiedProperties();
    }
}
#endif

#endregion

[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshCollider)),
 AddComponentMenu("Physics/SoftBodyClusterMatching")]
public class SoftBodyClusterMatching : MonoBehaviour
{
    [Header("Physics Settings")] public float impactForceMultiplier = 0.5f;
    public float impactForceOffset = 0;

    [Header("Cluster Settings")] public float clusterRadius = 0.5f;
    public float shapeStiffness = 30f;
    public float anchorStiffness = 5f;
    public float damping = 2;
    public float minimumCollisionForce = 1;

    [Header("Performance Profiling")] SimulationProfiler simulationProfiler;

    private struct Cluster
    {
        public int[] indices;
        public Vector3 restCenter;
        public Vector3[] restRelative;
    }

    Mesh filterMesh;
    Vector3[] filterOriginalVertices, filterDisplacedVertices, filterVertexVelocities, filterPredicted;
    int[] filterVertexMap;
    Cluster[] filterClusters;

    float uniformScale = 1f;

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
        int[] filterUnique = ExtractUniqueVertices(filterVertexMap);

        filterClusters = BuildClusters(filterOriginalVertices, filterUnique, clusterRadius);
    }

    void FixedUpdate()
    {
        float benchmarkStartTime = Time.realtimeSinceStartup;
        uniformScale = transform.localScale.x;

        SimulateClusterMatching(filterOriginalVertices, filterDisplacedVertices, filterPredicted,
            filterVertexVelocities, filterClusters);

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

    Cluster[] BuildClusters(Vector3[] originalVerts, int[] uniqueVerts, float radius)
    {
        List<Cluster> clusterList = new List<Cluster>();

        foreach (int i in uniqueVerts)
        {
            List<int> neighbors = new List<int>();
            foreach (int j in uniqueVerts)
            {
                if (Vector3.Distance(originalVerts[i], originalVerts[j]) <= radius)
                {
                    neighbors.Add(j);
                }
            }

            if (neighbors.Count > 3)
            {
                Cluster c = new Cluster();
                c.indices = neighbors.ToArray();

                c.restCenter = Vector3.zero;
                foreach (int n in c.indices) c.restCenter += originalVerts[n];
                c.restCenter /= c.indices.Length;

                c.restRelative = new Vector3[c.indices.Length];
                for (int k = 0; k < c.indices.Length; k++)
                {
                    c.restRelative[k] = originalVerts[c.indices[k]] - c.restCenter;
                }

                clusterList.Add(c);
            }
        }

        return clusterList.ToArray();
    }

    void SimulateClusterMatching(Vector3[] original, Vector3[] displaced, Vector3[] predicted,
        Vector3[] velocities, Cluster[] clusters)
    {
        float dt = Time.fixedDeltaTime;
        if (dt == 0) return;
        float scaledDt = dt / uniformScale;

        for (int i = 0; i < displaced.Length; i++)
        {
            predicted[i] = displaced[i] + velocities[i] * scaledDt;
        }

        Vector3[] sumGoals = new Vector3[displaced.Length];
        int[] clusterCounts = new int[displaced.Length];

        for (int c = 0; c < clusters.Length; c++)
        {
            Cluster cl = clusters[c];

            Vector3 currentCenter = Vector3.zero;
            for (int i = 0; i < cl.indices.Length; i++)
            {
                currentCenter += predicted[cl.indices[i]];
            }

            currentCenter /= cl.indices.Length;

            Matrix4x4 A = Matrix4x4.zero;
            for (int i = 0; i < cl.indices.Length; i++)
            {
                int vIdx = cl.indices[i];
                Vector3 p = predicted[vIdx] - currentCenter;
                Vector3 q = cl.restRelative[i];

                A.m00 += p.x * q.x;
                A.m01 += p.x * q.y;
                A.m02 += p.x * q.z;
                A.m10 += p.y * q.x;
                A.m11 += p.y * q.y;
                A.m12 += p.y * q.z;
                A.m20 += p.z * q.x;
                A.m21 += p.z * q.y;
                A.m22 += p.z * q.z;
            }

            Vector3 forwardAxis = new Vector3(A.m02, A.m12, A.m22);
            Vector3 upAxis = new Vector3(A.m01, A.m11, A.m21);
            Matrix4x4 R = Matrix4x4.identity;

            if (forwardAxis.sqrMagnitude > 0.001f && upAxis.sqrMagnitude > 0.001f)
            {
                R = Matrix4x4.Rotate(Quaternion.LookRotation(forwardAxis, upAxis));
            }

            for (int i = 0; i < cl.indices.Length; i++)
            {
                int vIdx = cl.indices[i];
                Vector3 localGoal = currentCenter + R.MultiplyVector(cl.restRelative[i]);
                sumGoals[vIdx] += localGoal;
                clusterCounts[vIdx]++;
            }
        }

        float stiffness = Mathf.Clamp01(shapeStiffness * dt);
        float tetherStiffness = Mathf.Clamp01(anchorStiffness * dt);

        float skeletonStiffness = Mathf.Clamp01((shapeStiffness * 0.2f) * dt);

        for (int i = 0; i < displaced.Length; i++)
        {
            if (clusterCounts[i] > 0)
            {
                Vector3 averageGoal = sumGoals[i] / clusterCounts[i];
                predicted[i] = Vector3.Lerp(predicted[i], averageGoal, stiffness);
            }

            predicted[i] = Vector3.Lerp(predicted[i], original[i], skeletonStiffness);

            predicted[i] = Vector3.Lerp(predicted[i], original[i], tetherStiffness);
        }

        float friction = 1f - Mathf.Clamp01(damping * dt);
        for (int i = 0; i < displaced.Length; i++)
        {
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

    int[] ExtractUniqueVertices(int[] map)
    {
        List<int> unique = new List<int>();
        for (int i = 0; i < map.Length; i++)
        {
            if (map[i] == i) unique.Add(i);
        }

        return unique.ToArray();
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

        if (collisionForce > minimumCollisionForce)
        {
            foreach (ContactPoint cp in collision.contacts)
            {
                Vector3 point = cp.point;
                point += cp.normal * impactForceOffset;
                AddDeformingForce(point, impactForceMultiplier * collisionForce / collision.contactCount);
            }
        }
    }
}
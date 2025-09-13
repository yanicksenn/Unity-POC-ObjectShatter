using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent,
 RequireComponent(typeof(MeshFilter)), 
 RequireComponent(typeof(MeshRenderer)), 
 RequireComponent(typeof(Rigidbody))]
public class Destructible : MonoBehaviour {
    [FormerlySerializedAs("CutCascades")] public int cutCascades = 1;
    [FormerlySerializedAs("ExplodeForce")] public float explodeForce = 0;
    
    private bool _edgeSet = false;
    private Vector3 _edgeVertex = Vector3.zero;
    private Vector2 _edgeUV = Vector2.zero;
    private Plane _edgePlane = new Plane();
    
    private void Update() {
        if (Input.GetMouseButtonDown(0)) {
            // Debug.Log(GetComponent<MeshFilter>().mesh.GetVolume());
            DestroyMesh();
        }
    }

    private void DestroyMesh() {
        var originalMesh = GetComponent<MeshFilter>().mesh;
        originalMesh.RecalculateBounds();

        var mainPart = new PartMesh() {
            UV = originalMesh.uv,
            Vertices = originalMesh.vertices,
            Normals = originalMesh.normals,
            Triangles = new int[originalMesh.subMeshCount][],
            Bounds = originalMesh.bounds
        };

        for (var i = 0; i < originalMesh.subMeshCount; i++)
            mainPart.Triangles[i] = originalMesh.GetTriangles(i);

        var parts = new List<PartMesh> { mainPart };
        var subParts = new List<PartMesh>();
        for (var c = 0; c < cutCascades; c++) {
            for (var i = 0; i < parts.Count; i++) {
                var bounds = parts[i].Bounds;
                bounds.Expand(0.5f);
                var plane = new Plane(Random.onUnitSphere, new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)));

                subParts.Add(GenerateMesh(parts[i], plane, true));
                subParts.Add(GenerateMesh(parts[i], plane, false));
            }
            parts = new List<PartMesh>(subParts);
            subParts.Clear();
        }

        for (var i = 0; i < parts.Count; i++) {
            parts[i].MakeGameObject(this);
            parts[i].GameObject.GetComponent<Rigidbody>()
                .AddForceAtPosition(parts[i].Bounds.center * explodeForce, transform.position);
        }

        Destroy(gameObject);
    }

    private PartMesh GenerateMesh(PartMesh original, Plane plane, bool left) {
        var partMesh = new PartMesh() { };
        var ray1 = new Ray();
        var ray2 = new Ray();
        
        for (var i = 0; i < original.Triangles.Length; i++) {
            var triangles = original.Triangles[i];
            _edgeSet = false;

            for (var j = 0; j < triangles.Length; j = j + 3) {
                var sideA = plane.GetSide(original.Vertices[triangles[j]]) == left;
                var sideB = plane.GetSide(original.Vertices[triangles[j + 1]]) == left;
                var sideC = plane.GetSide(original.Vertices[triangles[j + 2]]) == left;

                var sideCount = (sideA ? 1 : 0) +
                                (sideB ? 1 : 0) +
                                (sideC ? 1 : 0);
                if (sideCount == 0) {
                    continue;
                }

                if (sideCount == 3) {
                    partMesh.AddTriangle(i,
                        original.Vertices[triangles[j]], original.Vertices[triangles[j + 1]],
                        original.Vertices[triangles[j + 2]],
                        original.Normals[triangles[j]], original.Normals[triangles[j + 1]],
                        original.Normals[triangles[j + 2]],
                        original.UV[triangles[j]], original.UV[triangles[j + 1]], original.UV[triangles[j + 2]]);
                    continue;
                }

                //cut points
                var singleIndex = sideB == sideC ? 0 : sideA == sideC ? 1 : 2;

                ray1.origin = original.Vertices[triangles[j + singleIndex]];
                var dir1 = original.Vertices[triangles[j + ((singleIndex + 1) % 3)]] - original.Vertices[triangles[j + singleIndex]];
                ray1.direction = dir1;
                plane.Raycast(ray1, out var enter1);
                var lerp1 = enter1 / dir1.magnitude;

                ray2.origin = original.Vertices[triangles[j + singleIndex]];
                var dir2 = original.Vertices[triangles[j + ((singleIndex + 2) % 3)]] - original.Vertices[triangles[j + singleIndex]];
                ray2.direction = dir2;
                plane.Raycast(ray2, out var enter2);
                var lerp2 = enter2 / dir2.magnitude;

                //first vertex = ancor
                AddEdge(i,
                    partMesh,
                    left ? plane.normal * -1f : plane.normal,
                    ray1.origin + ray1.direction.normalized * enter1,
                    ray2.origin + ray2.direction.normalized * enter2,
                    Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                        original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                    Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                        original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));

                if (sideCount == 1) {
                    partMesh.AddTriangle(i,
                        original.Vertices[triangles[j + singleIndex]],
                        //Vector3.Lerp(originalMesh.vertices[triangles[j + singleIndex]], originalMesh.vertices[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        //Vector3.Lerp(originalMesh.vertices[triangles[j + singleIndex]], originalMesh.vertices[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
                        ray1.origin + ray1.direction.normalized * enter1,
                        ray2.origin + ray2.direction.normalized * enter2,
                        original.Normals[triangles[j + singleIndex]],
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
                        original.UV[triangles[j + singleIndex]],
                        Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));

                    continue;
                }

                if (sideCount == 2) {
                    partMesh.AddTriangle(i,
                        ray1.origin + ray1.direction.normalized * enter1,
                        original.Vertices[triangles[j + ((singleIndex + 1) % 3)]],
                        original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.Normals[triangles[j + ((singleIndex + 1) % 3)]],
                        original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.UV[triangles[j + ((singleIndex + 1) % 3)]],
                        original.UV[triangles[j + ((singleIndex + 2) % 3)]]);
                    partMesh.AddTriangle(i,
                        ray1.origin + ray1.direction.normalized * enter1,
                        original.Vertices[triangles[j + ((singleIndex + 2) % 3)]],
                        ray2.origin + ray2.direction.normalized * enter2,
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.Normals[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector3.Lerp(original.Normals[triangles[j + singleIndex]],
                            original.Normals[triangles[j + ((singleIndex + 2) % 3)]], lerp2),
                        Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                        original.UV[triangles[j + ((singleIndex + 2) % 3)]],
                        Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                            original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));
                    continue;
                }
            }
        }

        partMesh.FillArrays();
        return partMesh;
    }

    private void AddEdge(int subMesh, PartMesh partMesh, Vector3 normal, Vector3 vertex1, Vector3 vertex2, Vector2 uv1,
        Vector2 uv2) {
        if (!_edgeSet) {
            _edgeSet = true;
            _edgeVertex = vertex1;
            _edgeUV = uv1;
        } else {
            _edgePlane.Set3Points(_edgeVertex, vertex1, vertex2);

            partMesh.AddTriangle(subMesh,
                _edgeVertex,
                _edgePlane.GetSide(_edgeVertex + normal) ? vertex1 : vertex2,
                _edgePlane.GetSide(_edgeVertex + normal) ? vertex2 : vertex1,
                normal,
                normal,
                normal,
                _edgeUV,
                uv1,
                uv2);
        }
    }

    private class PartMesh {
        private readonly List<Vector3> _verticies = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<List<int>> _triangles = new();
        private readonly List<Vector2> _uVs = new();

        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[][] Triangles;
        public Vector2[] UV;
        public GameObject GameObject;
        public Bounds Bounds;

        public void AddTriangle(int submesh, Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 normal1, Vector3 normal2, Vector3 normal3, Vector2 uv1, Vector2 uv2, Vector2 uv3) {
            if (_triangles.Count - 1 < submesh) {
                _triangles.Add(new List<int>());
            }

            _triangles[submesh].Add(_verticies.Count);
            _verticies.Add(vert1);
            _triangles[submesh].Add(_verticies.Count);
            _verticies.Add(vert2);
            _triangles[submesh].Add(_verticies.Count);
            _verticies.Add(vert3);
            _normals.Add(normal1);
            _normals.Add(normal2);
            _normals.Add(normal3);
            _uVs.Add(uv1);
            _uVs.Add(uv2);
            _uVs.Add(uv3);

            Bounds.min = Vector3.Min(Bounds.min, vert1);
            Bounds.min = Vector3.Min(Bounds.min, vert2);
            Bounds.min = Vector3.Min(Bounds.min, vert3);
            Bounds.max = Vector3.Min(Bounds.max, vert1);
            Bounds.max = Vector3.Min(Bounds.max, vert2);
            Bounds.max = Vector3.Min(Bounds.max, vert3);
        }

        public void FillArrays() {
            Vertices = _verticies.ToArray();
            Normals = _normals.ToArray();
            UV = _uVs.ToArray();
            Triangles = new int[_triangles.Count][];
            for (var i = 0; i < _triangles.Count; i++)
                Triangles[i] = _triangles[i].ToArray();
        }

        public void MakeGameObject(Destructible original) {
            GameObject = new GameObject(original.name) {
                transform = {
                    position = original.transform.position,
                    rotation = original.transform.rotation,
                    localScale = original.transform.localScale
                }
            };

            var mesh = new Mesh {
                name = original.GetComponent<MeshFilter>().mesh.name,
                vertices = Vertices,
                normals = Normals,
                uv = UV
            };

            for(var i = 0; i < Triangles.Length; i++)
                mesh.SetTriangles(Triangles[i], i, true);

            Bounds = mesh.bounds;
            
            var renderer = GameObject.AddComponent<MeshRenderer>();
            renderer.materials = original.GetComponent<MeshRenderer>().materials;

            var filter = GameObject.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            var collider = GameObject.AddComponent<MeshCollider>();
            collider.convex = true;

            GameObject.AddComponent<Rigidbody>();

            if (mesh.GetVolume() >= 0.2f) {
                var meshDestroy = GameObject.AddComponent<Destructible>();
                meshDestroy.cutCascades = original.cutCascades;
                meshDestroy.explodeForce = original.explodeForce;
            }
        }
    }
}
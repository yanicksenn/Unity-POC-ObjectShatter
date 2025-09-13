/// <summary>
/// A component that allows a GameObject's mesh to be shattered into multiple pieces.
/// This script requires a MeshFilter, MeshRenderer, and Rigidbody to be attached to the same GameObject.
/// </summary>
[DisallowMultipleComponent,
 RequireComponent(typeof(MeshFilter)), 
 RequireComponent(typeof(MeshRenderer)), 
 RequireComponent(typeof(Rigidbody))]
public class Destructible : MonoBehaviour {
    
    /// <summary>
    /// The number of times the mesh should be recursively cut. More cascades result in more, smaller pieces.
    /// </summary>
    [Tooltip("The number of times the mesh should be recursively cut. More cascades result in more, smaller pieces.")]
    [FormerlySerializedAs("CutCascades")] public int cutCascades = 1;
    
    /// <summary>
    /// The force applied to the center of each new piece, pushing it away from the original object's position.
    /// </summary>
    [Tooltip("The force applied to the center of each new piece, pushing it away from the original object's position.")]
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

    /// <summary>
    /// Initiates the mesh destruction process. This method will slice the original mesh into smaller parts
    /// based on the cutCascades and then create new GameObjects for each part.
    /// </summary>
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
        
        // Recursively cut the mesh for each cascade level.
        for (var c = 0; c < cutCascades; c++) {
            for (var i = 0; i < parts.Count; i++) {
                var bounds = parts[i].Bounds;
                bounds.Expand(0.5f);
                
                // Create a random cutting plane within the bounds of the mesh part.
                var plane = new Plane(Random.onUnitSphere, new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z)));

                // Generate two new meshes, one for each side of the cutting plane.
                subParts.Add(GenerateMesh(parts[i], plane, true));
                subParts.Add(GenerateMesh(parts[i], plane, false));
            }
            parts = new List<PartMesh>(subParts);
            subParts.Clear();
        }

        // Create GameObjects for each of the final mesh parts.
        for (var i = 0; i < parts.Count; i++) {
            parts[i].MakeGameObject(this);
            parts[i].GameObject.GetComponent<Rigidbody>()
                .AddForceAtPosition(parts[i].Bounds.center * explodeForce, transform.position);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Generates a new mesh part by slicing the original mesh with a plane.
    /// </summary>
    /// <param name="original">The original PartMesh to be sliced.</param>
    /// <param name="plane">The plane used to slice the mesh.</param>
    /// <param name="left">A boolean indicating which side of the plane to generate the mesh for.</param>
    /// <returns>A new PartMesh representing one side of the sliced original.</returns>
    private PartMesh GenerateMesh(PartMesh original, Plane plane, bool left) {
        var partMesh = new PartMesh() { };
        var ray1 = new Ray();
        var ray2 = new Ray();
        
        for (var i = 0; i < original.Triangles.Length; i++) {
            var triangles = original.Triangles[i];
            _edgeSet = false;

            for (var j = 0; j < triangles.Length; j = j + 3) {
                // Determine which side of the cutting plane each vertex of the triangle is on.
                var sideA = plane.GetSide(original.Vertices[triangles[j]]) == left;
                var sideB = plane.GetSide(original.Vertices[triangles[j + 1]]) == left;
                var sideC = plane.GetSide(original.Vertices[triangles[j + 2]]) == left;

                var sideCount = (sideA ? 1 : 0) +
                                (sideB ? 1 : 0) +
                                (sideC ? 1 : 0);
                
                // If all vertices are on the wrong side of the plane, the triangle is discarded.
                if (sideCount == 0) {
                    continue;
                }

                // If all vertices are on the correct side, the triangle is kept as is.
                if (sideCount == 3) {
                    partMesh.AddTriangle(i,
                        original.Vertices[triangles[j]], original.Vertices[triangles[j + 1]],
                        original.Vertices[triangles[j + 2]],
                        original.Normals[triangles[j]], original.Normals[triangles[j + 1]],
                        original.Normals[triangles[j + 2]],
                        original.UV[triangles[j]], original.UV[triangles[j + 1]], original.UV[triangles[j + 2]]);
                    continue;
                }

                // The triangle is being cut by the plane.
                // We need to find the intersection points and create new triangles.
                var singleIndex = sideB == sideC ? 0 : sideA == sideC ? 1 : 2;

                // Raycast from the single vertex to the other two to find intersection points with the plane.
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

                // Add a new triangle to "cap" the cut surface.
                AddEdge(i,
                    partMesh,
                    left ? plane.normal * -1f : plane.normal,
                    ray1.origin + ray1.direction.normalized * enter1,
                    ray2.origin + ray2.direction.normalized * enter2,
                    Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                        original.UV[triangles[j + ((singleIndex + 1) % 3)]], lerp1),
                    Vector2.Lerp(original.UV[triangles[j + singleIndex]],
                        original.UV[triangles[j + ((singleIndex + 2) % 3)]], lerp2));

                // If only one vertex is on the correct side, a single new triangle is formed.
                if (sideCount == 1) {
                    partMesh.AddTriangle(i,
                        original.Vertices[triangles[j + singleIndex]],
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

                // If two vertices are on the correct side, two new triangles (a quad) are formed.
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

    /// <summary>
    /// Adds a new triangle to fill the hole created by the cutting plane.
    /// This creates a "cap" on the cut surface.
    /// </summary>
    private void AddEdge(int subMesh, PartMesh partMesh, Vector3 normal, Vector3 vertex1, Vector3 vertex2, Vector2 uv1,
        Vector2 uv2) {
        if (!_edgeSet) {
            _edgeSet = true;
            _edgeVertex = vertex1;
            _edgeUV = uv1;
        } else {
            // Creates a triangle connecting the new edge vertices to form the cap.
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

    /// <summary>
    /// A helper class to store mesh data for a single part of the destructible object.
    /// It also handles the creation of the final GameObject.
    /// </summary>
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

        /// <summary>
        /// Adds a triangle to the mesh data.
        /// </summary>
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

        /// <summary>
        /// Converts the internal lists of mesh data to arrays.
        /// </summary>
        public void FillArrays() {
            Vertices = _verticies.ToArray();
            Normals = _normals.ToArray();
            UV = _uVs.ToArray();
            Triangles = new int[_triangles.Count][];
            for (var i = 0; i < _triangles.Count; i++)
                Triangles[i] = _triangles[i].ToArray();
        }

        /// <summary>
        /// Creates a new GameObject from the mesh data.
        /// </summary>
        /// <param name="original">The original Destructible object, used for position, rotation, and material.</param>
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

            // If the new mesh part is large enough, allow it to be destructible as well.
            if (mesh.GetVolume() >= 0.2f) {
                var meshDestroy = GameObject.AddComponent<Destructible>();
                meshDestroy.cutCascades = original.cutCascades;
                meshDestroy.explodeForce = original.explodeForce;
            }
        }
    }
}
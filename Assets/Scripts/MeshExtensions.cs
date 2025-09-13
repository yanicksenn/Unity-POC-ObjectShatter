using UnityEngine;

public static class MeshExtensions {
    public static float GetVolume(this Mesh mesh) {
        if (mesh == null) {
            Debug.LogError("Mesh is null!");
            return 0f;
        }

        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var volume = 0f;

        // Iterate over all triangles in the mesh
        for (var i = 0; i < triangles.Length; i += 3) {
            var p1 = vertices[triangles[i + 0]];
            var p2 = vertices[triangles[i + 1]];
            var p3 = vertices[triangles[i + 2]];
            
            // Calculate the signed volume of the tetrahedron formed by the triangle and the origin
            volume += SignedVolumeOfTetrahedron(p1, p2, p3);
        }
        
        // The absolute value is taken because the signed volume can be negative
        // depending on the triangle winding order.
        return Mathf.Abs(volume);
    }

    private static float SignedVolumeOfTetrahedron(Vector3 p1, Vector3 p2, Vector3 p3) {
        // The formula for the signed volume of a tetrahedron is (v1 dot (v2 cross v3)) / 6
        return Vector3.Dot(p1, Vector3.Cross(p2, p3)) / 6.0f;
    }
}
using System;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class AutoSpawn : MonoBehaviour {
    
    [SerializeField] private Destructible destructiblePrefab;

    [SerializeField] private float minSpawnY;
    [SerializeField] private float maxSpawnY;

    [SerializeField] private float minSpawnX;
    [SerializeField] private float maxSpawnX;
    
    [SerializeField] private float minSizeMultiplier;
    [SerializeField] private float maxSizeMultiplier;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update() {
        if (Input.GetMouseButtonDown(0)) {
            var destructible = Instantiate(destructiblePrefab,
                new Vector3(Random.RandomRange(minSpawnX, maxSpawnX), Random.RandomRange(minSpawnY, maxSpawnY), 0f),
                transform.rotation);
            var scale = Random.Range(minSizeMultiplier, maxSizeMultiplier);
            destructible.transform.localScale *= scale;
            destructible.GetComponent<Rigidbody>().mass = 
                destructible.GetComponent<MeshFilter>().mesh.GetVolume() * scale;
        }
    }
}
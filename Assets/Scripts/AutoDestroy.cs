using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class AutoDestroy : MonoBehaviour {
    public float destroyAfterSeconds;

    private void Start() {
        StartCoroutine(DestroyAfterSeconds());
    }

    private IEnumerator DestroyAfterSeconds() {
        yield return new WaitForSeconds(destroyAfterSeconds * Random.Range(0.5f, 4f));
        Destroy(gameObject);
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.Animations;

public class FloatingWorldText : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;
    [SerializeField] private RotationConstraint rotationConstraint;
    [SerializeField] private float lifetime = 0.9f; //seconds
    [SerializeField] private float riseSpeed = 1.2f; //units per second

    private Color baseColor;
    private float elapsed;

    public void Initialize(string message, Color color, Transform rotationAnchor)
    {
        transform.rotation = Quaternion.identity;

        ConstraintSource rotationSource = new ConstraintSource();
        rotationSource.sourceTransform = rotationAnchor;
        rotationSource.weight = 1f;
        rotationConstraint.SetSource(0,rotationSource);
        
        textMesh.text = message;
        baseColor = color;
        textMesh.color = baseColor;
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);

        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

        Color currentColor = baseColor;
        currentColor.a = 1f - t;
        textMesh.color = currentColor;

        if (t >= 1f)
            Destroy(gameObject);
    }
}
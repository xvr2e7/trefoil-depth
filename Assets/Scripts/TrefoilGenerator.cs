using UnityEngine;

public class TrefoilGenerator : MonoBehaviour
{
    [Header("Trefoil Parameters")]
    public float R1 = 1.0f;
    public float R2 = 1.5f;
    public float width = 0.02f;
    public int segments = 1000;

    [Header("Rotation")]
    public float rotationSpeed = 90f;
    public int direction = 1;

    private LineRenderer lineRenderer;
    private float currentAngle = 0f;

    void Start()
    {
        gameObject.layer = LayerMask.NameToLayer("RightEyeOnly");

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = segments;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.black;
        lineRenderer.endColor = Color.black;

        GenerateTrefoil();
    }

    void Update()
    {
        currentAngle += rotationSpeed * Time.deltaTime * direction;
        transform.localRotation = Quaternion.Euler(0, 0, currentAngle);
    }

    void GenerateTrefoil()
    {
        for (int i = 0; i < segments; i++)
        {
            float phi = i * 2 * Mathf.PI / (segments - 1);
            float x = R1 * Mathf.Cos(phi) + R2 * Mathf.Cos(2 * phi);
            float y = R1 * Mathf.Sin(phi) - R2 * Mathf.Sin(2 * phi);
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    public void ResetRotation()
    {
        currentAngle = 0f;
        transform.localRotation = Quaternion.identity;
    }

    public void SetParameters(float r1, float r2, float speed, int dir)
    {
        R1 = r1;
        R2 = r2;
        rotationSpeed = speed;
        direction = dir;
        GenerateTrefoil();
        ResetRotation();
    }

    public void SetVisibility(bool visible)
    {
        lineRenderer.enabled = visible;
    }
}
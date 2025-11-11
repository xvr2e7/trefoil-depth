using UnityEngine;

public class Trefoil3D : MonoBehaviour
{
    [Header("Trefoil Parameters")]
    public int segments = 1000;

    [Tooltip("Line width/thickness")]
    public float width = 0.12f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 60f;

    [Tooltip("1 for CCW, -1 for CW")]
    public int direction = 1;

    [Header("Appearance")]
    public Color lineColor = Color.black;

    private LineRenderer lineRenderer;
    private float currentAngle = 0f;

    void Start()
    {
        // Setup LineRenderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.positionCount = segments;

        // Set material and color
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        // Draw the 3D trefoil knot
        DrawTrefoilKnot();
    }

    void Update()
    {
        // Rotate the trefoil around the Z-axis
        currentAngle += rotationSpeed * Time.deltaTime * direction;
        transform.localRotation = Quaternion.Euler(0, 0, currentAngle);
    }

    void DrawTrefoilKnot()
    {
        // Trefoil knot parametric equations:
        // x(t) = sin(t) + 2*sin(2t)
        // y(t) = cos(t) - 2*cos(2t)
        // z(t) = -sin(3t)

        float totalAngle = 2 * Mathf.PI; // Full rotation in radians

        for (int i = 0; i < segments; i++)
        {
            float t = (i / (float)(segments - 1)) * totalAngle;

            float x = Mathf.Sin(t) + 2 * Mathf.Sin(2 * t);
            float y = Mathf.Cos(t) - 2 * Mathf.Cos(2 * t);
            float z = -Mathf.Sin(3 * t);

            lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }

    public void ResetRotation()
    {
        currentAngle = 0f;
        transform.localRotation = Quaternion.identity;
    }

    public void SetDirection(int newDirection)
    {
        direction = newDirection;
    }
}
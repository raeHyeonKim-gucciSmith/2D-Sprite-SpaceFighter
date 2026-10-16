using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class BoosterFlameTriangle : MonoBehaviour
{
    [Header("Shape")]
    public float baseWidth = 0.12f;
    public float minLength = 0.12f;
    public float maxLength = 0.55f;

    [Header("Visual")]
    public Color lowThrustColor = new Color(0.1f, 0.65f, 1f, 0.55f);
    public Color highThrustColor = new Color(0.75f, 1f, 1f, 1f);
    public int sortingOrder = 5;

    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private Material runtimeMaterial;
    private Vector3[] vertices;

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    void OnValidate()
    {
        Initialize();
        SetThrust(1f, 1f);
    }

    void Initialize()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Booster Flame Triangle";
        }

        if (vertices == null || vertices.Length != 3)
            vertices = new Vector3[3];

        mesh.vertices = vertices;
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(1f, 0f)
        };

        GetComponent<MeshFilter>().mesh = mesh;

        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = sortingOrder;

        if (runtimeMaterial == null)
        {
            runtimeMaterial = meshRenderer.sharedMaterial != null
                ? meshRenderer.sharedMaterial
                : CreateDefaultMaterial();
        }

        meshRenderer.material = runtimeMaterial;

        SetThrust(0f, 1f);
    }

    Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material material = new Material(shader);
        material.color = lowThrustColor;
        material.renderQueue = 3000;
        return material;
    }

    public void SetThrust(float thrust01, float flicker = 1f)
    {
        thrust01 = Mathf.Clamp01(thrust01);

        float width = baseWidth * Mathf.Lerp(0.8f, 1.15f, thrust01) * flicker;
        float length = Mathf.Lerp(minLength, maxLength, thrust01) * flicker;

        vertices[0] = new Vector3(-width * 0.5f, 0f, 0f);
        vertices[1] = new Vector3(0f, -length, 0f);
        vertices[2] = new Vector3(width * 0.5f, 0f, 0f);

        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        if (runtimeMaterial != null)
        {
            Color flameColor = Color.Lerp(lowThrustColor, highThrustColor, thrust01);
            runtimeMaterial.color = flameColor;

            if (runtimeMaterial.HasProperty("_BaseColor"))
                runtimeMaterial.SetColor("_BaseColor", flameColor);

            if (runtimeMaterial.HasProperty("_Color"))
                runtimeMaterial.SetColor("_Color", flameColor);
        }
    }
}

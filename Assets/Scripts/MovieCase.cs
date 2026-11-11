using UnityEngine;

/// <summary>
/// Marks a single movie case in the store so the interaction system can
/// identify it, display its details and highlight it while focused.
/// </summary>
public class MovieCase : MonoBehaviour
{
    public string title = "UNTITLED";
    public int year = 1987;
    public string genre = "HORROR";
    public Color accent = new Color(1f, 0.78f, 0.35f);

    [HideInInspector] public MeshRenderer meshRenderer;
    [HideInInspector] public Material baseMaterial;

    public MeshRenderer Renderer
    {
        get
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            return meshRenderer;
        }
    }

    void Awake()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }
}

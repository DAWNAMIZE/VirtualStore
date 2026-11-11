using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Raycasts from the centre of the screen, highlights the movie case being
/// looked at and feeds its details to the HUD after a short dwell time.
///
/// Highlighting swaps the renderer to a shared emissive copy of its base
/// material. Copies are cached per base material (8 in total) so the runtime
/// never accumulates one material instance per movie case.
/// </summary>
public class StoreInteraction : MonoBehaviour
{
    public Camera viewCamera;
    public VirtualStoreHUD hud;
    public float distance = 3.6f;
    public float dwellTime = 0.3f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly Dictionary<Material, Material> HighlightCache = new Dictionary<Material, Material>();

    MovieCase current;
    float dwell;

    void Awake()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (hud == null) hud = GetComponent<VirtualStoreHUD>();
    }

    void Update()
    {
        if (viewCamera == null) return;

        MovieCase hitCase = null;

        if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward,
                out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            hitCase = hit.collider.GetComponentInParent<MovieCase>();
        }

        if (hitCase != current)
        {
            ClearHighlight(current);
            current = hitCase;
            dwell = 0f;
        }

        if (current != null)
        {
            dwell += Time.deltaTime;
            if (dwell > 0.08f)
                ApplyHighlight(current);
        }

        if (hud != null)
            hud.SetFocus(current, dwell / Mathf.Max(0.01f, dwellTime));
    }

    static Material GetHighlightMaterial(Material baseMaterial)
    {
        if (baseMaterial == null) return null;

        if (HighlightCache.TryGetValue(baseMaterial, out Material cached) && cached != null)
            return cached;

        var highlight = new Material(baseMaterial)
        {
            name = baseMaterial.name + "_Highlight"
        };
        highlight.EnableKeyword("_EMISSION");
        highlight.SetColor(EmissionColorId, new Color(1f, 0.84f, 0.55f) * 0.65f);
        HighlightCache[baseMaterial] = highlight;
        return highlight;
    }

    void ApplyHighlight(MovieCase movieCase)
    {
        var renderer = movieCase.Renderer;
        if (renderer == null) return;

        if (movieCase.baseMaterial == null)
            movieCase.baseMaterial = renderer.sharedMaterial;

        Material highlight = GetHighlightMaterial(movieCase.baseMaterial);
        if (highlight != null && renderer.sharedMaterial != highlight)
            renderer.sharedMaterial = highlight;
    }

    void ClearHighlight(MovieCase movieCase)
    {
        if (movieCase == null) return;

        var renderer = movieCase.Renderer;
        if (renderer == null || movieCase.baseMaterial == null) return;

        if (renderer.sharedMaterial != movieCase.baseMaterial)
            renderer.sharedMaterial = movieCase.baseMaterial;
    }
}

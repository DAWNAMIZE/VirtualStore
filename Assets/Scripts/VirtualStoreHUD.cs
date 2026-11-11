using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Builds and drives the in-game UI at runtime: crosshair, focus prompt,
/// control hints, start overlay and the mobile virtual joystick.
/// Using runtime-built uGUI keeps the scene light and avoids prefab churn.
/// </summary>
public class VirtualStoreHUD : MonoBehaviour
{
    public static VirtualStoreHUD Instance { get; private set; }

    public MobileJoystick Joystick { get; private set; }

    static Font cachedFont;

    public static Font UIFont
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return cachedFont;
        }
    }

    static Sprite circleSprite;
    static Sprite ringSprite;
    static Sprite roundedSprite;

    Canvas canvas;
    RectTransform joystickRoot;
    Image crosshairDot;
    Image crosshairRing;
    CanvasGroup promptGroup;
    Text promptTitle;
    Text promptMeta;
    CanvasGroup hintGroup;
    GameObject startOverlay;
    Text enterText;

    float promptTargetAlpha;
    float hintTimer = 11f;
    Color defaultAccent = new Color(1f, 0.78f, 0.35f);
    Color currentAccent;

    void Awake()
    {
        Instance = this;
        EnsureEventSystem();
        BuildUI();
    }

    void Update()
    {
        if (promptGroup != null)
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, promptTargetAlpha, Time.unscaledDeltaTime * 7f);

        if (hintGroup != null)
        {
            hintTimer -= Time.unscaledDeltaTime;
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, hintTimer > 0f ? 0.9f : 0f, Time.unscaledDeltaTime * 1.4f);
        }

        if (startOverlay != null && startOverlay.activeSelf)
        {
            if (enterText != null)
                enterText.color = new Color(1f, 0.82f, 0.45f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.2f));

            if (AnyInputPressed())
                startOverlay.SetActive(false);
        }

        if (joystickRoot != null && !joystickRoot.gameObject.activeSelf && FirstPersonController.AnyTouchPressed)
            joystickRoot.gameObject.SetActive(true);
    }

    /// <summary>Called every frame by the interaction system.</summary>
    public void SetFocus(MovieCase movieCase, float progress)
    {
        bool hasFocus = movieCase != null;
        promptTargetAlpha = hasFocus ? 1f : 0f;

        if (hasFocus)
        {
            if (promptTitle != null) promptTitle.text = movieCase.title;
            if (promptMeta != null) promptMeta.text = movieCase.genre + "   \u00B7   " + movieCase.year;

            currentAccent = movieCase.accent.a > 0.01f ? movieCase.accent : defaultAccent;

            if (crosshairDot != null)
                crosshairDot.color = Color.Lerp(Color.white, currentAccent, 0.85f);
            if (crosshairRing != null)
                crosshairRing.color = new Color(currentAccent.r, currentAccent.g, currentAccent.b, 0.9f);
        }
        else
        {
            if (crosshairDot != null)
                crosshairDot.color = new Color(1f, 1f, 1f, 0.8f);
            if (crosshairRing != null)
                crosshairRing.color = new Color(1f, 1f, 1f, 0.22f);
        }

        if (crosshairRing != null)
            crosshairRing.fillAmount = Mathf.Clamp01(progress);
    }

    public void ShowHint(string text, float seconds = 8f)
    {
        if (hintGroup == null) return;
        var hintText = hintGroup.GetComponent<Text>();
        if (hintText != null) hintText.text = text;
        hintTimer = seconds;
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("VirtualStoreUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildCrosshair();
        BuildPrompt();
        BuildHint();
        BuildBrand();
        BuildJoystick();
        BuildStartOverlay();
    }

    void BuildCrosshair()
    {
        var root = CreateRect("Crosshair", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));

        var ringRt = CreateRect("FocusRing", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
        crosshairRing = ringRt.gameObject.AddComponent<Image>();
        crosshairRing.sprite = GetRingSprite();
        crosshairRing.color = new Color(1f, 1f, 1f, 0.22f);
        crosshairRing.type = Image.Type.Filled;
        crosshairRing.fillMethod = Image.FillMethod.Radial360;
        crosshairRing.fillOrigin = (int)Image.Origin360.Top;
        crosshairRing.fillClockwise = true;
        crosshairRing.fillAmount = 0f;
        crosshairRing.raycastTarget = false;

        var dotRt = CreateRect("Dot", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        crosshairDot = dotRt.gameObject.AddComponent<Image>();
        crosshairDot.sprite = GetCircleSprite();
        crosshairDot.color = new Color(1f, 1f, 1f, 0.8f);
        crosshairDot.raycastTarget = false;
    }

    void BuildPrompt()
    {
        var panel = CreateRect("FocusPrompt", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(600f, 104f));

        var background = panel.gameObject.AddComponent<Image>();
        background.sprite = GetRoundedSprite();
        background.type = Image.Type.Sliced;
        background.color = new Color(0.02f, 0.02f, 0.03f, 0.74f);
        background.raycastTarget = false;

        promptGroup = panel.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.blocksRaycasts = false;
        promptGroup.interactable = false;

        var accentLine = CreateRect("Accent", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f));
        var accentImage = accentLine.gameObject.AddComponent<Image>();
        accentImage.color = new Color(1f, 0.62f, 0.2f, 0.9f);
        accentImage.raycastTarget = false;

        promptTitle = CreateText(panel, "Title", "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        var titleRt = promptTitle.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.4f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(16f, 0f);
        titleRt.offsetMax = new Vector2(-16f, -8f);

        promptMeta = CreateText(panel, "Meta", "", 17, new Color(0.78f, 0.78f, 0.8f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
        var metaRt = promptMeta.rectTransform;
        metaRt.anchorMin = new Vector2(0f, 0f);
        metaRt.anchorMax = new Vector2(1f, 0.42f);
        metaRt.offsetMin = new Vector2(16f, 8f);
        metaRt.offsetMax = new Vector2(-16f, 0f);
    }

    void BuildHint()
    {
        var hint = CreateText(canvas.transform, "Hint", "", 17, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter, FontStyle.Normal);
        var rt = hint.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -26f);
        rt.sizeDelta = new Vector2(900f, 34f);

        var shadow = hint.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;
        hintGroup.blocksRaycasts = false;

        hint.text = FirstPersonController.TouchCapable
            ? "LEFT STICK TO MOVE   \u00B7   DRAG TO LOOK"
            : "WASD / ARROWS TO MOVE   \u00B7   MOUSE TO LOOK   \u00B7   SHIFT TO RUN   \u00B7   ESC TO RELEASE CURSOR";
    }

    void BuildBrand()
    {
        var brand = CreateText(canvas.transform, "Brand", "NIGHTHAWKS VIDEO  \u00B7  AISLE 03", 16, new Color(1f, 0.85f, 0.6f, 0.4f), TextAnchor.UpperLeft, FontStyle.Bold);
        var rt = brand.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(28f, -24f);
        rt.sizeDelta = new Vector2(520f, 28f);
    }

    void BuildJoystick()
    {
        joystickRoot = CreateRect("Joystick", canvas.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(180f, 180f), new Vector2(270f, 270f));

        var background = joystickRoot.gameObject.AddComponent<Image>();
        background.sprite = GetCircleSprite();
        background.color = new Color(1f, 1f, 1f, 0.10f);
        background.raycastTarget = true;

        var outlineRt = CreateRect("Outline", joystickRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 230f));
        var outline = outlineRt.gameObject.AddComponent<Image>();
        outline.sprite = GetRingSprite();
        outline.color = new Color(1f, 1f, 1f, 0.22f);
        outline.raycastTarget = false;

        var knobRt = CreateRect("Knob", joystickRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(108f, 108f));
        var knob = knobRt.gameObject.AddComponent<Image>();
        knob.sprite = GetCircleSprite();
        knob.color = new Color(1f, 1f, 1f, 0.30f);
        knob.raycastTarget = false;

        var innerRt = CreateRect("KnobCore", knobRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
        var inner = innerRt.gameObject.AddComponent<Image>();
        inner.sprite = GetCircleSprite();
        inner.color = new Color(1f, 0.85f, 0.6f, 0.35f);
        inner.raycastTarget = false;

        Joystick = joystickRoot.gameObject.AddComponent<MobileJoystick>();
        Joystick.background = joystickRoot;
        Joystick.knob = knobRt;
        Joystick.radius = 92f;

        joystickRoot.gameObject.SetActive(FirstPersonController.TouchCapable);
    }

    void BuildStartOverlay()
    {
        var overlay = CreateRect("StartOverlay", canvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var background = overlay.gameObject.AddComponent<Image>();
        background.color = new Color(0.015f, 0.015f, 0.02f, 0.94f);
        background.raycastTarget = false;
        startOverlay = overlay.gameObject;

        var title = CreateText(overlay, "Title", "NIGHTHAWKS VIDEO", 62, new Color(1f, 0.78f, 0.38f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 110f);
        titleRt.sizeDelta = new Vector2(1100f, 90f);

        var subtitle = CreateText(overlay, "Subtitle", "A  L A T E   N I G H T   V I D E O   S T O R E", 18, new Color(0.85f, 0.85f, 0.9f, 0.75f), TextAnchor.MiddleCenter, FontStyle.Normal);
        var subRt = subtitle.rectTransform;
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.anchoredPosition = new Vector2(0f, 55f);
        subRt.sizeDelta = new Vector2(1100f, 32f);

        string controls = FirstPersonController.TouchCapable
            ? "LEFT STICK TO MOVE   \u00B7   DRAG THE RIGHT SIDE TO LOOK"
            : "WASD TO MOVE   \u00B7   MOUSE TO LOOK   \u00B7   SHIFT TO RUN";

        var controlText = CreateText(overlay, "Controls", controls, 20, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal);
        var controlRt = controlText.rectTransform;
        controlRt.anchorMin = new Vector2(0.5f, 0.5f);
        controlRt.anchorMax = new Vector2(0.5f, 0.5f);
        controlRt.pivot = new Vector2(0.5f, 0.5f);
        controlRt.anchoredPosition = new Vector2(0f, -25f);
        controlRt.sizeDelta = new Vector2(1100f, 32f);

        enterText = CreateText(overlay, "Enter", FirstPersonController.TouchCapable ? "TAP TO ENTER" : "CLICK TO ENTER", 24, new Color(1f, 0.82f, 0.45f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        var enterRt = enterText.rectTransform;
        enterRt.anchorMin = new Vector2(0.5f, 0.5f);
        enterRt.anchorMax = new Vector2(0.5f, 0.5f);
        enterRt.pivot = new Vector2(0.5f, 0.5f);
        enterRt.anchoredPosition = new Vector2(0f, -120f);
        enterRt.sizeDelta = new Vector2(1100f, 40f);
    }

    // ------------------------------------------------------------------ helpers

    static bool AnyInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        return false;
#else
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0;
#endif
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        return rt;
    }

    static Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor, FontStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = UIFont;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - distance) / 1.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float outer = center - 1f;
        float inner = outer * 0.80f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((outer - distance) / 1.5f) * Mathf.Clamp01((distance - inner) / 1.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;

        const int size = 64;
        const int radius = 18;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Max(0f, Mathf.Abs(x - (size - 1) * 0.5f) - (size * 0.5f - radius));
                float py = Mathf.Max(0f, Mathf.Abs(y - (size - 1) * 0.5f) - (size * 0.5f - radius));
                float distance = Mathf.Sqrt(px * px + py * py);
                float alpha = Mathf.Clamp01((radius - distance) / 1.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        roundedSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return roundedSprite;
    }
}

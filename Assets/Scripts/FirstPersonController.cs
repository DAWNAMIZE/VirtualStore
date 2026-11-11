using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First person walk controller.
/// Desktop: WASD + mouse look (click to capture the cursor, Esc to release).
/// Mobile: on-screen joystick + drag anywhere else to look around.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public Transform cameraTransform;
    public VirtualStoreHUD hud;

    [Header("Movement")]
    public float walkSpeed = 3.4f;
    public float sprintSpeed = 5.4f;
    public float acceleration = 12f;
    public float gravity = -20f;

    [Header("Look")]
    public float mouseSensitivity = 0.09f;
    public float touchSensitivity = 0.12f;
    public float pitchLimit = 85f;

    [Header("Head Bob")]
    public float bobAmplitude = 0.026f;
    public float bobFrequency = 8.5f;

    [Header("Field of View")]
    [Tooltip("Horizontal FOV kept constant across aspect ratios, so narrow portrait screens do not crop the aisle.")]
    public float horizontalFov = 95f;
    public float minVerticalFov = 68f;
    public float maxVerticalFov = 84f;

    CharacterController controller;
    Camera viewCamera;
    float yaw;
    float pitch;
    float verticalVelocity;
    Vector2 smoothedMove;
    float bobPhase;
    float eyeHeight;
    bool cursorLocked;

    public bool IsCursorLocked => cursorLocked;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        viewCamera = GetComponentInChildren<Camera>();

        if (cameraTransform == null && viewCamera != null)
            cameraTransform = viewCamera.transform;

        if (hud == null)
            hud = GetComponent<VirtualStoreHUD>();

        if (cameraTransform != null)
            eyeHeight = cameraTransform.localPosition.y;

        yaw = transform.eulerAngles.y;
    }

    void Start()
    {
        // Never grab the pointer before a user gesture: browsers (and WebGL in
        // particular) reject pointer lock outside of a click/tap event.
        LockCursor(false);
    }

    void Update()
    {
        HandleLookInput();
        HandleMovement();
        ApplyHeadBob();
        ApplyFieldOfView();
    }

    /// <summary>
    /// Keeps the horizontal field of view roughly constant so a portrait phone
    /// still shows both shelves instead of a narrow vertical slice.
    /// </summary>
    void ApplyFieldOfView()
    {
        if (viewCamera == null) return;

        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        float vertical = 2f * Mathf.Atan(Mathf.Tan(horizontalFov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.1f, aspect)) * Mathf.Rad2Deg;
        vertical = Mathf.Clamp(vertical, minVerticalFov, maxVerticalFov);

        if (Mathf.Abs(viewCamera.fieldOfView - vertical) > 0.05f)
            viewCamera.fieldOfView = vertical;
    }

    public static bool AnyTouchPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                if (touchscreen.touches[i].press.isPressed)
                    return true;
            }
            return false;
#else
            return Input.touchCount > 0;
#endif
        }
    }

    public static bool TouchMode
    {
        get { return TouchCapable; }
    }

    /// <summary>
    /// True when the current device can produce touch input. WebGL on a phone
    /// reports <c>Application.isMobilePlatform == false</c>, so touch support
    /// has to be detected separately.
    /// </summary>
    public static bool TouchCapable
    {
        get
        {
            if (Application.isMobilePlatform) return true;
            if (SystemInfo.deviceType == DeviceType.Handheld) return true;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null) return true;
#endif

            return AnyTouchPressed;
        }
    }

    public void LockCursor(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleLookInput()
    {
        Vector2 look = Vector2.zero;
        bool applyLook = false;

#if ENABLE_INPUT_SYSTEM
        if (!TouchMode && Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame && !cursorLocked)
                LockCursor(true);

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                LockCursor(false);

            if (cursorLocked)
            {
                look += Mouse.current.delta.ReadValue() * mouseSensitivity;
                applyLook = true;
            }
        }

        var touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                var touch = touchscreen.touches[i];
                if (!touch.press.isPressed) continue;

                int fingerId = touch.touchId.ReadValue();

                // Ignore the finger that is driving the virtual joystick.
                if (hud != null && hud.Joystick != null &&
                    hud.Joystick.IsDragging && hud.Joystick.FingerId == fingerId)
                    continue;

                look += touch.delta.ReadValue() * touchSensitivity;
                applyLook = true;
            }
        }
#else
        if (!TouchMode)
        {
            if (Input.GetMouseButtonDown(0) && !cursorLocked)
                LockCursor(true);
            if (Input.GetKeyDown(KeyCode.Escape))
                LockCursor(false);

            if (cursorLocked)
            {
                look += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * (mouseSensitivity * 12f);
                applyLook = true;
            }
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (hud != null && hud.Joystick != null &&
                hud.Joystick.IsDragging && hud.Joystick.FingerId == touch.fingerId)
                continue;

            look += touch.deltaPosition * touchSensitivity * 0.1f;
            applyLook = true;
        }
#endif

        if (!applyLook) return;

        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);

        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        bool sprint = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            sprint = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
#else
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif

        if (hud != null && hud.Joystick != null && hud.Joystick.gameObject.activeInHierarchy)
        {
            Vector2 stick = hud.Joystick.Value;
            input += stick;
            if (stick.magnitude > 0.94f)
                sprint = true;
        }

        input = Vector2.ClampMagnitude(input, 1f);
        smoothedMove = Vector2.Lerp(smoothedMove, input, 1f - Mathf.Exp(-acceleration * Time.deltaTime));

        float speed = sprint ? sprintSpeed : walkSpeed;
        Vector3 planar = (transform.right * smoothedMove.x + transform.forward * smoothedMove.y) * speed;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move((planar + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    void ApplyHeadBob()
    {
        if (cameraTransform == null) return;

        float planarSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
        bool moving = controller.isGrounded && planarSpeed > 0.15f;

        if (moving)
            bobPhase += Time.deltaTime * bobFrequency * Mathf.Clamp(planarSpeed / walkSpeed, 0.6f, 1.4f);

        float target = eyeHeight + (moving ? Mathf.Sin(bobPhase * Mathf.PI * 2f) * bobAmplitude : 0f);
        Vector3 localPos = cameraTransform.localPosition;
        localPos.y = Mathf.Lerp(localPos.y, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
        cameraTransform.localPosition = localPos;
    }
}

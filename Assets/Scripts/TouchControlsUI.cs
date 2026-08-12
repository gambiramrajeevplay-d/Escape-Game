using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Script;

/// <summary>
/// Shows/hides on-screen touch controls based on device type.
///
/// TV / Fire TV:
///     - Touch controls hidden
///     - Remote/gamepad controls used
///
/// Mobile / Tablet:
///     - Touch controls shown
///
/// Both:
///     - Touch controls shown
///     - Keyboard/gamepad input still works
///
/// Auto Detect can be disabled to force a device type while testing.
/// </summary>
public class TouchControlsUI : MonoBehaviour
{
    public enum DeviceType
    {
        Mobile,
        TV,
        Both
    }

    [Header("Testing")]
    [Tooltip("Automatically detect TV or mobile device.")]
    public bool autoDetectDevice = true;

    [Tooltip("Used only when Auto Detect Device is disabled.")]
    public DeviceType deviceTypeOverride = DeviceType.Mobile;

    [Header("Touch Control Buttons")]
    [Tooltip("Parent containing all mobile touch controls.")]
    [SerializeField] private GameObject controlsRoot;

    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button forwardButton;
    [SerializeField] private Button jumpButton;

    // =========================================================
    // STATIC INPUT
    // =========================================================

    public static float Horizontal { get; private set; }
    public static float Vertical { get; private set; }

    public static bool JumpPressed { get; private set; }

    public static bool ControlsActive { get; private set; }

    // =========================================================
    // HOLD STATES
    // =========================================================

    private bool holdingLeft;
    private bool holdingRight;
    private bool holdingForward;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        DeviceType deviceType = GetDeviceType();

        bool showTouchControls =
            deviceType == DeviceType.Mobile ||
            deviceType == DeviceType.Both;

        ControlsActive = showTouchControls;

        if (controlsRoot != null)
        {
            controlsRoot.SetActive(showTouchControls);
        }
        else
        {
            gameObject.SetActive(showTouchControls);
        }

        // Reset input states.
        Horizontal = 0f;
        Vertical = 0f;
        JumpPressed = false;

        holdingLeft = false;
        holdingRight = false;
        holdingForward = false;
    }

    private void Start()
    {
        if (!ControlsActive)
            return;

        WireHold(
            leftButton,
            () => holdingLeft = true,
            () => holdingLeft = false
        );

        WireHold(
            rightButton,
            () => holdingRight = true,
            () => holdingRight = false
        );

        WireHold(
            forwardButton,
            () => holdingForward = true,
            () => holdingForward = false
        );

        if (jumpButton != null)
        {
            jumpButton.onClick.AddListener(
                () => JumpPressed = true
            );
        }
    }

    private void Update()
    {
        if (!ControlsActive)
        {
            Horizontal = 0f;
            Vertical = 0f;
            return;
        }

        Horizontal =
            (holdingRight ? 1f : 0f) +
            (holdingLeft ? -1f : 0f);

        Vertical =
            holdingForward ? 1f : 0f;
    }

    // =========================================================
    // DEVICE DETECTION
    // =========================================================

    public DeviceType GetDeviceType()
    {
        // Manual testing override.
        if (!autoDetectDevice)
            return deviceTypeOverride;

        // Android TV / Fire TV.
        if (AndroidTV.IsAndroidOrFireTv())
            return DeviceType.TV;

        // Everything else supported as touch/mobile.
        if (Application.isMobilePlatform)
            return DeviceType.Mobile;

        // Editor / PC:
        // Treat as Mobile by default so the touch UI can be tested.
        return DeviceType.Mobile;
    }

    /// <summary>
    /// Returns true when the current device is TV / Fire TV.
    /// </summary>
    public bool IsTV()
    {
        return GetDeviceType() == DeviceType.TV;
    }

    /// <summary>
    /// Returns true when touch controls should be visible.
    /// </summary>
    public bool IsMobileControls()
    {
        DeviceType type = GetDeviceType();

        return type == DeviceType.Mobile ||
               type == DeviceType.Both;
    }

    // =========================================================
    // JUMP
    // =========================================================

    /// <summary>
    /// Called after PlayerControllerRoblox reads JumpPressed.
    /// Prevents one tap from triggering multiple jumps.
    /// </summary>
    public static void ConsumeJump()
    {
        JumpPressed = false;
    }

    // =========================================================
    // HOLD BUTTONS
    // =========================================================

    private void WireHold(
        Button button,
        System.Action onDown,
        System.Action onUp)
    {
        if (button == null)
            return;

        EventTrigger trigger =
            button.gameObject.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger =
                button.gameObject.AddComponent<EventTrigger>();
        }

        AddEntry(
            trigger,
            EventTriggerType.PointerDown,
            onDown
        );

        AddEntry(
            trigger,
            EventTriggerType.PointerUp,
            onUp
        );

        AddEntry(
            trigger,
            EventTriggerType.PointerExit,
            onUp
        );
    }

    private void AddEntry(
        EventTrigger trigger,
        EventTriggerType type,
        System.Action action)
    {
        EventTrigger.Entry entry =
            new EventTrigger.Entry();

        entry.eventID = type;

        entry.callback.AddListener(
            (_) => action()
        );

        trigger.triggers.Add(entry);
    }
}
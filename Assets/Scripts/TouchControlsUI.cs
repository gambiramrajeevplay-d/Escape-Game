using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Script;

/// <summary>
/// Shows/hides on-screen touch controls based on device type.
/// TV (Android TV / Fire TV) -> hidden, uses remote/gamepad instead.
/// Phone/Tablet -> shown.
/// Turn off "Auto Detect Device" and set "Device Type Override" to force
/// either state while testing in the Editor or on a dev build, without
/// needing an actual TV or tablet on hand.
/// </summary>
public class TouchControlsUI : MonoBehaviour
{
    public enum DeviceType
    {
        Mobile, // touch controls shown
        TV,     // touch controls hidden, remote/gamepad only
        Both    // touch controls shown AND keyboard/gamepad input still works at the same time
    }

    [Header("Testing")]
    [Tooltip("Real device detection (AndroidTV check + mobile check). Turn off to force a specific device type below.")]
    public bool autoDetectDevice = true;

    [Tooltip("Only used when Auto Detect Device is off.")]
    public DeviceType deviceTypeOverride = DeviceType.Mobile;

    [Header("Touch Control Buttons")]
    [Tooltip("Parent panel holding all the on-screen buttons. If left empty, this GameObject itself is shown/hidden.")]
    [SerializeField] private GameObject controlsRoot;

    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button forwardButton;
    [SerializeField] private Button jumpButton;

    // Static so PlayerControllerRoblox (or anything else) can read touch
    // input without needing a direct scene reference to this UI.
    public static float Horizontal { get; private set; }
    public static float Vertical { get; private set; }
    public static bool JumpPressed { get; private set; }
    public static bool ControlsActive { get; private set; }

    private bool holdingLeft;
    private bool holdingRight;
    private bool holdingForward;

    private void Awake()
    {
        bool shouldShow = ShouldShowControls();
        ControlsActive = shouldShow;

        if (controlsRoot != null)
            controlsRoot.SetActive(shouldShow);
        else
            gameObject.SetActive(shouldShow);
    }

    private void Start()
    {
        if (!ControlsActive)
            return;

        WireHold(leftButton, () => holdingLeft = true, () => holdingLeft = false);
        WireHold(rightButton, () => holdingRight = true, () => holdingRight = false);
        WireHold(forwardButton, () => holdingForward = true, () => holdingForward = false);

        if (jumpButton != null)
            jumpButton.onClick.AddListener(() => JumpPressed = true);
    }

    private void Update()
    {
        if (!ControlsActive)
            return;

        Horizontal = (holdingRight ? 1f : 0f) + (holdingLeft ? -1f : 0f);
        Vertical = holdingForward ? 1f : 0f;
    }

    /// <summary>
    /// Call this right after reading JumpPressed (e.g. in PlayerControllerRoblox's
    /// HandleJump), so a single tap doesn't keep firing the jump every frame.
    /// </summary>
    public static void ConsumeJump()
    {
        JumpPressed = false;
    }

    private bool ShouldShowControls()
    {
        if (!autoDetectDevice)
            // Mobile and Both both show the touch buttons. Only TV hides
            // them. Keyboard/gamepad input in PlayerControllerRoblox is
            // never disabled by this — it always keeps working regardless
            // of which device type is picked here, so "Both" is really just
            // "show the buttons too, on top of keyboard/gamepad."
            return deviceTypeOverride != DeviceType.TV;

        if (AndroidTV.IsAndroidOrFireTv())
            return false; // TV - remote/gamepad only, no touch buttons

        return Application.isMobilePlatform; // phone/tablet - show buttons
    }

    private void WireHold(Button button, System.Action onDown, System.Action onUp)
    {
        if (button == null) return;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        AddEntry(trigger, EventTriggerType.PointerDown, onDown);
        AddEntry(trigger, EventTriggerType.PointerUp, onUp);
        AddEntry(trigger, EventTriggerType.PointerExit, onUp); // dragging finger off the button releases it too
    }

    private void AddEntry(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((_) => action());
        trigger.triggers.Add(entry);
    }
}
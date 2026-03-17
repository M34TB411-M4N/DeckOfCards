using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour {
    // SINGLETON PATTERN
    public static CameraController Instance { get; private set; }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float elevationSpeed = 3f;
    [SerializeField] private float focusDuration = 0.5f;

    [Header("Sensitivity Settings")]
    [Tooltip("Sensitivity for PC/Mac Mouse turning")]
    [SerializeField] private float mouseSensitivity = 10f;
    [Tooltip("Degrees the camera turns for a full screen swipe on Mobile")]
    [SerializeField] private float touchSensitivity = 300f;

    [Header("Input References")]
    [SerializeField] private ObjectSelect objectSelect;
    [SerializeField] private Joystick movementJoystick;

    private Vector3 moveInput;
    private float elevationInput;
    private float pitch = 0f;
    private float yaw = 0f;
    private bool isRotating = false;
    private int activeFingerId = -1;
    private Coroutine focusCoroutine;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start() {
        Vector3 rot = transform.localRotation.eulerAngles;
        yaw = rot.y;
        pitch = rot.x;

        if (objectSelect == null) {
            objectSelect = FindFirstObjectByType<ObjectSelect>();
            if (objectSelect == null) Debug.LogError("[CameraController] ObjectSelect reference is MISSING!");
        }
    }

    void Update() {
        if (movementJoystick != null) {
            Vector2 joystickDir = movementJoystick.Direction;
            if (joystickDir.magnitude > 0.05f) {
                SetMoveInput(joystickDir);
            } else {
                SetMoveInput(Vector2.zero);
            }
        }

        HandleLook();
        ApplyMovement();
    }

    public void SetMoveInput(Vector2 input) {
        moveInput = new Vector3(input.x, 0, input.y);
    }

    public void SetElevationInput(float input) {
        elevationInput = input;
    }

    private void HandleLook() {
        if (objectSelect == null) return;

        // --- MOBILE TOUCH ---
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began) {
                if (objectSelect.CanCameraRotate()) {
                    isRotating = true;
                    activeFingerId = touch.fingerId;
                }
            }

            if (isRotating && touch.fingerId == activeFingerId) {
                if (touch.phase == TouchPhase.Moved) {

                    float normalizedX = touch.deltaPosition.x / Screen.width;
                    float normalizedY = touch.deltaPosition.y / Screen.height;

                    ApplyRotation(normalizedX * touchSensitivity, normalizedY * touchSensitivity);
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                    isRotating = false;
                    activeFingerId = -1;
                }
            }
            return;
        }

        // --- MOUSE (Editor & PC/Mac Builds) ---
        if (Input.GetMouseButtonDown(0)) {
            if (objectSelect.CanCameraRotate()) {
                isRotating = true;
            }
        }

        if (isRotating && Input.GetMouseButton(0)) {
            ApplyRotation(Input.GetAxis("Mouse X") * mouseSensitivity, Input.GetAxis("Mouse Y") * mouseSensitivity);
        }

        if (Input.GetMouseButtonUp(0)) {
            isRotating = false;
        }
    }

    private void ApplyRotation(float x, float y) {
        yaw -= x;
        pitch += y;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // --- THE FIX: Pitch-Independent Movement ---
    private void ApplyMovement() {
        if (moveInput == Vector3.zero && elevationInput == 0) return;

        // 1. Create a perfectly flat rotation using ONLY the camera's left/right yaw
        Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);

        // 2. Derive true horizontal forward/right vectors from that flat rotation
        Vector3 flatForward = flatRotation * Vector3.forward;
        Vector3 flatRight = flatRotation * Vector3.right;

        // 3. Apply the joystick inputs to our new flat vectors
        Vector3 direction = (flatForward * moveInput.z) + (flatRight * moveInput.x);
        Vector3 elevation = Vector3.up * elevationInput * elevationSpeed;

        // Apply it all! (direction magnitude is tied purely to how hard they push the joystick)
        transform.position += (direction * moveSpeed + elevation) * Time.deltaTime;
    }
    // ------------------------------------------

    public void OnUpButtonDown() => elevationInput = 1f;
    public void OnDownButtonDown() => elevationInput = -1f;
    public void OnElevationButtonUp() => elevationInput = 0f;

    public void FocusOnTransform(Transform targetAnchor) {
        if (targetAnchor == null) return;
        if (focusCoroutine != null) StopCoroutine(focusCoroutine);
        focusCoroutine = StartCoroutine(MoveToAnchor(targetAnchor));
    }

    private IEnumerator MoveToAnchor(Transform anchor) {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < focusDuration) {
            elapsed += Time.deltaTime;
            float t = elapsed / focusDuration;
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPos, anchor.position, t);
            transform.rotation = Quaternion.Slerp(startRot, anchor.rotation, t);
            yield return null;
        }

        transform.position = anchor.position;
        transform.rotation = anchor.rotation;

        Vector3 finalEuler = transform.localRotation.eulerAngles;
        pitch = finalEuler.x;
        if (pitch > 180) pitch -= 360;
        yaw = finalEuler.y;

        focusCoroutine = null;
    }
}
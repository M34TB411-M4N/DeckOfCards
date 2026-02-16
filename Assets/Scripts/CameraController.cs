using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour {
    // SINGLETON PATTERN
    public static CameraController Instance { get; private set; }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float elevationSpeed = 3f;
    [SerializeField] private float focusDuration = 0.5f;

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
        // Ensure there is only one CameraController
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
    }

    void Update() {
        if (movementJoystick != null) {
            Vector2 joystickDir = movementJoystick.Direction;

            // Only apply input if the joystick is moved more than 5%
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
        // --- EDITOR TESTING (Mouse) ---
        if (Application.isEditor && !Input.touchSupported) {
            if (Input.GetMouseButtonDown(0)) {
                // Only start rotating if we didn't click UI or an Object
                if (!EventSystem.current.IsPointerOverGameObject() && !IsOverObject()) {
                    isRotating = true;
                }
            }

            if (isRotating && Input.GetMouseButton(0)) {
                ApplyRotation(Input.GetAxis("Mouse X") * 10f, Input.GetAxis("Mouse Y") * 10f);
            }

            if (Input.GetMouseButtonUp(0)) isRotating = false;
            return;
        }

        // --- MOBILE TOUCH ---
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == UnityEngine.TouchPhase.Began) {
                // 1. Is it over UI (Joystick/Buttons)? 
                // 2. Is it over a Card/Deck?
                if (!EventSystem.current.IsPointerOverGameObject(touch.fingerId) && !IsOverObject()) {
                    isRotating = true;
                    activeFingerId = touch.fingerId;
                }
            }

            if (isRotating && touch.fingerId == activeFingerId) {
                if (touch.phase == UnityEngine.TouchPhase.Moved) {
                    ApplyRotation(touch.deltaPosition.x * lookSensitivity, touch.deltaPosition.y * lookSensitivity);
                }

                if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled) {
                    isRotating = false;
                    activeFingerId = -1;
                }
            }
        }
    }

    private bool IsOverObject() {
        if (objectSelect == null) return false;

        return objectSelect.IsPointerOverDraggable();
    }

    private void ApplyRotation(float x, float y) {
        yaw -= x;
        pitch += y;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
    private void ApplyMovement() {
        if (moveInput == Vector3.zero && elevationInput == 0) return;

        Vector3 direction = (transform.forward * moveInput.z) + (transform.right * moveInput.x);
        direction.y = 0; // Keep movement on the horizontal plane

        float currentHorizontalSpeed = direction.magnitude * moveSpeed;

        Vector3 elevation = Vector3.up * elevationInput * elevationSpeed;

        transform.position += (direction.normalized * currentHorizontalSpeed + elevation) * Time.deltaTime;
    }

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
            t = t * t * (3f - 2f * t); // Smoothstep

            transform.position = Vector3.Lerp(startPos, anchor.position, t);
            transform.rotation = Quaternion.Slerp(startRot, anchor.rotation, t);
            yield return null;
        }

        transform.position = anchor.position;
        transform.rotation = anchor.rotation;

        // Sync variables so joystick movement doesn't snap
        Vector3 finalEuler = transform.localRotation.eulerAngles;
        pitch = finalEuler.x;
        if (pitch > 180) pitch -= 360;
        yaw = finalEuler.y;

        focusCoroutine = null;
    }
}
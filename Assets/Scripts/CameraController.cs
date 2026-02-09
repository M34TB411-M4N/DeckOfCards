using UnityEngine;
using UnityEngine.EventSystems; // Fixed CS0103

public class CameraController : MonoBehaviour {
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float elevationSpeed = 3f;

    [Header("Input References")]
    [SerializeField] private ObjectSelect objectSelect;
    [SerializeField] private Joystick movementJoystick;

    private Vector3 moveInput;
    private float elevationInput;

    private float pitch = 0f;
    private float yaw = 0f;

    private bool isRotating = false;
    private int activeFingerId = -1;

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

    // Inside CameraController.cs
    private bool IsOverObject() {
        if (objectSelect == null) return false;

        // Now the camera only stops if we are clicking a CARD or DECK
        return objectSelect.IsPointerOverDraggable();
    }

    private void ApplyRotation(float x, float y) {
        yaw -= x;
        pitch += y;
        pitch = Mathf.Clamp(pitch, -80f, 80f);
        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
    private void ApplyMovement() {
        // If there's no movement or elevation input, stop early
        if (moveInput == Vector3.zero && elevationInput == 0) return;

        // 1. Get the horizontal direction relative to camera
        Vector3 direction = (transform.forward * moveInput.z) + (transform.right * moveInput.x);
        direction.y = 0; // Keep movement on the horizontal plane

        // 2. IMPORTANT: Do NOT normalize the direction here if you want variable speed.
        // The moveInput already has a magnitude between 0 and 1 from the joystick.
        float currentHorizontalSpeed = direction.magnitude * moveSpeed;

        // 3. Handle Elevation (this remains constant speed for precision)
        Vector3 elevation = Vector3.up * elevationInput * elevationSpeed;

        // 4. Apply the translation
        // We normalize the direction just to get the vector, then multiply by our calculated speed
        transform.position += (direction.normalized * currentHorizontalSpeed + elevation) * Time.deltaTime;
    }

    // Add these to CameraController.cs
    public void OnUpButtonDown() => elevationInput = 1f;
    public void OnDownButtonDown() => elevationInput = -1f;
    public void OnElevationButtonUp() => elevationInput = 0f;
}
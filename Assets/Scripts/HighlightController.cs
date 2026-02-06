using UnityEngine;

[DisallowMultipleComponent]
public class HighlightController : MonoBehaviour {
    [Tooltip("Padding added on each axis relative to target bounds (fraction).")]
    [SerializeField] private float padding = 0.04f;

    [Tooltip("How fast the highlight moves/lerps to follow the target.")]
    [SerializeField] private float followSpeed = 25f;

    private GameObject target;
    private Renderer[] targetRenderers;
    private Collider[] targetColliders;
    private bool isActive = false;



    void Awake() {
        HideImmediate();
    }

    void FixedUpdate() {
        if (!isActive || target == null)
            return;

        Bounds b = ComputeTargetBounds();
        Vector3 targetPos = b.center;
        Vector3 targetScale = b.size;

        // add padding (fraction of size)
        targetScale = targetScale * (1f + padding);

        // keep highlight axis-aligned (don't inherit target rotation)
        // smoothly follow position and scale to avoid jitter
        transform.position = Vector3.Lerp(transform.position, targetPos, Mathf.Clamp01(Time.fixedDeltaTime * followSpeed));
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Mathf.Clamp01(Time.fixedDeltaTime * followSpeed));
    }       
        
    private Bounds ComputeTargetBounds() {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        // Try renderers
        if (targetRenderers != null && targetRenderers.Length > 0) {
            bool first = true;
            foreach (var r in targetRenderers) {
                if (r == null) continue;
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            if (!first) return bounds;
        }

        // Fallback to colliders
        if (targetColliders != null && targetColliders.Length > 0) {
            bool first = true;
            foreach (var c in targetColliders) {
                if (c == null) continue;
                if (first) { bounds = c.bounds; first = false; } else bounds.Encapsulate(c.bounds);
            }
            if (!first) return bounds;
        }

        // ultimate fallback: single point at target position
        return new Bounds(target.transform.position, Vector3.one * 0.1f);
    }

    /// <summary>
    /// Show the highlight around the given object and begin following it.
    /// </summary>
    public void Show(GameObject newTarget) {
        if (newTarget == null) return;

        target = newTarget;
        targetRenderers = target.GetComponentsInChildren<Renderer>();
        targetColliders = target.GetComponentsInChildren<Collider>();

        isActive = true;
        gameObject.SetActive(true);
        Debug.Log("showing");

        // Immediately snap to avoid visible pop
        Bounds b = ComputeTargetBounds();
        transform.position = b.center;
        transform.localScale = b.size * (1f + padding);
    }

    /// <summary>
    /// Hide highlight and stop following.
    /// </summary>
    public void Hide() {
        isActive = false;
        // keep it inactive so FixedUpdate won't run
        gameObject.SetActive(false);
        target = null;
        targetRenderers = null;
        targetColliders = null;
        Debug.Log("hide");
    }

    /// <summary>
    /// Immediately hide without touching follow flags.
    /// </summary>
    public void HideImmediate() {
        isActive = false;
        gameObject.SetActive(false);
        target = null;
        targetRenderers = null;
        targetColliders = null;
        Debug.Log("hideimm");
    }
}

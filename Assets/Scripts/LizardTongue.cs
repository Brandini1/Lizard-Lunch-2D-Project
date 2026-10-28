using UnityEngine;
using UnityEngine.InputSystem;

// click to flick the tongue straight ahead.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LizardMovement))]
public sealed class LizardTongue : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float reach = 2.6f;
    [SerializeField, Min(0.01f)] private float extendSeconds = 0.12f;
    [SerializeField, Min(0f)] private float holdSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float retractSeconds = 0.16f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.25f;
    [SerializeField, Min(0f)] private float mouthOffset = 0.8f;

    private LizardMovement lizardMovement;
    private BackyardMap backyard;
    private LineRenderer tongueEdge;
    private LineRenderer tongueCenter;
    private Material tongueMaterial;
    private Vector2 shotDirection;
    private float shotTime;
    private float nextShotTime;
    private bool isShooting;

    public bool IsOut => isShooting;
    public float Readiness => isShooting ? 0f : cooldownSeconds <= 0f ? 1f
        : Mathf.Clamp01(1f - (nextShotTime - Time.time) / cooldownSeconds);
    public float Reach
    {
        get => reach;
        set => reach = Mathf.Max(0.1f, value);
    }
    public float CooldownSeconds
    {
        get => cooldownSeconds;
        set => cooldownSeconds = Mathf.Max(0f, value);
    }
    public Vector2 StartPoint => tongueCenter.GetPosition(0);
    public Vector2 TipPoint => tongueCenter.GetPosition(1);

    private void Awake()
    {
        lizardMovement = GetComponent<LizardMovement>();
        tongueMaterial = new Material(Shader.Find("Sprites/Default"));
        tongueEdge = MakeTongueLine("Tongue edge", new Color(0.48f, 0.10f, 0.25f), 0.22f, -2);
        tongueCenter = MakeTongueLine("Tongue", new Color(1f, 0.49f, 0.65f), 0.14f, -1);
        ShowTongue(false);
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
            !isShooting && Time.time >= nextShotTime &&
            !(GetComponent<BugGame>()?.BlocksTongue ?? false))
        {
            shotDirection = lizardMovement.FacingDirection;
            shotTime = 0f;
            isShooting = true;
            ShowTongue(true);
        }

        if (!isShooting) return;

        // keep the mouth and tongue together while the lizard walks.
        lizardMovement.FaceDirection(shotDirection);
        shotTime += Time.deltaTime;
        float retractStarts = extendSeconds + holdSeconds;
        float totalSeconds = retractStarts + retractSeconds;
        float stretch = shotTime < extendSeconds ? shotTime / extendSeconds
                      : shotTime < retractStarts ? 1f
                      : 1f - (shotTime - retractStarts) / retractSeconds;

        Vector2 start = (Vector2)transform.position + shotDirection * mouthOffset;
        if (backyard == null) backyard = GetComponent<BackyardMap>();
        float clearReach = backyard != null ? backyard.ClearTongueDistance(start, shotDirection, reach) : reach;
        Vector2 end = start + shotDirection * Mathf.Min(reach * Mathf.Clamp01(stretch), clearReach);
        tongueEdge.SetPosition(0, start);
        tongueEdge.SetPosition(1, end);
        tongueCenter.SetPosition(0, start);
        tongueCenter.SetPosition(1, end);

        if (shotTime < totalSeconds) return;
        isShooting = false;
        nextShotTime = Time.time + cooldownSeconds;
        ShowTongue(false);
    }

    private LineRenderer MakeTongueLine(string lineName, Color color, float width, int order)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.material = tongueMaterial;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.sortingOrder = order;
        return line;
    }

    private void ShowTongue(bool visible)
    {
        tongueEdge.enabled = visible;
        tongueCenter.enabled = visible;
    }

    private void OnDisable()
    {
        isShooting = false;
        if (tongueEdge != null && tongueCenter != null) ShowTongue(false);
    }

    private void OnDestroy()
    {
        if (tongueMaterial != null) Destroy(tongueMaterial);
    }
}

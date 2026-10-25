using UnityEngine;
using UnityEngine.InputSystem;

// top-down movement with four walking directions.
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public sealed class LizardMovement : MonoBehaviour
{
    private const float DashSeconds = 0.18f;
    private const float DashCooldown = 3f;
    private const float DashSpeed = 11f;
    // the vertical frames drift sideways, so each pose gets its own pivot.
    private static readonly float[] DownPivotsX = { 406.5f / 724f, 362f / 724f, 298.5f / 724f };
    private static readonly float[] UpPivotsX = { 422f / 724f, 369.5f / 724f, 293.5f / 724f };

    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 8f;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 300f;

    private readonly Sprite[][] walkFrames = new Sprite[4][];
    private Rigidbody2D lizardBody;
    private SpriteRenderer lizardSprite;
    private Vector2 travelInput;
    private int facing; // down, up, left, right.
    private float stepTime;
    private Vector2 dashDirection;
    private float dashUntil;
    private float dashReadyAt;

    public bool IsDashing => enabled && Time.time < dashUntil;
    public float DashReadiness => IsDashing ? 0f : Mathf.Clamp01(1f - (dashReadyAt - Time.time) / DashCooldown);

    public bool TryDash()
    {
        if (Time.timeScale == 0f || !enabled || IsDashing || Time.time < dashReadyAt) return false;
        Vector2 input = ReadTravel(Keyboard.current);
        dashDirection = input == Vector2.zero ? FacingDirection : input.normalized;
        dashUntil = Time.time + DashSeconds;
        dashReadyAt = Time.time + DashCooldown;
        FaceDirection(dashDirection);
        return true;
    }

    public void ResetDash()
    {
        dashUntil = dashReadyAt = 0f;
        travelInput = Vector2.zero;
        if (lizardSprite != null) lizardSprite.color = Color.white;
        if (lizardBody != null) lizardBody.linearVelocity = Vector2.zero;
    }

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    public Vector2 FacingDirection => facing == 0 ? Vector2.down : facing == 1 ? Vector2.up
                                    : facing == 2 ? Vector2.left : Vector2.right;

    public void FaceDirection(Vector2 direction)
    {
        if (direction == Vector2.zero) return;

        facing = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? (direction.x < 0f ? 2 : 3)
            : (direction.y < 0f ? 0 : 1);
        ShowFrame(1);
    }

    private void Awake()
    {
        lizardBody = GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
        lizardSprite = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();

        if (!TryGetComponent(out CapsuleCollider2D hitbox))
        {
            hitbox = gameObject.AddComponent<CapsuleCollider2D>();
            hitbox.size = new Vector2(0.55f, 1.25f);
        }

        lizardBody.gravityScale = 0f;
        lizardBody.freezeRotation = true;
        lizardBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        LoadWalkFrames(0, "Lizard/walk_down");
        LoadWalkFrames(1, "Lizard/walk_up");
        LoadWalkFrames(2, "Lizard/walk_left");
        LoadWalkFrames(3, "Lizard/walk_right");
        ShowFrame(1);
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
        {
            travelInput = Vector2.zero;
            lizardBody.linearVelocity = Vector2.zero;
            return;
        }

        travelInput = IsDashing ? dashDirection : ReadTravel(Keyboard.current);
        lizardSprite.color = IsDashing ? new Color(0.6f, 1f, 1f) : Color.white;
        if (travelInput == Vector2.zero)
        {
            stepTime = 0f;
            ShowFrame(1);
            return;
        }

        int nextFacing = Mathf.Abs(travelInput.x) > Mathf.Abs(travelInput.y)
            ? (travelInput.x < 0f ? 2 : 3)
            : (travelInput.y < 0f ? 0 : 1);
        if (nextFacing != facing)
        {
            facing = nextFacing;
            stepTime = 0f;
        }

        stepTime += Time.deltaTime * (IsDashing ? 2f : 1f);
        int step = Mathf.FloorToInt(stepTime * walkFramesPerSecond) % 4;
        ShowFrame(step == 1 ? 0 : step == 3 ? 2 : 1);
    }

    private void FixedUpdate()
    {
        lizardBody.linearVelocity = IsDashing ? dashDirection * DashSpeed : travelInput * moveSpeed;
    }

    private static Vector2 ReadTravel(Keyboard keys)
    {
        if (keys == null) return Vector2.zero;
        float x = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1f : 0f)
                - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1f : 0f);
        float y = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1f : 0f)
                - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1f : 0f);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    private void OnDisable() => ResetDash();

    private void OnDestroy()
    {
        foreach (Sprite[] poses in walkFrames)
        {
            if (poses == null) continue;
            foreach (Sprite pose in poses)
                if (pose != null) Destroy(pose);
        }
    }

    private void LoadWalkFrames(int directionIndex, string spritePath)
    {
        Texture2D sheet = Resources.Load<Texture2D>(spritePath);
        if (sheet == null)
        {
            Debug.LogError($"Missing lizard sprite sheet: Resources/{spritePath}.png", this);
            return;
        }

        int cellWidth = sheet.width / 3;
        if (cellWidth < 1 || sheet.height < 1)
        {
            Debug.LogError($"Invalid lizard sprite sheet: {spritePath}.", this);
            return;
        }

        walkFrames[directionIndex] = new Sprite[3];
        for (int frameIndex = 0; frameIndex < 3; frameIndex++)
        {
            Rect cell = new Rect(frameIndex * cellWidth, 0, cellWidth, sheet.height);
            float pivotX = directionIndex == 0 ? DownPivotsX[frameIndex]
                         : directionIndex == 1 ? UpPivotsX[frameIndex] : 0.5f;
            Sprite pose = Sprite.Create(sheet, cell, new Vector2(pivotX, 0.5f), pixelsPerUnit);
            pose.name = $"{spritePath}_{frameIndex}";
            walkFrames[directionIndex][frameIndex] = pose;
        }
    }

    private void ShowFrame(int frame)
    {
        Sprite[] poses = walkFrames[facing];
        if (poses != null)
            lizardSprite.sprite = poses[frame];
    }
}

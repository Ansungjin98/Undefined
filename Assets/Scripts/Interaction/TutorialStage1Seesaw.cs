using UnityEngine;

public enum SeesawSide
{
    Left,
    Right
}

public class TutorialStage1Seesaw : MonoBehaviour
{
    [SerializeField] private Transform board;
    [SerializeField] private float maxTiltAngleZ = 35f;
    [SerializeField] private float loadToTorque = 110f;
    [SerializeField] private float restoreStrength = 5f;
    [SerializeField] private float angularDamping = 5f;
    [SerializeField] private float maxAngularSpeed = 120f;
    [SerializeField] private float leftLoad;
    [SerializeField] private float rightLoad;
    [SerializeField] private float currentAngleZ;
    [SerializeField] private float currentAngularVelocity;
    [SerializeField] private float geometryMaxAngleZ;

    public float CurrentAngleZ => currentAngleZ;
    public float MaxOperationalAngleZ => Mathf.Min(maxTiltAngleZ, geometryMaxAngleZ);

    private Quaternion _initialLocalRotation;
    private float _boardHalfLength;
    private float _boardHalfThickness;
    private float _pivotCenterHeightFromFloor;

    private void Awake()
    {
        if (board == null)
        {
            board = transform;
        }

        _initialLocalRotation = board.localRotation;
        CacheGeometry();
        currentAngleZ = Mathf.Clamp(currentAngleZ, -geometryMaxAngleZ, geometryMaxAngleZ);
        board.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 0f, currentAngleZ);
    }

    private void Update()
    {
        if (board == null)
        {
            return;
        }

        // +Z tilt => left side down, right side up.
        float loadDelta = leftLoad - rightLoad;
        float torque = (loadDelta * loadToTorque) - (currentAngleZ * restoreStrength) - (currentAngularVelocity * angularDamping);
        currentAngularVelocity += torque * Time.deltaTime;
        currentAngularVelocity = Mathf.Clamp(currentAngularVelocity, -maxAngularSpeed, maxAngularSpeed);
        currentAngleZ += currentAngularVelocity * Time.deltaTime;

        float angleLimit = Mathf.Min(maxTiltAngleZ, geometryMaxAngleZ);
        if (currentAngleZ > angleLimit)
        {
            currentAngleZ = angleLimit;
            currentAngularVelocity = 0f;
        }
        else if (currentAngleZ < -angleLimit)
        {
            currentAngleZ = -angleLimit;
            currentAngularVelocity = 0f;
        }

        board.localRotation = _initialLocalRotation * Quaternion.Euler(0f, 0f, currentAngleZ);
    }

    public void SetSideLoad(SeesawSide side, float load)
    {
        float clamped = Mathf.Max(0f, load);
        if (side == SeesawSide.Left)
        {
            leftLoad = clamped;
            return;
        }

        rightLoad = clamped;
    }

    // Compatibility for older calls.
    public void SetWeighted(bool weighted)
    {
        leftLoad = weighted ? 3f : 0f;
        rightLoad = 0f;
    }

    private void CacheGeometry()
    {
        Vector3 scale = board.localScale;
        _boardHalfLength = Mathf.Max(0.1f, Mathf.Abs(scale.x) * 0.5f);
        _boardHalfThickness = Mathf.Max(0.01f, Mathf.Abs(scale.y) * 0.5f);
        _pivotCenterHeightFromFloor = Mathf.Max(0.01f, board.localPosition.y);

        // One end touching floor:
        // pivotHeight - sin(theta)*halfLength - halfThickness = 0
        // theta = asin((pivotHeight - halfThickness)/halfLength)
        float ratio = (_pivotCenterHeightFromFloor - _boardHalfThickness) / _boardHalfLength;
        ratio = Mathf.Clamp(ratio, 0f, 1f);
        geometryMaxAngleZ = Mathf.Asin(ratio) * Mathf.Rad2Deg;
        geometryMaxAngleZ = Mathf.Max(2f, geometryMaxAngleZ);
    }
}

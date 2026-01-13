using UnityEngine;
using UnityEngine.InputSystem;

public class GridCameraController : MonoBehaviour
{
    [Header("References")]
    public GridNodeGraph graph;
    public Transform cameraPivot; 

    [Header("Movement (Grid)")]
    public float moveSmoothTime = 0.15f;
    public float inputRepeatDelay = 0.18f;
    public bool keepCameraHeight = true;

    [Header("Look")]
    public float lookSensitivity = 120f;
    public float minPitch = -45f;
    public float maxPitch = 70f;
    public bool invertY = false;

    [Header("Start")]
    public bool snapToClosestNodeOnStart = true;

    // Input
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float nextMoveTime;

    // Grid state
    private Vector2Int currentCell;
    private Vector3 velocity;
    private float fixedY;

    // Look state
    private float yaw;
    private float pitch;
    private Vector2 lookDelta;
    private bool isDragging;

    private GridNode currentNode;



    private void Awake()
    {
        if (!graph) graph = FindObjectOfType<GridNodeGraph>();
        if (!cameraPivot) cameraPivot = Camera.main.transform;
    }

    private void Start()
    {
        if (!graph)
        {
            Debug.LogError("No hay GridNodeGraph asignado.");
            enabled = false;
            return;
        }

        graph.Rebuild();

        fixedY = transform.position.y;

        Vector3 angles = cameraPivot.localEulerAngles;
        yaw = angles.y;
        pitch = angles.x > 180 ? angles.x - 360 : angles.x;

        if (snapToClosestNodeOnStart)
        {
            float best = float.PositiveInfinity;
            Vector2Int bestCell = graph.WorldToCell(transform.position);

            foreach (var kvp in graph.NodesByCell)
            {
                Vector3 target = graph.CellToWorldCenter(kvp.Key, fixedY);
                float d = (transform.position - target).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestCell = kvp.Key;
                }
            }

            currentCell = bestCell;
            UpdateActiveViewNode();
            transform.position = GetTargetWorldPos(currentCell);
        }
        else
        {
            currentCell = graph.WorldToCell(transform.position);
        }
    }

    private void Update()
    {
        HandleGridMovement();
        HandleLook();
    }

    private void LateUpdate()
    {
        Vector3 targetPos = GetTargetWorldPos(currentCell);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocity,
            moveSmoothTime
        );
    }


    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            isDragging = false;
            lookDelta = Vector2.zero;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            isDragging = false;
            lookDelta = Vector2.zero;
        }
    }

    private void OnDisable()
    {
        isDragging = false;
        lookDelta = Vector2.zero;
    }

    private void UpdateActiveViewNode()
    {
        // Apagar el anterior
        if (currentNode != null)
            currentNode.SetViewActive(false);

        // Encender el nuevo
        if (graph.TryGetNode(currentCell, out GridNode node))
        {
            currentNode = node;
            currentNode.SetViewActive(true);
        }
    }

    private void HandleGridMovement()
    {
        if (Time.time < nextMoveTime) return;

        Vector2Int dir = ReadCardinalRelativeToCamera(moveInput);
        if (dir == Vector2Int.zero) return;

        Vector2Int targetCell = currentCell + dir;

        if (graph.HasNode(targetCell))
        {
            currentCell = targetCell;
            UpdateActiveViewNode();
        }


        nextMoveTime = Time.time + inputRepeatDelay;
    }

    private Vector3 GetTargetWorldPos(Vector2Int cell)
    {
        float y = keepCameraHeight ? fixedY : transform.position.y;
        return graph.CellToWorldCenter(cell, y);
    }

    private Vector2Int ReadCardinalRelativeToCamera(Vector2 input)
    {
        if (input.sqrMagnitude < 0.04f) return Vector2Int.zero;

        Vector3 up = graph != null && graph.useXZPlane ? Vector3.up : Vector3.forward;

        Vector3 camForward = Vector3.ProjectOnPlane(cameraPivot.forward, up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(cameraPivot.right, up).normalized;

        Vector3 desiredWorld = camForward * input.y + camRight * input.x;

        if (graph != null && graph.useXZPlane)
        {
            if (Mathf.Abs(desiredWorld.x) > Mathf.Abs(desiredWorld.z))
                return desiredWorld.x > 0 ? Vector2Int.right : Vector2Int.left;
            else
                return desiredWorld.z > 0 ? Vector2Int.up : Vector2Int.down; // up = +Z
        }
        else
        {
            if (Mathf.Abs(desiredWorld.x) > Mathf.Abs(desiredWorld.y))
                return desiredWorld.x > 0 ? Vector2Int.right : Vector2Int.left;
            else
                return desiredWorld.y > 0 ? Vector2Int.up : Vector2Int.down; // up = +Y
        }
    }

    private void HandleLook()
    {
        if (!isDragging) return;

        if (!Pointer.current.press.isPressed)
        {
            isDragging = false;
            lookDelta = Vector2.zero;
            return;
        }

        if (lookDelta.sqrMagnitude < 0.0001f) return;

        float deltaX = lookDelta.x * lookSensitivity * Time.deltaTime;
        float deltaY = lookDelta.y * lookSensitivity * Time.deltaTime;

        yaw += deltaX;
        pitch += invertY ? deltaY : -deltaY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        lookDelta = Vector2.zero;
    }


    public void OnLookDelta(InputValue value)
    {
        lookDelta = value.Get<Vector2>();
    }

    public void OnLookPress(InputValue value)
    {
        isDragging = value.isPressed;
        if (!isDragging) lookDelta = Vector2.zero;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

}

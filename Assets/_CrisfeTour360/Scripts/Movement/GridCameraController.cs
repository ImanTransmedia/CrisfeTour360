using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridCameraController : MonoBehaviour
{
    [Header("References")]
    public GridNodeGraph graph;
    public Transform cameraPivot;
    [Header("Pulse Blur")]
    public ViewPointBlurPulse pulse; 

    [Header("Movement (Grid)")]
    public bool showCursor = false;
    public float moveSmoothTime = 0.15f;
    public float inputRepeatDelay = 0.18f;
    public bool keepCameraHeight = true;
    [Header("ViewPoint Switching")]
    public float deactivatePreviousDelay = 0.08f; 
    Coroutine switchCo;


    [Header("Look")]
    public float lookSensitivity = 120f;
    public float minPitch = -45f;
    public float maxPitch = 70f;
    public bool invertY = false;

    [Header("Mobile Tuning")]
    public float mobileSensitivityDivider = 15f;
    float lookMultiplier = 1f;
    float zoomMultiplier = 1f;


    [Header("Zoom")]
    bool isPinchingNow;
    public float minFov = 50f;
    public float maxFov = 120f;
    public float mouseWheelZoomSpeed = 12f;
    public float pinchZoomSpeed = 0.06f;

    [Header("Tap Move")]
    public bool enableTapMove = true;
    public LayerMask tapHitMask = ~0;
    public LayerMask blockHitMask = ~0;
    public float tapMaxDistance = 250f;

    public float tapMaxTime = 0.22f;
    public float tapMaxMovePixels = 12f;

    [Header("Start")]
    public bool snapToClosestNodeOnStart = true;

    Vector2 moveInput;
    float nextMoveTime;

    Vector2Int currentCell;
    Vector3 velocity;
    float fixedY;

    float yaw;
    float pitch;
    Vector2 lookDelta;
    bool isDragging;

    GridNode currentNode;

    Camera cam;
    Vector2 lastPinchVector;
    bool wasPinching;

    Vector2 pointerPos;
    bool hasPointerPos;

    bool pressDown;
    float pressTime;
    Vector2 pressPos;
    bool movedTooMuch;

    void Awake()
    {
        if (!graph) graph = FindObjectOfType<GridNodeGraph>();
        if (!cameraPivot && Camera.main != null) cameraPivot = Camera.main.transform;

        if (cameraPivot != null)
        {
            cam = cameraPivot.GetComponent<Camera>();
            if (cam == null) cam = cameraPivot.GetComponentInChildren<Camera>();
        }

        if (cam == null && Camera.main != null) cam = Camera.main;

        Cursor.visible = showCursor;

        deactivatePreviousDelay = moveSmoothTime * 0.55f;
    }

    void Start()
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
            Vector2Int bestCell = FindClosestCellToPoint(transform.position);
            currentCell = bestCell;
            UpdateActiveViewNode();
            transform.position = GetTargetWorldPos(currentCell);
        }
        else
        {
            currentCell = graph.WorldToCell(transform.position);
            UpdateActiveViewNode();
        }

        if (cam != null) cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minFov, maxFov);

        bool isMobile = DeviceDetector.Instance != null && DeviceDetector.Instance.IsMobile;

        if (isMobile)
        {
            lookMultiplier = 1f / mobileSensitivityDivider;
            zoomMultiplier = 1f / mobileSensitivityDivider;
        }
        else
        {
            lookMultiplier = 1f;
            zoomMultiplier = 1f;
        }

    }

    void Update()
    {
        isPinchingNow = IsTwoFingerPinching();

        HandleMouseWheelZoom();
        HandlePinchZoom();

        if (isPinchingNow)
        {
            isDragging = false;
            lookDelta = Vector2.zero;
            pressDown = false;
            movedTooMuch = false;
            return;
        }

        HandleGridMovement();
        HandleLook();
        UpdateTapState();
        FallbackTapPolling();
    }


    void LateUpdate()
    {
        Vector3 targetPos = GetTargetWorldPos(currentCell);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, moveSmoothTime);
    }

    void ResetPointerState()
    {
        isDragging = false;
        lookDelta = Vector2.zero;
        wasPinching = false;
        pressDown = false;
        movedTooMuch = false;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) ResetPointerState();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) ResetPointerState();
    }

    void OnDisable()
    {
        ResetPointerState();
    }

    void UpdateActiveViewNode()
    {
        if (switchCo != null) StopCoroutine(switchCo);

        GridNode prevNode = currentNode;

        if (graph.TryGetNode(currentCell, out GridNode nextNode))
        {
            currentNode = nextNode;

            currentNode.SetViewActive(true);

            if (pulse != null) pulse.Pulse();

            switchCo = StartCoroutine(DisablePrevAfterDelay(prevNode, deactivatePreviousDelay));
        }
        else
        {
            if (prevNode != null) prevNode.SetViewActive(false);
            currentNode = null;
        }
    }

    IEnumerator DisablePrevAfterDelay(GridNode prev, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);


        if (prev != null && prev != currentNode)
            prev.SetViewActive(false);

        switchCo = null;
    }



    void HandleGridMovement()
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

    Vector3 GetTargetWorldPos(Vector2Int cell)
    {
        float y = keepCameraHeight ? fixedY : transform.position.y;
        return graph.CellToWorldCenter(cell, y);
    }

    Vector2Int ReadCardinalRelativeToCamera(Vector2 input)
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
                return desiredWorld.z > 0 ? Vector2Int.up : Vector2Int.down;
        }
        else
        {
            if (Mathf.Abs(desiredWorld.x) > Mathf.Abs(desiredWorld.y))
                return desiredWorld.x > 0 ? Vector2Int.right : Vector2Int.left;
            else
                return desiredWorld.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }

    void HandleLook()
    {
        if (!isDragging) return;

        if (Pointer.current == null || !Pointer.current.press.isPressed)
        {
            isDragging = false;
            lookDelta = Vector2.zero;
            return;
        }

        if (lookDelta.sqrMagnitude < 0.0001f) return;

        float deltaX = lookDelta.x * lookSensitivity * lookMultiplier * Time.deltaTime;
        float deltaY = lookDelta.y * lookSensitivity * lookMultiplier * Time.deltaTime;

        yaw += deltaX;
        pitch += invertY ? deltaY : -deltaY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        lookDelta = Vector2.zero;
    }

    void HandleMouseWheelZoom()
    {
        if (cam == null || Mouse.current == null) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.fieldOfView -= scroll * mouseWheelZoomSpeed * zoomMultiplier * Time.deltaTime;
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minFov, maxFov);
        }


    }

    void HandlePinchZoom()
    {
        if (cam == null || Touchscreen.current == null) return;

        var touches = Touchscreen.current.touches;

        if (touches.Count < 2)
        {
            wasPinching = false;
            return;
        }

        if (!touches[0].isInProgress || !touches[1].isInProgress)
        {
            wasPinching = false;
            return;
        }

        Vector2 p0 = touches[0].position.ReadValue();
        Vector2 p1 = touches[1].position.ReadValue();
        Vector2 pinchVector = p0 - p1;

        if (wasPinching)
        {
            float delta = pinchVector.magnitude - lastPinchVector.magnitude;
            cam.fieldOfView -= delta * pinchZoomSpeed * zoomMultiplier;
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minFov, maxFov);
        }


        lastPinchVector = pinchVector;
        wasPinching = true;
    }

    bool IsTwoFingerPinching()
    {
        if (Touchscreen.current == null) return false;
        var touches = Touchscreen.current.touches;
        if (touches.Count < 2) return false;
        return touches[0].isInProgress && touches[1].isInProgress;
    }

    void UpdateTapState()
    {
        if (!pressDown) return;
        if (!hasPointerPos) return;
        if (movedTooMuch) return;

        float moved = (pointerPos - pressPos).magnitude;
        if (moved > tapMaxMovePixels) movedTooMuch = true;
    }

    void TryTapMove(Vector2 screenPos)
    {
        if (!enableTapMove) return;
        if (cam == null) return;
        if (IsTwoFingerPinching()) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, tapMaxDistance, tapHitMask, QueryTriggerInteraction.Ignore))
        {
            Vector2Int bestCell = FindClosestCellToPoint(hit.point);
            if (graph.HasNode(bestCell))
            {
                currentCell = bestCell;
                UpdateActiveViewNode();
            }
        }
    }

    Vector2Int FindClosestCellToPoint(Vector3 worldPoint)
    {
        float best = float.PositiveInfinity;
        Vector2Int bestCell = graph.WorldToCell(worldPoint);

        foreach (var kvp in graph.NodesByCell)
        {
            Vector3 nodeCenter = graph.CellToWorldCenter(kvp.Key, keepCameraHeight ? fixedY : worldPoint.y);
            float d = (nodeCenter - worldPoint).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestCell = kvp.Key;
            }
        }

        return bestCell;
    }

    void FallbackTapPolling()
    {
        if (!enableTapMove) return;
        if (cam == null) return;

        if (!hasPointerPos)
        {
            if (Mouse.current != null) { pointerPos = Mouse.current.position.ReadValue(); hasPointerPos = true; }
            else if (Touchscreen.current != null) { pointerPos = Touchscreen.current.primaryTouch.position.ReadValue(); hasPointerPos = true; }
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                pressDown = true;
                pressTime = Time.time;
                pressPos = Mouse.current.position.ReadValue();
                pointerPos = pressPos;
                hasPointerPos = true;
                movedTooMuch = false;
            }

            if (pressDown && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                float held = Time.time - pressTime;
                float moved = (Mouse.current.position.ReadValue() - pressPos).magnitude;
                bool isTap = held <= tapMaxTime && moved <= tapMaxMovePixels;
                pressDown = false;
                if (isTap) TryTapMove(pressPos);
            }
        }

        if (Touchscreen.current != null)
        {
            var t = Touchscreen.current.primaryTouch;
            if (t.press.wasPressedThisFrame)
            {
                pressDown = true;
                pressTime = Time.time;
                pressPos = t.position.ReadValue();
                pointerPos = pressPos;
                hasPointerPos = true;
                movedTooMuch = false;
            }

            if (pressDown && t.press.wasReleasedThisFrame)
            {
                float held = Time.time - pressTime;
                float moved = (t.position.ReadValue() - pressPos).magnitude;
                bool isTap = held <= tapMaxTime && moved <= tapMaxMovePixels;
                pressDown = false;
                if (isTap) TryTapMove(pressPos);
            }
        }
    }

    public void OnLookDelta(InputValue value)
    {
        lookDelta = value.Get<Vector2>();
    }

    public void OnLookPress(InputValue value)
    {
        if (IsTwoFingerPinching()) return;

        bool pressed = value.isPressed;

        if (pressed)
        {
            pressDown = true;
            pressTime = Time.time;
            pressPos = pointerPos;
            movedTooMuch = false;
            isDragging = true;
        }
        else
        {
            float held = Time.time - pressTime;
            bool isTap = held <= tapMaxTime && !movedTooMuch;

            pressDown = false;
            isDragging = false;
            lookDelta = Vector2.zero;

            if (isTap) TryTapMove(pressPos);
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnZoom(InputValue value)
    {
        if (cam == null) return;

        float v = value.Get<float>();
        cam.fieldOfView -= v * mouseWheelZoomSpeed * Time.deltaTime;
        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minFov, maxFov);
    }

    public void OnPoint(InputValue value)
    {
        pointerPos = value.Get<Vector2>();
        hasPointerPos = true;
    }
}

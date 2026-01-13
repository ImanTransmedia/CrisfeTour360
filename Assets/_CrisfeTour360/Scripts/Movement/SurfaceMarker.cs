using UnityEngine;
using UnityEngine.InputSystem;

public class SurfaceMarker : MonoBehaviour
{
    public Camera cam;
    public Transform marker;                 
    public LayerMask hitMask = ~0;           
    public float maxDistance = 200f;
    public float surfaceOffset = 0.02f;      

    [Header("Ray origin")]
    public bool useScreenCenter = true;     
    public Vector2 screenPoint;             

    private void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (!cam || !marker) return;

        Ray ray;
        if (useScreenCenter)
        {
            ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }
        else
        {
            ray = cam.ScreenPointToRay(screenPoint);
        }

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            marker.gameObject.SetActive(true);

            marker.position = hit.point + hit.normal * surfaceOffset;

            marker.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);


        }
        else
        {
            marker.gameObject.SetActive(false);
        }
    }

    public void OnPoint(InputValue value)
    {
        screenPoint = value.Get<Vector2>();
        useScreenCenter = false; 
    }

    public void SetScreenPoint(Vector2 p) => screenPoint = p;
}

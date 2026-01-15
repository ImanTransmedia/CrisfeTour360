using UnityEngine;
using UnityEngine.InputSystem;

public class SurfaceMarker : MonoBehaviour
{
    public Camera cam;
    public Transform marker;
    public LayerMask hitMask = ~0;
    public LayerMask blockMask = ~0;
    public float maxDistance = 200f;
    public float surfaceOffset = 0.02f;

    public bool useScreenCenter = true;
    public Vector2 screenPoint;

    public Material nodeHoverMaterial;
    public bool hideMouseMarkerOnNodeHover = true;

    public Material blockHoverMaterial;

    public bool mobileScreenPointer = true;
    public bool mobileDisablePointer = false;
    public Vector3 mobilePointerScale = new Vector3(2f, 2f, 2f);

    GridNode hoveredNode;

    Renderer markerRenderer;
    Material[] markerOriginalMaterials;
    bool markerHasOriginal;

    bool onBlock;
    Vector3 originalScale;
    bool cachedScale;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        if (marker != null)
        {
            markerRenderer = marker.GetComponentInChildren<Renderer>(true);
            if (markerRenderer != null)
            {
                markerOriginalMaterials = markerRenderer.sharedMaterials;
                markerHasOriginal = markerOriginalMaterials != null && markerOriginalMaterials.Length > 0;
            }

            originalScale = marker.localScale;
            cachedScale = true;
        }
    }

    void LateUpdate()
    {
        if (!cam) return;

        bool isMobile = DeviceDetector.Instance != null && DeviceDetector.Instance.IsMobile;

        if (isMobile && mobileDisablePointer)
        {
            ClearHoverState();
            if (marker != null) marker.gameObject.SetActive(false);
            return;
        }

        Ray ray;
        if (useScreenCenter) ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        else ray = cam.ScreenPointToRay(screenPoint);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            GridNode node = hit.collider != null ? hit.collider.GetComponentInParent<GridNode>() : null;

            if (node != null && nodeHoverMaterial != null)
            {
                if (hoveredNode != node)
                {
                    if (hoveredNode != null) hoveredNode.RestoreMaterial();
                    hoveredNode = node;
                    hoveredNode.SetHoverMaterial(nodeHoverMaterial);
                }

                if (marker != null && hideMouseMarkerOnNodeHover) marker.gameObject.SetActive(false);
                onBlock = false;
                RestoreMarkerMaterial();
                return;
            }
            else
            {
                if (hoveredNode != null)
                {
                    hoveredNode.RestoreMaterial();
                    hoveredNode = null;
                }
            }

            if (marker == null) return;

            marker.gameObject.SetActive(true);

            if (isMobile && mobileScreenPointer)
            {
                if (cachedScale) marker.localScale = mobilePointerScale;
            }
            else
            {
                if (cachedScale) marker.localScale = originalScale;
            }

            if (isMobile && mobileScreenPointer)
            {
                Vector3 p = hit.point + hit.normal * surfaceOffset;
                marker.position = p;
                marker.rotation = cam.transform.rotation;
            }
            else
            {
                marker.position = hit.point + hit.normal * surfaceOffset;
                marker.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }

            bool hitIsBlock = (blockMask.value & (1 << hit.collider.gameObject.layer)) != 0;

            if (hitIsBlock)
            {
                onBlock = true;
                ApplyMarkerMaterial(blockHoverMaterial);
            }
            else
            {
                if (onBlock)
                {
                    onBlock = false;
                    RestoreMarkerMaterial();
                }
            }
        }
        else
        {
            ClearHoverState();
            if (marker != null) marker.gameObject.SetActive(false);
        }
    }

    void ClearHoverState()
    {
        if (hoveredNode != null)
        {
            hoveredNode.RestoreMaterial();
            hoveredNode = null;
        }

        onBlock = false;
        RestoreMarkerMaterial();
    }

    void ApplyMarkerMaterial(Material mat)
    {
        if (markerRenderer == null) return;
        if (mat == null) return;

        var mats = markerRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0) mats = new Material[1];

        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
        markerRenderer.sharedMaterials = mats;
    }

    void RestoreMarkerMaterial()
    {
        if (markerRenderer == null) return;
        if (!markerHasOriginal) return;
        markerRenderer.sharedMaterials = markerOriginalMaterials;
    }

    public void OnPoint(InputValue value)
    {
        screenPoint = value.Get<Vector2>();
        useScreenCenter = false;
    }

    public void SetScreenPoint(Vector2 p)
    {
        screenPoint = p;
    }
}

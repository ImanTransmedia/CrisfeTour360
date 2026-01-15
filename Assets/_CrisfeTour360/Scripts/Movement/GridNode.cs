using UnityEngine;

public class GridNode : MonoBehaviour
{
    public bool snapToGridCenter = true;
    public float gizmoRadius = 0.25f;

    public Renderer nodeMarkerRenderer;

    GameObject viewNode;
    Material[] originalMaterials;
    bool hasOriginal;

    void Awake()
    {
        Transform t = transform.Find("ViewPoint");
        if (t != null) viewNode = t.gameObject;

        if (nodeMarkerRenderer == null)
        {
            Transform m = transform.Find("Marker");
            if (m != null) nodeMarkerRenderer = m.GetComponentInChildren<Renderer>(true);
            if (nodeMarkerRenderer == null && viewNode != null) nodeMarkerRenderer = viewNode.GetComponentInChildren<Renderer>(true);
            if (nodeMarkerRenderer == null) nodeMarkerRenderer = GetComponentInChildren<Renderer>(true);
        }

        if (nodeMarkerRenderer != null)
        {
            originalMaterials = nodeMarkerRenderer.sharedMaterials;
            hasOriginal = originalMaterials != null && originalMaterials.Length > 0;
        }

        SetViewActive(false);
    }

    public void SetViewActive(bool active)
    {
        if (viewNode != null) viewNode.SetActive(active);
    }

    public void SetHoverMaterial(Material hoverMat)
    {
        if (nodeMarkerRenderer == null) return;
        if (hoverMat == null) return;

        var mats = nodeMarkerRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0) mats = new Material[1];

        for (int i = 0; i < mats.Length; i++) mats[i] = hoverMat;
        nodeMarkerRenderer.sharedMaterials = mats;
    }

    public void RestoreMaterial()
    {
        if (nodeMarkerRenderer == null) return;
        if (!hasOriginal) return;
        nodeMarkerRenderer.sharedMaterials = originalMaterials;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
    }
}

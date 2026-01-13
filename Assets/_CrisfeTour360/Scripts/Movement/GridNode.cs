using UnityEngine;

public class GridNode : MonoBehaviour
{
    public bool snapToGridCenter = true;
    public float gizmoRadius = 0.25f;

    private GameObject viewNode;

    private void Awake()
    {
        Transform t = transform.Find("ViewPoint");
        if (t != null)
            viewNode = t.gameObject;

        SetViewActive(false);
    }

    public void SetViewActive(bool active)
    {
        if (viewNode != null)
            viewNode.SetActive(active);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
    }
}

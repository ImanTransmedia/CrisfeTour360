using System.Collections;
using UnityEngine;

public class ViewPointBlurPulse : MonoBehaviour
{
    [Header("Target")]
    public Renderer targetRenderer;

    [Header("Material")]
    public Material blurMaterialTemplate;

    [Header("Shader Property")]
    public string sigmaProperty = "_Sigma";

    [Header("Pulse")]
    public float minSigma = 0.001f;
    public float peakSigma = 1.0f;
    public float upTime = 0.12f;
    public float downTime = 0.18f;

    Material runtimeMat;
    Coroutine pulseCo;

    void Awake()
    {
        if (targetRenderer == null)
        {
            Debug.LogError($"[{name}] No targetRenderer asignado.");
            enabled = false;
            return;
        }

        if (blurMaterialTemplate == null)
        {
            Debug.LogError($"[{name}] No blurMaterialTemplate asignado.");
            enabled = false;
            return;
        }

        // Material instancia (no modifica el asset)
        runtimeMat = new Material(blurMaterialTemplate);
        targetRenderer.material = runtimeMat;

        SetSigma(minSigma);
    }

    void OnEnable()
    {
        Pulse();
    }

    public void Pulse()
    {
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(PulseCoroutine());
    }

    IEnumerator PulseCoroutine()
    {
        yield return LerpSigma(minSigma, peakSigma, upTime);
        yield return LerpSigma(peakSigma, minSigma, downTime);
        pulseCo = null;
    }

    IEnumerator LerpSigma(float from, float to, float time)
    {
        if (time <= 0f)
        {
            SetSigma(to);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / time;
            float s = Mathf.SmoothStep(from, to, t);
            SetSigma(s);
            yield return null;
        }

        SetSigma(to);
    }

    void SetSigma(float v)
    {
        if (runtimeMat != null && runtimeMat.HasProperty(sigmaProperty))
            runtimeMat.SetFloat(sigmaProperty, v);
    }
}
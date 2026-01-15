using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class DebugLogUI : MonoBehaviour
{
    [Header("TMP Outputs")]
    public TMP_Text logsTMP;
    public TMP_Text fpsTMP;
    public TMP_Text deviceTMP;

    [Header("Log Capture")]
    public bool captureErrors = true;
    public bool captureWarnings = true;

    [Header("Logs Settings")]
    [Range(10, 500)]
    public int maxLines = 120;
    public bool includeStackTrace = true;
    public bool showTime = true;

    [Header("FPS Settings")]
    public float fpsSmoothing = 0.1f;
    public float fpsUpdateInterval = 0.25f;

    private readonly Queue<string> _lines = new Queue<string>();
    private StringBuilder _sb = new StringBuilder(4096);

    private float _smoothedDeltaTime;
    private float _fpsTimer;

    private void Awake()
    {
        _smoothedDeltaTime = Time.unscaledDeltaTime;

        if (logsTMP != null)
            logsTMP.gameObject.SetActive(false);

        if (DeviceDetector.Instance == null)
        {
            new GameObject("DeviceDetector").AddComponent<DeviceDetector>();
        }
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
        UpdateDeviceText();
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void Update()
    {
        UpdateFPS();
    }

    private void UpdateDeviceText()
    {
        if (deviceTMP == null) return;

        bool isMobile = DeviceDetector.Instance != null && DeviceDetector.Instance.IsMobile;
        deviceTMP.text = isMobile ? "Device: Mobile" : "Device: Desktop";
    }

    private void UpdateFPS()
    {
        if (fpsTMP == null) return;

        float dt = Time.unscaledDeltaTime;
        _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, dt, fpsSmoothing);

        _fpsTimer += Time.unscaledDeltaTime;
        if (_fpsTimer >= fpsUpdateInterval)
        {
            _fpsTimer = 0f;
            float fps = 1f / Mathf.Max(0.00001f, _smoothedDeltaTime);
            fpsTMP.text = $"FPS: {fps:0.}";
        }
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log)
            return;

        if (type == LogType.Warning && !captureWarnings)
            return;

        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !captureErrors)
            return;

        if (logsTMP != null && !logsTMP.gameObject.activeSelf)
            logsTMP.gameObject.SetActive(true);

        string prefix = type switch
        {
            LogType.Warning => "<color=#ffaa00>[WARN]</color> ",
            LogType.Error => "<color=#ff5555>[ERROR]</color> ",
            LogType.Exception => "<color=#ff3333>[EXCEPTION]</color> ",
            LogType.Assert => "<color=#ff3333>[ASSERT]</color> ",
            _ => ""
        };

        string time = showTime
            ? $"<color=#888888>[{System.DateTime.Now:HH:mm:ss}]</color> "
            : "";

        string line = $"{time}{prefix}{EscapeRichText(condition)}";

        if (includeStackTrace && type != LogType.Warning && !string.IsNullOrEmpty(stackTrace))
        {
            line += $"\n<color=#bbbbbb>{EscapeRichText(stackTrace)}</color>";
        }

        EnqueueLine(line);
        RebuildTMP();
    }

    private void EnqueueLine(string line)
    {
        _lines.Enqueue(line);
        while (_lines.Count > maxLines)
            _lines.Dequeue();
    }

    private void RebuildTMP()
    {
        _sb.Clear();

        foreach (var l in _lines)
        {
            _sb.AppendLine(l);
            _sb.AppendLine();
        }

        logsTMP.text = _sb.ToString();
    }

    private string EscapeRichText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    public void ClearLogs()
    {
        _lines.Clear();
        if (logsTMP != null)
            logsTMP.text = "";
        if (logsTMP != null)
            logsTMP.gameObject.SetActive(false);
    }
}

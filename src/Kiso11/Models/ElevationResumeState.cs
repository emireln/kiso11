namespace Kiso11.Models;

public sealed class ElevationResumeState
{
    public string IsoPath { get; set; } = string.Empty;
    public string OutputIsoPath { get; set; } = string.Empty;
    public OutputMode OutputMode { get; set; }
    public string UsbDriveLetter { get; set; } = string.Empty;
    public int SelectedEditionIndex { get; set; } = 1;
    public int CurrentStep { get; set; } = 1;
    public bool RemoveBloatwareApps { get; set; }
    public bool RemoveAiAndCopilot { get; set; }
    public bool RemoveEdge { get; set; }
    public bool RemoveOneDrive { get; set; }
    public bool BypassTpmAndHardware { get; set; }
    public bool BypassMicrosoftAccount { get; set; }
    public bool DisableBitlockerEncryption { get; set; }
    public bool DisableTelemetryAndAds { get; set; }
    public bool RestoreUserFolders { get; set; }
    public bool IntegrateIntelDrivers { get; set; }
    public bool CompressEsd { get; set; }
}

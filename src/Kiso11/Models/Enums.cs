namespace Kiso11.Models;

public enum OutputMode
{
    IsoFile,
    BootableUsb
}

public enum ProcessState
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum ProcessStage
{
    Initializing,
    MountingIso,
    ExtractingFiles,
    MountingWim,
    DebloatingApps,
    DebloatingAi,
    ApplyingRegistryTweaks,
    ConfiguringBypasses,
    IntegratingDrivers,
    SavingWim,
    GeneratingIso,
    CreatingUsb,
    CleaningUp,
    Finished
}

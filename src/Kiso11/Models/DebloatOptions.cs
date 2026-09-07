using System.ComponentModel;
using Kiso11.ViewModels;

namespace Kiso11.Models;

public class DebloatOptions : BaseViewModel
{
    private bool _removeBloatwareApps = true;
    private bool _removeAiAndCopilot = true;
    private bool _removeEdge = false;
    private bool _removeOneDrive = true;
    private bool _bypassTpmAndHardware = true;
    private bool _bypassMicrosoftAccount = true;
    private bool _disableBitlockerEncryption = true;
    private bool _disableTelemetryAndAds = true;
    private bool _restoreUserFolders = true;
    private bool _integrateIntelDrivers = false;
    private bool _compressEsd = false;

    public bool RemoveBloatwareApps
    {
        get => _removeBloatwareApps;
        set
        {
            if (SetProperty(ref _removeBloatwareApps, value))
                OnPropertyChanged(nameof(RemoveBloatware));
        }
    }
    public bool RemoveBloatware
    {
        get => RemoveBloatwareApps;
        set => RemoveBloatwareApps = value;
    }

    public bool RemoveAiAndCopilot
    {
        get => _removeAiAndCopilot;
        set => SetProperty(ref _removeAiAndCopilot, value);
    }

    public bool RemoveEdge
    {
        get => _removeEdge;
        set => SetProperty(ref _removeEdge, value);
    }

    public bool RemoveOneDrive
    {
        get => _removeOneDrive;
        set => SetProperty(ref _removeOneDrive, value);
    }

    public bool BypassTpmAndHardware
    {
        get => _bypassTpmAndHardware;
        set
        {
            if (SetProperty(ref _bypassTpmAndHardware, value))
                OnPropertyChanged(nameof(BypassHardwareChecks));
        }
    }
    public bool BypassHardwareChecks
    {
        get => BypassTpmAndHardware;
        set => BypassTpmAndHardware = value;
    }

    public bool BypassMicrosoftAccount
    {
        get => _bypassMicrosoftAccount;
        set => SetProperty(ref _bypassMicrosoftAccount, value);
    }

    public bool DisableBitlockerEncryption
    {
        get => _disableBitlockerEncryption;
        set
        {
            if (SetProperty(ref _disableBitlockerEncryption, value))
                OnPropertyChanged(nameof(DisableBitlocker));
        }
    }
    public bool DisableBitlocker
    {
        get => DisableBitlockerEncryption;
        set => DisableBitlockerEncryption = value;
    }

    public bool DisableTelemetryAndAds
    {
        get => _disableTelemetryAndAds;
        set
        {
            if (SetProperty(ref _disableTelemetryAndAds, value))
                OnPropertyChanged(nameof(DisableTelemetry));
        }
    }
    public bool DisableTelemetry
    {
        get => DisableTelemetryAndAds;
        set => DisableTelemetryAndAds = value;
    }

    public bool RestoreUserFolders
    {
        get => _restoreUserFolders;
        set
        {
            if (SetProperty(ref _restoreUserFolders, value))
                OnPropertyChanged(nameof(RestoreClassicFolders));
        }
    }
    public bool RestoreClassicFolders
    {
        get => RestoreUserFolders;
        set => RestoreUserFolders = value;
    }

    public bool IntegrateIntelDrivers
    {
        get => _integrateIntelDrivers;
        set
        {
            if (SetProperty(ref _integrateIntelDrivers, value))
                OnPropertyChanged(nameof(IntegrateIntelRstDrivers));
        }
    }
    public bool IntegrateIntelRstDrivers
    {
        get => IntegrateIntelDrivers;
        set => IntegrateIntelDrivers = value;
    }

    public bool CompressEsd
    {
        get => _compressEsd;
        set
        {
            if (SetProperty(ref _compressEsd, value))
                OnPropertyChanged(nameof(ConvertToEsd));
        }
    }
    public bool ConvertToEsd
    {
        get => CompressEsd;
        set => CompressEsd = value;
    }

    public void ApplyRecommended()
    {
        RemoveBloatwareApps = true;
        RemoveAiAndCopilot = true;
        RemoveEdge = false;
        RemoveOneDrive = true;
        BypassTpmAndHardware = true;
        BypassMicrosoftAccount = true;
        DisableBitlockerEncryption = true;
        DisableTelemetryAndAds = true;
        RestoreUserFolders = true;
        IntegrateIntelDrivers = false;
        CompressEsd = false;
    }
}

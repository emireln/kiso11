using Kiso11.Services;

namespace Kiso11.Models;

public class UsbDriveItem
{
    /// Windows' built-in formatter refuses FAT32 above this size, and UEFI boot needs FAT32.
    public const long Fat32FormatLimitBytes = 32L * 1024 * 1024 * 1024;

    public string DriveLetter { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }

    public bool IsSupported => TotalBytes > 0 && TotalBytes <= Fat32FormatLimitBytes;

    public string TotalSizeFormatted => FormatBytes(TotalBytes);

    public string DisplayName
    {
        get
        {
            var loc = LocalizationService.Instance;
            var label = string.IsNullOrWhiteSpace(VolumeLabel) ? loc.UsbNoLabel : VolumeLabel;
            var text = $"{DriveLetter}  {label}  ·  {TotalSizeFormatted}";
            return IsSupported ? text : $"{text}  ·  {loc.UsbTooLargeNote}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int i = 0;
        double d = bytes;
        while (d >= 1024 && i < suffixes.Length - 1)
        {
            d /= 1024;
            i++;
        }
        return $"{d:0.#} {suffixes[i]}";
    }

    public override string ToString() => DisplayName;
}

namespace Kiso11.Models;

public class UsbDriveItem
{
    public string DriveLetter { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }

    public string TotalSizeFormatted => FormatBytes(TotalBytes);
    public string FreeSizeFormatted => FormatBytes(FreeBytes);

    public string DisplayName
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(VolumeLabel) ? "Sem nome" : VolumeLabel;
            var modelStr = string.IsNullOrWhiteSpace(Model) ? "USB Removível" : Model;
            return $"[{DriveLetter}] {label} ({modelStr} - {TotalSizeFormatted})";
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

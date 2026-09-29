namespace Kiso11.Models;

public sealed class WindowsImageEdition
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Version)
        ? $"{Name}  ·  {Architecture}  ·  Image {Index}"
        : $"{Name}  ·  {Architecture}  ·  {Version}  ·  Image {Index}";

    public override string ToString() => DisplayName;
}

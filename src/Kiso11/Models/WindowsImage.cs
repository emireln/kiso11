using System.Collections.Generic;

namespace Kiso11.Models;

/// One Windows edition (image index) inside install.wim or install.esd.
public sealed record WimEdition(int Index, string Name);

/// What the app learns about an ISO before it changes anything.
public sealed record IsoInfo(bool IsWindowsInstaller, bool UsesEsd, IReadOnlyList<WimEdition> Editions);

using System;
using System.Collections.Generic;

namespace WinHop;

public sealed class Workspace
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Workspace";
    public bool IsExpanded { get; set; } = true;
    public List<string> ProcessNames { get; set; } = new();
}

public sealed class PinnedApp
{
    public string ProcessName { get; set; } = "";
}


# WinHop Documentation

## Overview

WinHop is an Arc-inspired window switcher for Windows. It provides a fast, keyboard-native way to switch between windows, organize them into workspaces, and pin frequently used apps.

## Getting Started

### System Requirements
- Windows 10 or 11
- .NET 9.0 Runtime (included in self-contained build)

### First Launch
1. Run `WinHop.exe`
2. Press `Ctrl+Space` to open the sidebar
3. Start switching windows!

## Features

### Edge Triggers
Hover your cursor at the screen edge to activate WinHop. Configure which edges are active using the `◀` `▶` buttons in the footer.

### Workspaces
Organize windows by project or context:
1. Right-click a window
2. Select **Add to workspace** → choose or create workspace
3. Windows in workspaces are hidden from the main list

Manage workspaces:
- Right-click workspace header → **Rename** or **Delete**
- Right-click window in workspace → **Remove from workspace**

### Pinned Apps
Pin frequently used apps for one-click access:
1. Right-click a window → **Pin to top**
2. Pinned apps appear as icons at the top
3. Right-click pinned icon → **Unpin** to remove

### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Ctrl+Space` | Toggle WinHop |
| `Ctrl+K` | Focus search box |
| `↑` `↓` | Navigate list |
| `Enter` | Activate selected window |
| `Esc` | Hide WinHop |
| `Tab` | Toggle focus between search and list |
| `1-9` | Quick select window by position |

### Closing Windows
- **Middle-click** any window to close it
- **Hover** over a window and click the `×` button

## Configuration

Settings are stored in `%APPDATA%/WinHop/settings.json`:

```json
{
  "TriggerLeft": true,
  "TriggerRight": false,
  "PinnedProcessNames": ["Code", "chrome"],
  "Workspaces": [
    {
      "Id": "...",
      "Name": "Work",
      "IsExpanded": true,
      "ProcessNames": ["Slack", "Outlook"]
    }
  ]
}
```

## Troubleshooting

### WinHop doesn't appear
- Check if another app is using `Ctrl+Space`
- Try the edge trigger instead

### Edge trigger not working
- Ensure edge triggers are enabled (check footer buttons)
- Move cursor slowly to the edge

### Window not showing in list
- WinHop only shows visible, non-minimized windows
- Check if the window is in a workspace

## Building from Source

```bash
# Clone
git clone https://github.com/youruser/winhop.git
cd winhop

# Build debug
dotnet build

# Run
dotnet run --project WinHop/WinHop.csproj

# Publish portable exe
dotnet publish WinHop/WinHop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Architecture

```
WinHop/
├── MainWindow.xaml(.cs)      # Main UI and logic
├── EdgeTriggerWindow.xaml    # Invisible edge trigger
├── EdgeTriggerManager.cs     # Manages edge triggers
├── WindowEnumerator.cs       # Lists open windows
├── Win32.cs                  # Windows API interop
├── IconExtractor.cs          # Window icon extraction
├── WindowInfo.cs             # Window data model
├── AppSettings.cs            # Settings persistence
├── Workspace.cs              # Workspace model
└── Converters.cs             # XAML value converters
```


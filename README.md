# WinHop

A Windows application launcher and window switcher built with WPF. Quickly find and switch between open windows using a keyboard shortcut or edge trigger.

## Features

- **Global Hotkey**: Press `Ctrl+Space` to open the window switcher
- **Edge Trigger**: Hover over the screen edge to activate
- **Search & Filter**: Type to filter windows by title or process name
- **Keyboard Navigation**: 
  - `Enter` to activate selected window
  - `Escape` to close
  - `Arrow Keys` to navigate
- **Auto-hide**: Sidebar automatically hides when you move away

## Requirements

- Windows 10/11
- .NET 9.0 Runtime

## Building

1. Clone the repository
2. Open the project in Visual Studio or use the .NET CLI:
   ```bash
   dotnet build
   ```
3. Run the application:
   ```bash
   dotnet run --project WinHop/WinHop.csproj
   ```

## Usage

1. Launch WinHop
2. Press `Ctrl+Space` or hover over the left edge of your screen
3. Type to search for windows
4. Press `Enter` or double-click to switch to a window
5. Press `Escape` to close the sidebar

## Project Structure

- `MainWindow.xaml.cs` - Main application window and logic
- `EdgeTriggerWindow.xaml.cs` - Edge trigger window for mouse activation
- `WindowEnumerator.cs` - Enumerates open windows
- `Win32.cs` - Windows API interop
- `IconExtractor.cs` - Extracts window icons
- `WindowInfo.cs` - Window information model

## License

[Add your license here]


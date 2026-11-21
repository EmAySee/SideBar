# PhantomCore SideBar

A lightweight, focus-immune Windows 11 Fluent WPF desktop launcher toolbar — part of the **PhantomCore** suite — designed to streamline workspace workflows without stealing focus from active windows.

## Features
- **Windows 11 Fluent Design & Live Theme Sync:** Automatically follows Windows 11 Dark/Light theme (with optional manual override in Settings), including native dark/light DWM title bars, rounded corners, Fluent menus, and a themed system tray menu.
- **Focus-Immune Architecture:** Built using `WS_EX_TOOLWINDOW` and `WS_EX_NOACTIVATE` styles so clicking launchers never steals focus from your active app.
- **Auto-Hide & Top-Edge Snapping:** Docks cleanly at the top edge of the screen with a subtle reveal pill, sliding smoothly into view on hover.
- **Drag & Drop Pinning:** Drag executables, shortcuts, or folders directly onto the toolbar to pin them instantly.
- **Icon Arrangement & Shortcut Manager:** Reorder launchers directly via "Arrange Icons" mode or manage everything in the Fluent Shortcut Manager with live icon previews.
- **Flexible Display Modes:** Switch between `Icons and Text` and `Icons Only` viewing modes from the context menu or Settings.
- **Rich Icon Support:** Supports custom `.ico` / `.png` files, automatic executable & folder icon extraction (including `PATH` commands), and a fast async `shell32.dll` system icon picker.

---

## Getting Started & Installation

### Prerequisites
- Windows 10 / 11
- .NET 10 Desktop Runtime (or publish self-contained)

### Standard Installation
1. Download the latest standalone setup installer (`SideBar_Setup_v*.exe`) from **GitHub Releases**.
2. Run the installer. It installs cleanly to your user profile (`%AppData%\SideBar`) without requiring administrator privileges.
3. Your `toolbar_config.json` is preserved across upgrades.

### Building from Source
Clone the repository and build using Visual Studio or the .NET CLI:
```cmd
dotnet build -c Release
```

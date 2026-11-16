# SideBar

A lightweight, focus-immune custom WPF toolbar application designed to replace heavy taskbar launchers and streamline desktop workspace workflows[cite: 10].

## Features
- **Focus-Immune Architecture:** Built using `WS_EX_TOOLWINDOW` and `WS_EX_NOACTIVATE` styles to remain non-interactive and background-persistent without stealing focus from active windows[cite: 8, 10].
- **Auto-Hide & Edge Snapping:** Automatically docks or hides at the top edge of the screen, revealing itself instantly when the cursor enters the top reveal zone.
- **Drag & Drop Customization:** Instantly pin new applications or folders by dragging and dropping shortcuts or executables directly onto the toolbar surface[cite: 8].
- **Icon Arrangement & Reordering:** Enable "Arrange Icons" mode via the system tray or context menu to easily drag and reorder launcher items[cite: 8].
- **Flexible Display Modes:** Toggle between `IconsAndText` and `IconsOnly` viewing modes via the settings window or context menu[cite: 8, 12].
- **System Tray Integration:** Persistent tray icon with quick access controls for toggling "Always on Top", reloading configuration, accessing settings, or shutting down[cite: 8].
- **Rich Icon Support:** Supports custom `.ico` files, `.png` assets, built-in application icon extraction, and shell32 system icons[cite: 8].

---

## Getting Started & Installation

### Prerequisites
- Windows 10 / 11 (Supports dark/light theme tracking based on Windows system preferences)[cite: 8, 10].
- .NET runtime (or published as self-contained)[cite: 10].

### Standard Installation
1. Download the latest standalone setup installer (`SideBar_Setup_v*.exe`) from **GitHub Releases**.
2. Run the installer. It installs the application cleanly to your user profile (`%appdata%\SideBar`) without requiring administrator privileges[cite: 8].
3. The installer automatically creates a Start Menu shortcut and an optional desktop shortcut, while preserving your configuration files during upgrades[cite: 8].

### Building from Source
Clone the repository and build the solution using Visual Studio 2026 or the .NET CLI[cite: 8, 10]:
```cmd
dotnet build -c Release

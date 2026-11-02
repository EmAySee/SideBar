# SideBar

A lightweight, focus-immune custom WPF toolbar application designed to replace heavy taskbar launchers and streamline desktop workspace workflows.

## Features
- **Focus-Immune Architecture:** Built using `WS_EX_TOOLWINDOW` and `WS_EX_NOACTIVATE` styles to remain non-interactive and background-persistent without stealing focus from active windows.
- **Global Hotkey Support:** Toggles visibility globally using system-wide hotkey bindings (`Shift + Ctrl + Alt + D`).
- **Clean Iconography:** Tailored custom visual themes and branding for core systems and remote connections.
- **Taskbar De-clutterer:** Centralizes frequent app launching into a minimalist, persistent panel.

---

## Getting Started

### Prerequisites
- Windows 10 / 11
- .NET runtime (or publish as self-contained)

### Building from Source
Clone the repository and build the solution using Visual Studio or the .NET CLI:
```cmd
dotnet build -c Release

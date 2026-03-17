# Copa Form GUI – CNC Punching Machine Controller

A complete **.NET 8 WPF** desktop application for controlling a CNC punching machine via a network-connected Delta Tau controller.

## Technology Stack

| Layer | Technology |
|-------|-----------|
| UI Framework | WPF (.NET 8, `net8.0-windows`) |
| Architecture | MVVM via `CommunityToolkit.Mvvm` |
| Dependency Injection | `Microsoft.Extensions.DependencyInjection` |
| Settings Persistence | JSON file (`%APPDATA%\CopaFormGui\settings.json`) |

## Building

```bash
dotnet build CopaFormGui/CopaFormGui.csproj
dotnet run --project CopaFormGui/CopaFormGui.csproj
```

## Default Connection Credentials

| Field | Default |
|-------|---------|
| IP Address | 192.168.0.200 |
| User Name | root |
| Password | deltatau |

---

## Screen Previews

### Screen 1 – Login / Connection
Pre-filled credentials, masked password, **Connect** and **No Device (Offline)** buttons.

![Login Screen](docs/screenshots/screen1_login.png)

---

### Screen 2 – Main Window (Punching Operations Active)
Full menu bar (File / Punching / Database / Tools / Settings / Help), toolbar with icons, axis position display, program list, mode buttons, E-Stop, and green/red controller status indicator.

![Main Window – Punching View](docs/screenshots/screen2_main_punching.png)

---

### Screen 3 – Database
Tool records DataGrid with all columns (ID, Name, Type, Diameter, Length, Stroke Length, Max Strokes, Current Strokes, Status, Notes). Add / Delete / Save / Refresh actions.

![Database Screen](docs/screenshots/screen3_database.png)

---

### Screen 4 – Settings (Speed Settings Tab)
Auto speeds and hand/jog speeds for X, Y, Z axes. Tabbed interface with Positions & Limits and Tool tabs.

![Settings – Speed Settings](docs/screenshots/screen4_settings_speed.png)

---

### Screen 5 – Tool Management
Split layout: tool library DataGrid on the left, detail edit panel on the right. Reset strokes, add/delete tools.

![Tool Management Screen](docs/screenshots/screen5_tool_management.png)

---

### Screen 6 – Alarms & Events
Severity-coloured alarm table (Critical / Error / Warning / Info), active alarms banner, acknowledge selected/all, clear history.

![Alarms & Events Screen](docs/screenshots/screen6_alarms.png)

---

### Screen 7 – Settings (Positions & Limits Tab)
Axis travel limits (min/max for X, Y, Z) and home positions configuration.

![Settings – Positions & Limits](docs/screenshots/screen7_settings_positions.png)

---

## Project Structure

```
CopaFormGui/
├── Models/
│   ├── AlarmRecord.cs
│   ├── ConnectionSettings.cs
│   ├── MachineSettings.cs
│   ├── PunchProgram.cs
│   └── ToolRecord.cs
├── Services/
│   ├── IControllerService.cs / ControllerService.cs
│   └── ISettingsService.cs  / SettingsService.cs
├── ViewModels/
│   ├── LoginViewModel.cs
│   ├── MainViewModel.cs
│   ├── PunchingViewModel.cs
│   ├── DatabaseViewModel.cs
│   ├── SettingsViewModel.cs
│   ├── ToolManagementViewModel.cs
│   └── AlarmViewModel.cs
├── Views/
│   ├── LoginWindow.xaml
│   ├── PunchingView.xaml
│   ├── DatabaseView.xaml
│   ├── SettingsView.xaml
│   ├── ToolManagementView.xaml
│   └── AlarmView.xaml
├── Converters/
│   └── Converters.cs
├── App.xaml          (global styles + DI bootstrap)
└── MainWindow.xaml   (shell with menu, toolbar, status bar)
```

# Simple Graph Form Control

> ⚠️ **Important:** Use this module at your own risk. See the **Disclaimer** section below.

A custom form control module for the [Decisions](https://decisions.com) platform that renders an SVG area/line chart on any form. Display time-series or numeric data with a clean, configurable chart that automatically scales to its container.

## Features

- **SVG area/line chart** — smooth connected line with an optional gradient fill beneath it.
- **Average line** — optional dashed horizontal line drawn at the dataset mean.
- **Data point dots** — optional filled circles rendered at each data value.
- **Configurable color** — a single hex color controls the line, dots, gradient fill, and average line.
- **Y-axis suffix** — append a unit string (e.g. `h`, `%`, `km`) to every Y-axis tick label.
- **X-axis labels** — supply labels from flow data or configure them statically at design time. Labels are automatically thinned when there are more data points than can comfortably fit.
- **Data-driven or static** — Y values and X labels can each be populated from flow data at runtime or configured as static values at design time.
- **Responsive sizing** — the chart fills its parent container and re-renders automatically when the container is resized.
- **Active Form Flow integration** — optionally outputs the Y-value series and/or X labels via form outcome paths.

## Requirements

- Decisions 9.21 or later

## Installation

### Option 1: Install Pre-built Module
1. Download the compiled module (`.zip` file)
2. Log into the Decisions Portal
3. Navigate to **System > Administration > Features**
4. Click **Install Module**
5. Upload the `.zip` file
6. Restart the Decisions service if prompted

### Option 2: Build from Source
See the [Building from Source](#building-from-source) section below.

## Configuration

Once installed, the **Simple Graph** control appears in the form toolbox under *Charts*.

### Data Source

| Property | Description |
|---|---|
| Static Input | When ticked, Y values are taken from the *Static Data* list. When unticked, Y values are read from flow data bound to *Data Name*. |
| Data Name | The form data name that carries the `double[]` Y-value array. Hidden when *Static Input* is ticked. |
| Static Data | Design-time list of `double` values used as Y data. Visible only when *Static Input* is ticked. |

### Appearance

| Property | Description |
|---|---|
| Color | Hex color applied to the line, dots, gradient fill, and average line (default `#3d6fb5`). |
| Y Axis Suffix | String appended to every Y-axis tick label (e.g. `h`, `%`). Leave blank for plain numbers. |
| Show Average Line | Draws a dashed horizontal line at the dataset average. |
| Show Data Points | Draws a filled circle at each data value on the line. |
| Show Area Fill | Renders a gradient fill between the line and the X axis. |
| X Labels From Data | When ticked, X-axis labels are read from flow data bound to *X Labels Data Name*. When unticked, *Static X Labels* are used. |
| X Labels Data Name | The form data name that carries the `string[]` label array. Visible only when *X Labels From Data* is ticked. |
| Static X Labels | Design-time list of label strings. Hidden when *X Labels From Data* is ticked. |

### X-Label Thinning

Labels are automatically skipped when there are more data points than can comfortably fit. The last label is always shown. The skip factor scales with the number of points:

| Data points | Labels shown |
|---|---|
| ≤ 10 | Every label |
| ≤ 20 | Every 2nd |
| ≤ 40 | Every 4th |
| > 40 | Every ⌈n/8⌉th |

### Data Binding with Active Form Flows

The control integrates with Active Form Flows through standard Decisions data binding:

1. Set *Data Name* (and optionally *X Labels Data Name*) in Common Properties and leave *Static Input* unticked.
2. In a flow, use **Set Control Value** to push a `double[]` array (and optionally a `string[]` labels array) into the control at runtime.
3. To pass data through the form, configure one or more output paths on the control's **Outcome** tab and select *Required* or *Optional*. The Y series and/or X labels will be emitted on those paths when the form is submitted.

Output is fully optional — outcome paths default to *Not Used* and the chart works as a display-only control without any output configuration.

## Building from Source

### Prerequisites
- .NET 10.0 SDK or higher
- `CreateDecisionsModule` Global Tool (installed automatically during build)
- Decisions Platform SDK (NuGet package: `DecisionsSDK`)

### Build Steps

#### On Linux/macOS:
```bash
chmod +x build_module.sh
./build_module.sh
```

#### On Windows (PowerShell):
```powershell
.\build_module.ps1
```

#### Manual Build:
```bash
dotnet build build.msbuild
dotnet msbuild build.msbuild -t:build_module
```

### Build Output
The build creates `Decisions.SimpleGraph.zip` in the root directory. Upload it directly to Decisions via **System > Administration > Features**.

## Disclaimer

This module is provided "as is" without warranties of any kind. Use it at your own risk. The authors, maintainers, and contributors disclaim all liability for any direct, indirect, incidental, special, or consequential damages, including data loss or service interruption, arising from the use of this software.

**Important Notes:**
- Always test in a non-production environment first
- This module is not officially supported by Decisions

## License

[MIT](LICENSE)

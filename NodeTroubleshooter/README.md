# NodeTroubleshooter


# NodeTroubleshooter – Node Recovery Wizard (FPGA-STP Platform)

Tracks and guides node-level hardware investigations across work windows.

A menu-driven troubleshooting wizard for Gen6.1 (C2310) and Gen8.1 (C2390) FPGA-STP nodes.

## Features

- **Interactive Wizard**: Select generation ? Select symptom ? Get actionable guidance
- **Stage-Based Investigation**: Stage 0 (domain ID) ? Stage 1 (domain-specific) ? Stage 2 (validation)
- **Session Persistence**: One session file per node, progress survives across work windows
- **CLI Commands**: Quick access to node info, symptoms, and diagnostics
- **Data-Driven**: All node definitions, components, symptoms, and runbooks stored in JSON

## Quick Start

```
# Build
dotnet build

# Run interactive wizard
dotnet run

# Or use CLI commands
dotnet run -- show gen6
dotnet run -- show gen8
dotnet run -- symptom PXE_TIMEOUT
dotnet run -- diag fpga
```

## CLI Commands

| Command | Description |
|---------|-------------|
| `wizard` | Start interactive troubleshooting wizard |
| `show gen6` | Display Gen6.1 (C2310) component inventory and facts |
| `show gen8` | Display Gen8.1 (C2390) component inventory and facts |
| `symptom <CODE>` | Show guidance for a symptom code (e.g., `PXE_TIMEOUT`) |
| `diag <TYPE>` | Run diagnostic runbook (e.g., `diag fpga`) |

### Available Symptom Codes

**Unknown Domain (Stage 0):**
- `NODE_D_STATE` - Node in D State / Unidentified Issue
- `BLADE_IS_NOT_VISIBLE` - Blade Is Not Visible in Rack Manager
- `BMC_UNRESPONSIVE` - BMC Unresponsive / Cannot Connect to BMC

**Power / Infrastructure:**
- `POWER_P1` - Power Issue / PSU / PDU / CMOS

**Platform / Board:**
- `MOTHERBOARD` - Motherboard / Board Replacement
- `BIOS_BMC` - BIOS / BMC Firmware Issue

**PCIe / FPGA / Riser:**
- `FPGA_MISSING_MULTIPLE` - Multiple Storm Peak FPGAs Missing
- `RISER_CARD` - PCIe Riser Card Issue
- `OCULINK` - Oculink Cable Issue

**Network / PXE:**
- `PXE_TIMEOUT` - PXE Boot Timeout / Network Boot Failure
- `NIC_DOWN` - Network Interface Down / Link Failure
- `DAC_CABLE` - DAC Cable Issue / Miswire / Loose Cable

**Storage:**
- `NVME_MISSING` - NVMe SSD Not Detected

**Memory / CPU:**
- `CPU` - CPU Issue / CPU Replacement
- `DIMM` - Memory DIMM Issue / DIMM Replacement

**Management / Process:**
- `ASK_MODE_PENDING` - Ask Mode Pending / Approval Required
- `SOFTWARE` - Software / OS Issue

## Options

| Option | Description |
|--------|-------------|
| `--data <path>` | Override path to `nodespecs.json` (default: `Data/nodespecs.json`) |

### Examples

```
# Use custom knowledge base location
dotnet run -- --data C:\custom\nodespecs.json wizard

# Get help for PXE timeout issues
dotnet run -- symptom PXE_TIMEOUT

# View FPGA diagnostic steps
dotnet run -- diag fpga
```

## Adding New Symptoms

To add a new symptom, edit `Data/nodespecs.json` and add an entry to the `symptoms` array:

```json
{
  "code": "NEW_SYMPTOM_CODE",
  "title": "Human-readable symptom title",
  "appliesTo": ["gen6.1", "gen8.1"],
  "componentIds": ["cpu", "host_mem"],
  "checks": [
    "First check to perform",
    "Second check to perform"
  ],
  "evidence": [
    "Log file to collect",
    "Command output to capture"
  ],
  "runbookIds": ["diag_fpga"]
}
```

### Field Reference

| Field | Description |
|-------|-------------|
| `code` | Unique identifier (used in CLI) |
| `title` | Human-readable description |
| `appliesTo` | Array of generation IDs (`gen6.1`, `gen8.1`) |
| `componentIds` | Array of component IDs to highlight |
| `checks` | Array of actionable troubleshooting steps |
| `evidence` | Array of logs/outputs to collect |
| `runbookIds` | Array of related runbook IDs |

## Adding New Components

Add to the `components` array within the appropriate node in `nodespecs.json`:

```json
{
  "id": "component_id",
  "name": "Full Component Name",
  "category": "Category Name"
}
```

## Adding New Runbooks

Add to the `runbooks` array in `nodespecs.json`:

```json
{
  "id": "runbook_id",
  "title": "Runbook Title",
  "steps": [
    "Step 1",
    "Step 2"
  ]
}
```

## Project Structure

```
NodeTroubleshooter/
??? NodeTroubleshooter.csproj
??? Program.cs                 # CLI entry point and command definitions
??? Core/
?   ??? Commands.cs            # Command implementations
?   ??? SpecLoader.cs          # JSON loading logic
??? Model/
?   ??? SpecDatabase.cs        # Data models
??? Data/
?   ??? nodespecs.json         # Knowledge base (all node/symptom data)
??? README.md
```

## Requirements

- .NET 8.0 SDK or later

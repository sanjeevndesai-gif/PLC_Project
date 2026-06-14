---
name: create-alarm-monitor
user-invocable: true
description: |
  Skill to create, update, and review an alarm-monitoring workflow that maps
  controller variables to alarm signals, captures a baseline, monitors in a
  timer loop, persists history, and exposes acknowledgment operations.
  Use when adding or changing alarm mappings, thresholds, or persistence.
---

# Create Alarm Monitor (SKILL)

**Purpose**

- Provide a repeatable workflow for implementing an alarm-monitoring feature
  that watches controller variables, raises/stores alarms, and supports
  operator acknowledgements.

**When to Use**

- Implementing or updating a WPF/desktop alarm monitor that reads variables
  from a controller service (e.g., `IControllerService`), or when converting
  an ad-hoc implementation into a maintainable workflow.

**Scope & Location**

- Workspace-scoped by default. Place this skill under `.github/skills/create-alarm-monitor/SKILL.md`.

## Step-by-step Workflow

1. Discover signals
   - Define a canonical `AlarmSignal` shape: `DisplayId`, `SignalKey`, `Code`,
     `Description`, `Severity`, `ActiveBelowThreshold?`, `VariableCandidates[]`.

2. Map signals
   - Create a signal map (list) where each entry includes ordered `VariableCandidates`.

3. Baseline capture
   - On connect, perform an initial poll with `captureBaselineOnly=true` to
     populate `_lastSignalStates` without spamming history.

4. Monitoring loop
   - Use a timer (e.g., 300ms) to Poll signals. Use `Dispatcher.InvokeAsync`
     for UI-bound updates.

5. Read variable values
   - For each `VariableCandidate`, call `ReadVariableAsync`; treat first
     successful read as authoritative for that signal.

6. Activation logic
   - If `ActiveBelowThreshold` is set: `isActive = value < threshold`.
   - Otherwise: `isActive = Math.Abs(value) > epsilon`.
   - When a signal transitions true→false or false→true, add/remove from the
     `liveActiveAlarms` dictionary and append an `AlarmRecord` to history.

7. Deactivation logic
   - When a signal clears, mark the most recent non-acknowledged record with
     `IsAcknowledged = true` and `AcknowledgedBy = "System"`.

8. Acknowledge operations
   - Implement `AcknowledgeSelected()` and `AcknowledgeAll()` to let an
     operator clear `_liveActiveAlarms` and mark history records.

9. Persistence
   - Persist alarm history as JSON to `%APPDATA%/CopaFormGui/alarm_history.json`.
   - Use `JsonSerializerOptions { WriteIndented = true }` to produce readable files.

10. UI synchronization
   - Keep `ActiveAlarms` and `HasActiveAlarms` in sync; update `StatusMessage`.

11. Error handling
   - Swallow transient read errors in the polling loop to keep the monitor alive.

## Decision Points & Branching

- Variable candidate selection order: try direct signal key first, then fallbacks.
- Threshold semantics: allow `ActiveBelowThreshold` for low-value triggers
  (e.g., speed near zero) and default to non-zero comparison for digital signals.
- Baseline capture: if `captureBaselineOnly` skip appending history entries.

## Quality Criteria / Checks

- Uses `Dispatcher.InvokeAsync` for UI updates.
- Timer interval is configurable but avoid sub-100ms unless required.
- Persistence succeeds gracefully (create directory, catch exceptions).
- `description` fields for signals are human-readable and unique codes are used.
- Frontmatter in this `SKILL.md` must include `name`, `user-invocable`, and `description`.

## Examples / Prompts to Try

- "Generate an AlarmSignal list for X/Y axis limits and hyd sensors, following
  the `AlarmSignal` shape and including variable fallbacks."
- "Add a new `BUSBAR_PRESENT_SENSE_ERROR` signal with code `BUSBAR_PRESENT_SENSE_ERROR` and description 'INSERT BUSBAR TO START PUNCH'."
- "Refactor polling loop to use CancellationTokenSource instead of a timer."

## Files & Locations

- Recommended: `.github/skills/create-alarm-monitor/SKILL.md` (this file)
- Suggested code targets: `ViewModels/AlarmViewModel.cs`, `Models/AlarmRecord.cs`,
  `Services/IControllerService.cs`.

## Clarifying Questions

- Scope: should this be workspace-shared (`.github/`) or user-local (`{{VSCODE_USER_PROMPTS_FOLDER}}`)?
- Naming conventions: prefer `SNAKE_CASE` or `PascalCase` for `SignalKey`/`Code`?
- Persist format: JSON is default — do you want additional archival or rotation?

---
Once you confirm scope and naming, I can: generate a starter `AlarmSignal` list,
update `AlarmViewModel` snippets, or create prompt templates for adding new signals.

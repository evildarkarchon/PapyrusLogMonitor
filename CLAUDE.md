# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PapyrusLogMonitor is a C# Avalonia application for real-time monitoring of Papyrus script logs from Bethesda games (Fallout 4 and Skyrim). This is a focused port of the Papyrus monitoring functionality from the Python-based CLASSIC tool.

## Specific Porting Scope

The port focuses on two Python files:
- `Code to Port/ClassicLib/Interface/Papyrus.py` and `Code to Port/ClassicLib/PapyrusLog.py` - Business logic for monitoring
- `Code to Port/ClassicLib/Interface/PapyrusDialog.py` - Dialog window UI

### Key Components to Port

1. **PapyrusStats** (dataclass) → C# record
   - Properties: timestamp, dumps, stacks, warnings, errors, ratio
   - Custom equality comparison
   - Hash implementation

2. **PapyrusMonitorWorker** (QObject) → C# ViewModel with INotifyPropertyChanged
   - Real-time monitoring loop
   - Stats parsing from log file
   - Signal emission → Event/Observable pattern

3. **PapyrusMonitorDialog** (QDialog) → Avalonia Window/UserControl
   - Stats display grid
   - Real-time updates
   - Status indicators (✓, ⚠️, ❌)
   - Stop monitoring button

## Essential Commands

### Development
```bash
# Build the solution
dotnet build

# Run the Avalonia application
dotnet run --project PapyrusLogMonitor.Desktop

# Run tests
dotnet test                                    # All tests
dotnet test --logger "console;verbosity=detailed"  # Verbose output

# Clean build artifacts
dotnet clean
```

## Architecture Overview

### Solution Structure
- `PapyrusLog.Core` - Core business logic (PapyrusStats, monitoring logic)
- `PapyrusLogMonitor` - Avalonia UI with MVVM ViewModels
- `PapyrusLogMonitor.Desktop` - Desktop application entry point
- `PapyrusLogMonitor.Core.Tests` - Unit tests

### Technology Stack
- **.NET 8.0** - Target framework
- **Avalonia 11.3.2** - Cross-platform UI framework
- **ReactiveUI** - MVVM framework for reactive bindings
- **System.Reactive** - Observable patterns for real-time updates

## Python to C# Porting Guide

### Type Mappings
- `@dataclass` → C# `record` with init-only properties
- `QObject/QThread` → `INotifyPropertyChanged` + `Task`/`Timer`
- `Signal` → `IObservable<T>` or events
- `Slot` → Method with async/await
- `QDialog` → Avalonia `Window` or `UserControl`

### Key Functionality Mapping

1. **papyrus_logging() function**:
   - Reads Papyrus log file from configured path
   - Counts occurrences of "Dumping Stacks", warnings, errors
   - Returns tuple of (message, dump_count)
   - C# implementation: async method returning `PapyrusStats`

2. **Monitoring Loop**:
   - Python: `while self._should_run` with `QThread.msleep(1000)`
   - C#: `Timer` or `PeriodicTimer` with cancellation token
   - Use `FileSystemWatcher` for more efficient monitoring

3. **Stats Parsing**:
   - Simple string parsing looking for keywords
   - Keep same logic but use C# string methods

4. **UI Updates**:
   - Python: Qt signals → C#: ReactiveUI observables
   - Status colors: Use Avalonia styles/brushes

## Implementation Notes

1. **File Reading**:
   - Python uses `chardet` for encoding detection
   - C#: Use `StreamReader` with encoding detection or UTF-8 fallback
   - Always use `FileShare.ReadWrite` to avoid blocking game

2. **Real-time Updates**:
   - Use `System.Reactive` for throttling UI updates
   - Implement `INotifyPropertyChanged` on ViewModels
   - Use `ObservableAsPropertyHelper` for computed properties

3. **Dialog Window**:
   - Create as UserControl for reusability
   - Use Grid for layout (similar to Qt's QGridLayout)
   - Style with Avalonia's Fluent theme

4. **Error Handling**:
   - Wrap file operations in try-catch
   - Use `IObservable<T>` error handling operators
   - Display errors in UI similar to Python version

## Testing Approach

- Mock file system operations
- Test stats parsing logic independently
- Verify observable emissions with `TestScheduler`
- UI testing with Avalonia.Headless
- **Chosen Testing Framework**: Xunit for unit testing

## Project Entry Point

- The entry-point project is `PapyrusMonitor.Desktop`

## Code Organization Principles

- Interfaces should be put in separate files from their implementations, for organizational purposes, preferably in a dedicated folder

## Build Configuration Considerations

- Code should be able to be compiled with trimming enabled

## Test Project Organization

- Tests for PapyrusMonitor.Core must go in PapyrusMonitor.Core.Tests
- Tests for PapyrusMonitor.Avalonia must go in PapyrusMonitor.Avalonia.Tests
- Strict separation is important because the PapyrusMonitor.Core project, and its tests, will be utilized elsewhere

## Developer Warnings

- Never run `dotnet test` or `dotnet run` with `--no-build`
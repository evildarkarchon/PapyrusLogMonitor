Read-only Reference code available in `Code to Port/`
# Multi-Phase Implementation Plan: Papyrus Monitor C# Avalonia App

## Phase 1: Project Structure & Core Library Setup
- [x] Create solution structure:
  - [x] `PapyrusMonitor.Core` - Class library for business logic
  - [x] `PapyrusMonitor.Avalonia` - Avalonia MVVM UI application
  - [x] `PapyrusMonitor.Core.Tests` - Unit tests for core library
- [x] Set up NuGet packages:
  - [x] Core: `System.Reactive`, `System.IO.Abstractions`
  - [x] Avalonia: `Avalonia`, `Avalonia.ReactiveUI`, `ReactiveUI.Fody`
- [x] Configure `.editorconfig` and code style rules

## Phase 2: Core Business Logic Library
- [x] Create domain models:
  - [x] `PapyrusStats` record with properties (Dumps, Stacks, Warnings, Errors, Ratio, Timestamp)
  - [x] `LogEntry` record for individual log line parsing
  - [x] `MonitoringConfiguration` class for settings
- [x] Implement log parsing engine:
  - [x] `ILogParser` interface with line parsing methods
  - [x] `PapyrusLogParser` implementation with regex patterns
  - [x] Encoding detection support (similar to chardet functionality)
- [x] Create monitoring service:
  - [x] `IPapyrusMonitorService` interface
  - [x] `PapyrusMonitorService` with Observable pattern using System.Reactive
  - [x] File watcher implementation for real-time monitoring
  - [x] Throttling/debouncing to prevent file lock conflicts

## Phase 3: Real-time Monitoring Infrastructure
- [x] Implement file monitoring:
  - [x] `FileSystemWatcher` wrapper with proper error handling
  - [x] Tail-like functionality for reading new log entries
  - [x] Handle file rotation and recreation scenarios
- [x] Add performance optimizations:
  - [x] Buffered reading for large files
  - [x] Incremental parsing (only parse new content)
  - [x] Background thread processing with `Task.Run`
- [x] Create statistics aggregator:
  - [x] Running totals with thread-safe operations
  - [x] Observable stream of `PapyrusStats` updates
  - [x] Configurable update intervals

## Phase 4: MVVM ViewModels with ReactiveUI
- [x] Create base ViewModel infrastructure:
  - [x] `ViewModelBase` with ReactiveObject inheritance
  - [x] `IActivatableViewModel` implementation
- [x] Implement main ViewModels:
  - [x] `MainWindowViewModel` - Application shell
  - [x] `PapyrusMonitorViewModel` - Main monitoring logic
  - [x] `StatisticsViewModel` - Real-time stats display
- [x] Add ReactiveUI features:
  - [x] Commands with `ReactiveCommand.CreateFromTask`
  - [x] Observable properties with `WhenAnyValue`
  - [x] Validation using ReactiveUI validation helpers

## Phase 5: Avalonia UI Implementation
- [x] Create main window:
  - [x] Responsive grid layout
  - [x] Modern dark theme styling
  - [ ] Window state persistence
- [x] Build monitoring view:
  - [x] Statistics cards with live updates
  - [x] Start/Stop toggle button with state indication
  - [x] Timestamp display with auto-refresh
- [x] Add visual enhancements:
  - [x] Progress indicators during file processing
  - [x] Smooth animations for value changes

## Phase 6: Advanced Features
- [x] Implement configuration system:
  - [x] Settings view for log path configuration
  - [x] JSON-based settings persistence
  - [x] Hot-reload of configuration changes
- [x] Create data export functionality:
  - [x] Export statistics to CSV/JSON
  - [x] Session history tracking
  - [ ] Trend analysis graphs

## Phase 7: Testing & Performance
- [ ] Unit tests for Core library:
  - [ ] Log parser test cases
  - [ ] Statistics calculation verification
  - [ ] File monitoring edge cases
- [ ] Integration tests:
  - [ ] End-to-end monitoring scenarios
  - [ ] Performance benchmarks
  - [ ] Memory leak detection
- [ ] UI tests:
  - [ ] ViewModel behavior verification
  - [ ] Command execution tests
  - [ ] Binding validation

## Phase 8: Deployment & Distribution
- [ ] Configure build pipeline:
  - [ ] Self-contained deployment
  - [ ] Platform-specific builds (Windows, Linux, macOS)
  - [ ] Code signing for Windows
- [ ] Create installer:
  - [ ] WiX installer for Windows
  - [ ] AppImage for Linux
  - [ ] DMG for macOS
- [ ] Documentation:
  - [ ] API documentation for Core library
  - [ ] User guide with screenshots
  - [ ] Developer setup instructions

## Key Implementation Notes

### For Real-time Performance:
- Use `FileSystemWatcher` with minimal buffer sizes
- Implement tail-reading to avoid re-parsing entire file
- Use `System.Reactive` throttling to batch UI updates
- Consider memory-mapped files for very large logs

### For Non-obstructive Monitoring:
- Open log files with `FileShare.ReadWrite`
- Implement retry logic for locked files
- Use background threads via `Task.Run`
- Configure process priority to "Below Normal"

### For Reusability:
- Core library should have zero UI dependencies
- Use dependency injection (consider `Microsoft.Extensions.DependencyInjection`)
- Expose monitoring service via NuGet package
- Include comprehensive XML documentation
Read-only Reference code available in `Code to Port/`
# Multi-Phase Implementation Plan: Papyrus Monitor C# Avalonia App

## Phase 1: Project Structure & Core Library Setup
- [ ] Create solution structure:
  - [ ] `PapyrusMonitor.Core` - Class library for business logic
  - [ ] `PapyrusMonitor.Avalonia` - Avalonia MVVM UI application
  - [ ] `PapyrusMonitor.Core.Tests` - Unit tests for core library
- [ ] Set up NuGet packages:
  - [ ] Core: `System.Reactive`, `System.IO.Abstractions`
  - [ ] Avalonia: `Avalonia`, `Avalonia.ReactiveUI`, `ReactiveUI.Fody`
- [ ] Configure `.editorconfig` and code style rules

## Phase 2: Core Business Logic Library
- [ ] Create domain models:
  - [ ] `PapyrusStats` record with properties (Dumps, Stacks, Warnings, Errors, Ratio, Timestamp)
  - [ ] `LogEntry` record for individual log line parsing
  - [ ] `MonitoringConfiguration` class for settings
- [ ] Implement log parsing engine:
  - [ ] `ILogParser` interface with line parsing methods
  - [ ] `PapyrusLogParser` implementation with regex patterns
  - [ ] Encoding detection support (similar to chardet functionality)
- [ ] Create monitoring service:
  - [ ] `IPapyrusMonitorService` interface
  - [ ] `PapyrusMonitorService` with Observable pattern using System.Reactive
  - [ ] File watcher implementation for real-time monitoring
  - [ ] Throttling/debouncing to prevent file lock conflicts

## Phase 3: Real-time Monitoring Infrastructure
- [ ] Implement file monitoring:
  - [ ] `FileSystemWatcher` wrapper with proper error handling
  - [ ] Tail-like functionality for reading new log entries
  - [ ] Handle file rotation and recreation scenarios
- [ ] Add performance optimizations:
  - [ ] Buffered reading for large files
  - [ ] Incremental parsing (only parse new content)
  - [ ] Background thread processing with `Task.Run`
- [ ] Create statistics aggregator:
  - [ ] Running totals with thread-safe operations
  - [ ] Observable stream of `PapyrusStats` updates
  - [ ] Configurable update intervals

## Phase 4: MVVM ViewModels with ReactiveUI
- [ ] Create base ViewModel infrastructure:
  - [ ] `ViewModelBase` with ReactiveObject inheritance
  - [ ] `IActivatableViewModel` implementation
- [ ] Implement main ViewModels:
  - [ ] `MainWindowViewModel` - Application shell
  - [ ] `PapyrusMonitorViewModel` - Main monitoring logic
  - [ ] `StatisticsViewModel` - Real-time stats display
- [ ] Add ReactiveUI features:
  - [ ] Commands with `ReactiveCommand.CreateFromTask`
  - [ ] Observable properties with `WhenAnyValue`
  - [ ] Validation using ReactiveUI validation helpers

## Phase 5: Avalonia UI Implementation
- [ ] Create main window:
  - [ ] Responsive grid layout
  - [ ] Modern dark theme styling
  - [ ] Window state persistence
- [ ] Build monitoring view:
  - [ ] Statistics cards with live updates
  - [ ] Start/Stop toggle button with state indication
  - [ ] Timestamp display with auto-refresh
- [ ] Add visual enhancements:
  - [ ] Progress indicators during file processing
  - [ ] Color-coded severity indicators
  - [ ] Smooth animations for value changes

## Phase 6: Advanced Features
- [ ] Implement configuration system:
  - [ ] Settings view for log path configuration
  - [ ] JSON-based settings persistence
  - [ ] Hot-reload of configuration changes
- [ ] Add notification system:
  - [ ] Threshold alerts (e.g., error count > X)
  - [ ] System tray integration
  - [ ] Toast notifications for critical events
- [ ] Create data export functionality:
  - [ ] Export statistics to CSV/JSON
  - [ ] Session history tracking
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
# PapyrusLogMonitor AI Coding Agent Instructions

## Project Overview

PapyrusLogMonitor is a C# Avalonia application for real-time monitoring of Papyrus script logs from Bethesda games (Fallout 4/Skyrim). It's a modern cross-platform port of Python monitoring functionality, built with MVVM architecture using ReactiveUI and System.Reactive.

## Architecture Patterns

### Clean Architecture with Reactive Patterns
- **Core Layer**: `PapyrusMonitor.Core` - Pure business logic with System.Reactive
- **UI Layer**: `PapyrusMonitor.Avalonia` - MVVM ViewModels with ReactiveUI
- **Entry Point**: `PapyrusMonitor.Desktop` - Application bootstrapping
- **Test Layers**: Separate test projects per component (`*.Tests`)

### Interface-First Design
- All services implement interfaces in `PapyrusMonitor.Core/Interfaces/`
- Interfaces are in separate files from implementations for organization
- Example: `IPapyrusMonitorService` → `PapyrusMonitorService`

### Reactive Observable Patterns
```csharp
// Core pattern: Observable streams for real-time updates
public interface IPapyrusMonitorService : IDisposable
{
    IObservable<PapyrusStats> StatsUpdated { get; }
    IObservable<string> Errors { get; }
    bool IsMonitoring { get; }
}

// ViewModel pattern: ReactiveUI with ObservableAsPropertyHelper
[Reactive] public PapyrusStats? CurrentStats { get; set; }
```

## Critical Development Standards

### Project Structure Requirements
- **Strict separation**: Core tests in `PapyrusMonitor.Core.Tests`, UI tests in `PapyrusMonitor.Avalonia.Tests`
- **Trimming support**: All code must compile with trimming enabled (`EnableTrimAnalyzer=true`)
- **Nullable reference types**: Enabled project-wide with `<Nullable>enable</Nullable>`

### Record-Based Models
```csharp
// Use records for immutable data models with custom equality
public record PapyrusStats(DateTime Timestamp, int Dumps, int Stacks, int Warnings, int Errors, double Ratio)
{
    // Custom equality ignores timestamp/ratio for content comparison
    public virtual bool Equals(PapyrusStats? other) =>
        other is not null && Dumps == other.Dumps && Stacks == other.Stacks && 
        Warnings == other.Warnings && Errors == other.Errors;
}
```

### ReactiveUI ViewModel Patterns
```csharp
// Base class with activation lifecycle
public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; }
    
    protected virtual void HandleActivation(CompositeDisposable disposables) { }
}

// Use [Reactive] attribute for automatic property generation
[Reactive] public bool IsMonitoring { get; set; }
```

## Essential Commands

### Build & Run
```bash
# Build entire solution
dotnet build

# Run desktop application
dotnet run --project PapyrusMonitor.Desktop

# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Clean build artifacts
dotnet clean
```

### Development Workflow
- **IDE**: JetBrains Rider or Visual Studio (configured for Avalonia)
- **Debugging**: Use `dotnet run --project PapyrusMonitor.Desktop` or IDE debugger
- **Hot reload**: Avalonia supports XAML hot reload in debug mode

## File System Integration Patterns

### Non-Blocking File Access
```csharp
// Always use FileShare.ReadWrite to avoid blocking game processes
using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
```

### Dependency Injection with System.IO.Abstractions
- Use `IFileSystem` instead of static `File` methods for testability
- Inject `IFileSystem` in constructors for easy mocking in tests

### Real-time Monitoring Architecture
1. **FileSystemWatcher**: Detects file changes
2. **FileTailReader**: Reads only new content (tail functionality)
3. **PapyrusLogParser**: Parses new lines for statistics
4. **Observable streams**: Emit updates only when stats change

## Testing Patterns

### xUnit with System.IO.Abstractions.TestingHelpers
```csharp
[Fact]
public void ParseLogContent_WithValidData_ReturnsCorrectStats()
{
    // Arrange
    var fileSystem = new MockFileSystem();
    var parser = new PapyrusLogParser(fileSystem);
    
    // Act & Assert
    var stats = parser.ParseLogContent(content);
    Assert.Equal(5, stats.Dumps);
}
```

### ReactiveUI Testing with TestScheduler
```csharp
// Use TestScheduler for testing observable streams
var scheduler = new TestScheduler();
var service = new PapyrusMonitorService(fileSystem, scheduler);
```

## Configuration & Dependencies

### Package Management
- **Core**: `System.Reactive`, `System.IO.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`
- **UI**: `Avalonia 11.3.2`, `Avalonia.ReactiveUI`, `ReactiveUI.Fody`
- **Testing**: `xUnit`, `System.IO.Abstractions.TestingHelpers`

### Build Configuration
- **Target**: .NET 8.0 across all projects
- **Global settings**: `Directory.Build.props` contains shared configuration
- **Documentation**: `GenerateDocumentationFile=true` for XML docs
- **Language**: `LangVersion=latest` for modern C# features

## Key Integration Points

### Log File Processing Pipeline
1. **File monitoring**: `FileWatcher` detects changes via `FileSystemWatcher`
2. **Incremental reading**: `FileTailReader` reads only new content since last position
3. **Pattern matching**: `PapyrusLogParser` counts "Dumping Stacks", warnings, errors
4. **Stats calculation**: Ratio computed as `dumps / (double)stacks`
5. **Change detection**: Only emit updates when statistics actually change

### MVVM Binding Architecture
- **ViewModels**: ReactiveUI-based with `[Reactive]` properties
- **Commands**: `ReactiveCommand.CreateFromTask` for async operations
- **Computed properties**: `ObservableAsPropertyHelper` for derived values
- **Validation**: Use ReactiveUI validation helpers for input validation

## Common Pitfalls

1. **File locking**: Always use `FileShare.ReadWrite` when opening game log files
2. **Memory leaks**: Dispose observables and use `CompositeDisposable` in ViewModels
3. **UI thread**: Use `ObserveOn(RxApp.MainThreadScheduler)` before UI updates
4. **Test isolation**: Mock `IFileSystem` instead of using real files in tests
5. **Trimming compatibility**: Avoid reflection-heavy patterns, use source generators when possible

## Key Reference Files

- `PapyrusMonitor.Core/Models/PapyrusStats.cs`: Core data model with custom equality
- `PapyrusMonitor.Core/Services/PapyrusMonitorService.cs`: Main monitoring orchestration
- `PapyrusMonitor.Avalonia/ViewModels/ViewModelBase.cs`: ReactiveUI base patterns
- `Directory.Build.props`: Project-wide configuration and standards
- `docs/porting-plan.md`: Implementation roadmap and phase tracking

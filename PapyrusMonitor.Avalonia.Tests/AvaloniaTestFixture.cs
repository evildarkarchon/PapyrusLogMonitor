using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;
using Avalonia.Threading;

namespace PapyrusMonitor.Avalonia.Tests;

public class AvaloniaTestFixture : IDisposable
{
    static AvaloniaTestFixture()
    {
        // Initialize Avalonia UI for headless testing - only once
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseReactiveUI()
            .SetupWithoutStarting();
    }

    public AvaloniaTestFixture()
    {
        // Ensure we're on the UI thread
        Dispatcher.UIThread.VerifyAccess();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

public class TestApp : Application
{
    public override void Initialize()
    {
        // Load any required styles/resources
    }
}
using System.Windows;
using MultyAudioOutput.Platform;

namespace MultyAudioOutput;

public partial class App : Application
{
    private const string MutexName = @"Local\MultyAudioOutput.SingleInstance";
    private const string ShowEventName = @"Local\MultyAudioOutput.Show";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => string.Equals(a, "--diag", StringComparison.OrdinalIgnoreCase)))
        {
            Diagnostics.Run();
            Shutdown();
            return;
        }

        // After a self-update the old process may still be shutting down: wait for it.
        bool justUpdated = e.Args.Any(a => string.Equals(a, "--updated", StringComparison.OrdinalIgnoreCase));
        _mutex = new Mutex(false, MutexName);
        bool isFirstInstance;
        try { isFirstInstance = _mutex.WaitOne(justUpdated ? TimeSpan.FromSeconds(15) : TimeSpan.Zero); }
        catch (AbandonedMutexException) { isFirstInstance = true; }
        if (justUpdated) UpdateService.CleanupOldVersion();

        if (!isFirstInstance)
        {
            // Already running: ask the first instance to open its panel and quit.
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ShowEventName);
                existing.Set();
            }
            catch { /* ignore */ }
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled", args.Exception);
            args.Handled = true;
        };

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.BeginInvoke(() => _controller?.ShowFlyout()), null, Timeout.Infinite, false);

        UpdateService.CleanupOldVersion();
        bool minimized = justUpdated || e.Args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
        _controller = new AppController(this);
        _controller.Start(minimized, justUpdated);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _controller?.Dispose();
        if (_mutex != null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}

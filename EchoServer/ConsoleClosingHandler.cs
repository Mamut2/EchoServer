using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class ConsoleClosingHandler
{
    private static bool _exiting;
    public static event EventHandler? OnClosing;

    // Static reference to prevent garbage collection
    private static readonly HandlerRoutine _consoleHandler = ConsoleEventCallback;

    [DllImport("Kernel32", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

    private delegate bool HandlerRoutine(ConsoleCtrlType sig);

    private enum ConsoleCtrlType
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2, // Triggered when closing the window
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }

    // Initialize the handler
    public static void Initialize()
    {
        // Register the console handler
        bool success = SetConsoleCtrlHandler(_consoleHandler, add: true);
        if (!success)
            Console.WriteLine("Failed to register console handler. Error: " + Marshal.GetLastWin32Error());

        // Handle normal exits (e.g., Main() returns)
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => TriggerOnClosing();

        // Handle Ctrl+C
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            TriggerOnClosing();
        };
    }

    // Cleanup logic
    private static void TriggerOnClosing()
    {
        if (_exiting) return;
        _exiting = true;
        OnClosing?.Invoke(null, EventArgs.Empty);
    }

    // Handler for console events
    private static bool ConsoleEventCallback(ConsoleCtrlType eventType)
    {
        if (eventType == ConsoleCtrlType.CTRL_CLOSE_EVENT)
        {
            TriggerOnClosing();

            return true;
        }

        return false;
    }
}
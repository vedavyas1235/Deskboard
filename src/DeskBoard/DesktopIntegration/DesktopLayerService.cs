using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeskBoard.DesktopIntegration;

/// <summary>
/// Service that integrates with Windows desktop layer to show tasks on the desktop
/// </summary>
public class DesktopLayerService
{
    // Windows API constants
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private IntPtr _progman;
    private IntPtr _workerw;
    private Window? _desktopWindow;

    public void AttachToDesktop(Window window)
    {
        _desktopWindow = window;

        // Get the window handle
        var windowInterop = new WindowInteropHelper(window);
        var hwnd = windowInterop.Handle;

        // Find the Progman window
        _progman = FindWindow("Progman", null);

        if (_progman == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("Failed to find Progman window");
            return;
        }

        // Send message to spawn WorkerW behind desktop icons
        IntPtr result = IntPtr.Zero;
        SendMessage(_progman, 0x052C, new IntPtr(0), IntPtr.Zero);

        // Find the WorkerW window
        _workerw = IntPtr.Zero;
        EnumWindows((tophandle, topparamhandle) =>
        {
            IntPtr shellDllDefView = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (shellDllDefView != IntPtr.Zero)
            {
                _workerw = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
            }

            return true;
        }, IntPtr.Zero);

        if (_workerw != IntPtr.Zero)
        {
            // Set window as child of WorkerW
            SetParent(hwnd, _workerw);

            // Make window non-activatable
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Failed to find WorkerW window");
        }
    }

    public void DetachFromDesktop()
    {
        if (_desktopWindow != null)
        {
            var windowInterop = new WindowInteropHelper(_desktopWindow);
            var hwnd = windowInterop.Handle;

            // Remove from desktop layer
            SetParent(hwnd, IntPtr.Zero);

            // Restore normal window style
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }
    }

    // P/Invoke declarations
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
}

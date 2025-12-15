using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace AutoClicker.Platforms.Windows
{
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_S = 0x53;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;

        private IntPtr _windowHandle;
        private Action? _hotkeyCallback;
        private bool _isRegistered;
        private IntPtr _hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _hookProc;
        private bool _ctrlSPressed = false;

        public HotkeyManager()
        {
            // Store delegate to prevent garbage collection
            _hookProc = HookCallback;
        }

        public bool RegisterCtrlSHotkey(Microsoft.UI.Xaml.Window window, Action callback)
        {
            _windowHandle = WindowNative.GetWindowHandle(window);
            _hotkeyCallback = callback;

            // Set up keyboard hook for event-driven approach
            SetupKeyboardHook();
            _isRegistered = _hookId != IntPtr.Zero;

            return _isRegistered;
        }

        public bool RegisterCtrlSHotkey(IntPtr windowHandle, Action callback)
        {
            _windowHandle = windowHandle;
            _hotkeyCallback = callback;

            // Set up keyboard hook for event-driven approach
            SetupKeyboardHook();
            _isRegistered = _hookId != IntPtr.Zero;

            return _isRegistered;
        }

        private void SetupKeyboardHook()
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                if (curModule != null)
                {
                    _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // CRITICAL: Early exit if not processing or not a key event we care about
            if (nCode < 0)
                return CallNextHookEx(_hookId, nCode, wParam, lParam);

            // Only process WM_KEYDOWN events
            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                
                // OPTIMIZATION: Only process 'S' key - ignore all other keys immediately
                if (vkCode == VK_S)
                {
                    // Only check Ctrl state when 'S' is pressed
                    if (!_ctrlSPressed)
                    {
                        // Check if either Ctrl key is pressed
                        bool ctrlPressed = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || 
                                          (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
                        
                        if (ctrlPressed)
                        {
                            _ctrlSPressed = true;
                            
                            // Invoke callback on UI thread
                            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() => 
                            {
                                try
                                {
                                    _hotkeyCallback?.Invoke();
                                }
                                finally
                                {
                                    // Reset flag after callback to allow repeated presses
                                    _ctrlSPressed = false;
                                }
                            });
                        }
                    }
                }
            }

            // Always pass to next hook in chain
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            // Unhook keyboard hook
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _isRegistered = false;
            }
        }
    }
}

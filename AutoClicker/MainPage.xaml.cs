using System.Runtime.InteropServices;

namespace AutoClicker
{
    //dotnet publish -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained true
    public partial class MainPage : ContentPage
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _clickingTask;
        private Random _random = new Random();
        private Dictionary<Guid, Tuple<double, double>> SpeedOptions = new Dictionary<Guid, Tuple<double, double>>();
        int clickTestAmount = 0;
        private bool _isClicking;
        public bool IsClicking
        {
            get => _isClicking;
            set
            {
                if (_isClicking != value)
                {
                    _isClicking = value;
                    OnPropertyChanged(); // notify binding system
                }
            }
        }

#if WINDOWS
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

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
        private const int WM_KEYUP = 0x0101;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const int VK_S = 0x53;
        private const int VK_CONTROL = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;

        private IntPtr _hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _hookProc;
        private bool _ctrlSPressed = false;
#endif

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
            SlowRadio.CheckedChanged += OnRadioCheckedChanged;
            MediumRadio.CheckedChanged += OnRadioCheckedChanged;
            FastRadio.CheckedChanged += OnRadioCheckedChanged;
            SpeedOptions.Add(SlowRadio.Id, new Tuple<double, double>(27500, 57900));
            SpeedOptions.Add(MediumRadio.Id, new Tuple<double, double>(880, 1450));
            SpeedOptions.Add(FastRadio.Id, new Tuple<double, double>(85, 220));

            MediumRadio.IsChecked = true;
            
            // Apply current system theme immediately
            ApplySystemTheme();
            
            // Listen for future theme changes
            Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;

#if WINDOWS
            _hookProc = HookCallback;
            SetupKeyboardHook();
#endif

            MinimumEntry.TextChanged += (s, e) => UpdateSecondsLabels();
            MaximumEntry.TextChanged += (s, e) => UpdateSecondsLabels();
        }

        private void ApplySystemTheme()
        {
            // Get the current system theme and apply it
            var currentTheme = Application.Current?.RequestedTheme ?? AppTheme.Light;
            Application.Current!.UserAppTheme = currentTheme;
        }

        private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
        {
            // MAUI automatically updates AppThemeBinding values
            // This event fires when the system theme changes
            System.Diagnostics.Debug.WriteLine($"Theme changed to: {e.RequestedTheme}");
            Application.Current!.UserAppTheme = e.RequestedTheme;
        }

        private void OnRadioCheckedChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (sender == null) return;
            var radio = (RadioButton)sender;
            if (e.Value) // true when this one is checked
            {

                MinimumEntry.SetValue(Entry.TextProperty, SpeedOptions[radio.Id].Item1);
                MaximumEntry.SetValue(Entry.TextProperty, SpeedOptions[radio.Id].Item2);
                
                // Update the seconds labels
                UpdateSecondsLabels();
            }
        }

        private void UpdateSecondsLabels()
        {
            // Update Minimum seconds label
            if (double.TryParse(MinimumEntry.Text, out double minimum))
            {
                double minimumSeconds = minimum / 1000.0;
                MinimumSecondsLabel.Text = $"{minimumSeconds:F2} (s)";
            }
            else
            {
                MinimumSecondsLabel.Text = "0 (s)";
            }

            // Update Maximum seconds label
            if (double.TryParse(MaximumEntry.Text, out double maximum))
            {
                double maximumSeconds = maximum / 1000.0;
                MaximumSecondsLabel.Text = $"{maximumSeconds:F2} (s)";
            }
            else
            {
                MaximumSecondsLabel.Text = "0 (s)";
            }
        }

        private void OnTestButtonClicked(object sender, EventArgs e)
        {
            clickTestAmount++;
            ClickTestLabel.Text = $"{clickTestAmount}";
        }

        private void OnResetButtonClicked(object sender, EventArgs e)
        {
            clickTestAmount = 0;
            ClickTestLabel.Text = $"{clickTestAmount}";
        }
        
#if WINDOWS
        private void SetupKeyboardHook()
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(curModule!.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    
                    // Check for key down
                    if (wParam == (IntPtr)WM_KEYDOWN && vkCode == VK_S)
                    {
                        bool ctrlPressed = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
                        
                        if (ctrlPressed && !_ctrlSPressed)
                        {
                            _ctrlSPressed = true;
                            
                            // Toggle between start and stop
                            if (IsClicking)
                                MainThread.BeginInvokeOnMainThread(() => OnStopClicked(null, EventArgs.Empty));
                            else
                                MainThread.BeginInvokeOnMainThread(() => OnStartClicked(null, EventArgs.Empty));
                        }
                    }
                    // Check for S key release
                    else if (wParam == (IntPtr)WM_KEYUP && vkCode == VK_S)
                    {
                        _ctrlSPressed = false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hook error: {ex}");
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

#endif

        private async void OnStartClicked(object? sender, EventArgs e)
        {
            if (IsClicking)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(MinimumEntry.Text) || string.IsNullOrWhiteSpace(MaximumEntry.Text))
            {
                await DisplayAlertAsync("Invalid Input", "Please enter valid numeric values for Minimum and Maximum.", "OK");
                return;
            }

            if (!double.TryParse(MinimumEntry.Text, out double minimum) || !double.TryParse(MaximumEntry.Text, out double maximum))
            {
                await DisplayAlertAsync("Invalid Input", "Please enter valid numeric values for Minimum and Maximum.", "OK");
                return;
            }

            if (minimum < 0 || maximum < 0 || minimum > maximum)
            {
                await DisplayAlertAsync("Invalid Range", "Minimum must be less than or equal to Maximum, and both must be non-negative.", "OK");
                return;
            }

            IsClicking = true;
            _cancellationTokenSource = new CancellationTokenSource();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            
            _clickingTask = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (!int.TryParse(MinimumEntry.Text, out int minimum) || !int.TryParse(MaximumEntry.Text, out int maximum))
                    {
                        continue;
                    }
                    if (minimum < 0 || maximum < 0 || minimum > maximum)
                    {
                        continue;
                    }

                    int delay = _random.Next(minimum, maximum + 1);

                    System.Diagnostics.Debug.WriteLine(delay);
                    try
                    {
                        await Task.Delay(delay, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        _ = PerformMouseClick();
                    }
                }
            });

            await _clickingTask;

            IsClicking = false;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void OnStopClicked(object? sender, EventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        private async Task PerformMouseClick()
        {
#if WINDOWS
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            if (DoubleClickCheckBox.IsChecked)
            {
                await Task.Delay(_random.Next(65, 125));
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
#endif
        }

#if WINDOWS
        ~MainPage()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
            }
        }
#endif
    }
}

using System.Runtime.InteropServices;

namespace AutoClicker
{
    //dotnet publish -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained true
    public partial class MainPage : ContentPage
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _clickingTask;
        private Random _random = Random.Shared;
        private Dictionary<Guid, Tuple<double, double>> SpeedOptions = new Dictionary<Guid, Tuple<double, double>>();
        int clickTestAmount = 0;
        private bool _isClicking;
        private bool _isUpdatingLabels = false;
        private CancellationTokenSource? _debounceTokenSource;
        private string _lastMinimumLabelText = string.Empty;
        private string _lastMaximumLabelText = string.Empty;

#if WINDOWS
        private Platforms.Windows.HotkeyManager? _hotkeyManager;
#endif
        
        public bool IsClicking
        {
            get => _isClicking;
            set
            {
                if (_isClicking != value)
                {
                    _isClicking = value;
                    OnPropertyChanged();
                }
            }
        }

#if WINDOWS
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
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
            SpeedOptions.Add(FastRadio.Id, new Tuple<double, double>(110, 180));

            MediumRadio.IsChecked = true;

            MinimumEntry.TextChanged += OnEntryTextChanged;
            MaximumEntry.TextChanged += OnEntryTextChanged;

#if WINDOWS
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
#endif
        }

#if WINDOWS
        private void OnPageLoaded(object? sender, EventArgs e)
        {
            // Register Ctrl+S hotkey
            if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window)
            {
                _hotkeyManager = new Platforms.Windows.HotkeyManager();
                _hotkeyManager.RegisterCtrlSHotkey(window, OnHotkeyPressed);
            }
        }

        private void OnPageUnloaded(object? sender, EventArgs e)
        {
            _hotkeyManager?.Dispose();
            _hotkeyManager = null;
        }

        private void OnHotkeyPressed()
        {
            // Toggle clicking state
            if (IsClicking)
                OnStopClicked(null, EventArgs.Empty);
            else
                OnStartClicked(null, EventArgs.Empty);
        }
#endif

        private void OnEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Only update if text actually changed, not just selection
            if (e.NewTextValue != e.OldTextValue)
            {
                UpdateSecondsLabelsDebounced();
            }
        }

        private void OnRadioCheckedChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (sender == null) return;
            var radio = (RadioButton)sender;
            if (e.Value)
            {
                MinimumEntry.Text = SpeedOptions[radio.Id].Item1.ToString();
                MaximumEntry.Text = SpeedOptions[radio.Id].Item2.ToString();
                UpdateSecondsLabels();
            }
        }

        private async void UpdateSecondsLabelsDebounced()
        {
            // Cancel any pending update
            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();
            var token = _debounceTokenSource.Token;

            try
            {
                // Wait 150ms before updating (debounce)
                await Task.Delay(150, token);
                
                if (!token.IsCancellationRequested)
                {
                    UpdateSecondsLabels();
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when debouncing
            }
        }

        private void UpdateSecondsLabels()
        {
            // Prevent concurrent updates
            if (_isUpdatingLabels) return;
            
            _isUpdatingLabels = true;
            
            try
            {
                // Update Minimum seconds label
                string newMinimumText;
                if (double.TryParse(MinimumEntry.Text, out double minimum))
                {
                    double minimumSeconds = minimum / 1000.0;
                    newMinimumText = $"{minimumSeconds:F2} (s)";
                }
                else
                {
                    newMinimumText = "0 (s)";
                }

                // Only update if changed
                if (_lastMinimumLabelText != newMinimumText)
                {
                    MinimumSecondsLabel.Text = newMinimumText;
                    _lastMinimumLabelText = newMinimumText;
                }

                // Update Maximum seconds label
                string newMaximumText;
                if (double.TryParse(MaximumEntry.Text, out double maximum))
                {
                    double maximumSeconds = maximum / 1000.0;
                    newMaximumText = $"{maximumSeconds:F2} (s)";
                }
                else
                {
                    newMaximumText = "0 (s)";
                }

                // Only update if changed
                if (_lastMaximumLabelText != newMaximumText)
                {
                    MaximumSecondsLabel.Text = newMaximumText;
                    _lastMaximumLabelText = newMaximumText;
                }
            }
            finally
            {
                _isUpdatingLabels = false;
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
                await Task.Delay(_random.Next(40, 80));
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
#endif
        }
    }
}

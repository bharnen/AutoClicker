using System.Runtime.InteropServices;

namespace AutoClicker
{
    //dotnet publish -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained true
    public partial class MainPage : ContentPage
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _clickingTask;
        private Random _random = Random.Shared;
        int clickTestAmount = 0;
        private bool _isClicking;
        private bool _isUpdatingLabels = false;
        private CancellationTokenSource? _debounceTokenSource;
        private string _lastMinimumLabelText = string.Empty;
        private string _lastMaximumLabelText = string.Empty;
        
        // Cache parsed values to avoid UI thread access from background
        private volatile int _cachedMinimum = 880;
        private volatile int _cachedMaximum = 1450;

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
                // Update cached values for background thread access
                if (sender == MinimumEntry && int.TryParse(e.NewTextValue, out int min))
                {
                    _cachedMinimum = min;
                }
                else if (sender == MaximumEntry && int.TryParse(e.NewTextValue, out int max))
                {
                    _cachedMaximum = max;
                }
                
                UpdateSecondsLabelsDebounced();
            }
        }

        private void OnRadioCheckedChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (sender == null || !e.Value) return;
            
            var radio = (RadioButton)sender;
            
            // Direct object reference comparison - fastest possible approach
            (int min, int max) = GetSpeedRange(radio);
            
            MinimumEntry.Text = min.ToString();
            MaximumEntry.Text = max.ToString();
            UpdateSecondsLabels();
        }

        private (int min, int max) GetSpeedRange(RadioButton radio)
        {
            // Compare RadioButton object references directly
            if (ReferenceEquals(radio, SlowRadio))
                return (27500, 57900);
            else if (ReferenceEquals(radio, MediumRadio))
                return (880, 1450);
            else if (ReferenceEquals(radio, FastRadio))
                return (110, 180);
            
            return (880, 1450); // Default to Medium
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
                    // Use string.Create for allocation-efficient formatting
                    newMinimumText = string.Create(null, stackalloc char[32], $"{minimumSeconds:F2} (s)");
                }
                else
                {
                    newMinimumText = "0.00 (s)";
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
                    // Use string.Create for allocation-efficient formatting
                    newMaximumText = string.Create(null, stackalloc char[32], $"{maximumSeconds:F2} (s)");
                }
                else
                {
                    newMaximumText = "0.00 (s)";
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
            // Use ToString() to avoid boxing allocation
            ClickTestLabel.Text = clickTestAmount.ToString();
        }

        private void OnResetButtonClicked(object sender, EventArgs e)
        {
            clickTestAmount = 0;
            // Pre-cached common value
            ClickTestLabel.Text = "0";
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

            // Cache initial values before starting background task
            _cachedMinimum = (int)minimum;
            _cachedMaximum = (int)maximum;

            IsClicking = true;
            _cancellationTokenSource = new CancellationTokenSource();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            
            _clickingTask = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Use cached values instead of accessing UI properties
                    int minimum = _cachedMinimum;
                    int maximum = _cachedMaximum;
                    
                    if (minimum < 0 || maximum < 0 || minimum > maximum)
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);
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

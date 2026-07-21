using System;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutoClickerApp
{
    public class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private CancellationTokenSource? _clickCts;
        private bool _isRunning = false;
        private readonly Random _random = new();

        // Keybind Settings
        private Key _currentKey = Key.F6;
        private int _currentVk = 0x75; // Virtual Key for F6
        private bool _isListeningForKey = false;

        // UI Controls
        private TextBlock _cpsText = null!;
        private Slider _cpsSlider = null!;
        private CheckBox _randomizeCheck = null!;
        private CheckBox _soundCheck = null!;
        private RadioButton _leftClickRadio = null!;
        private RadioButton _rightClickRadio = null!;
        private RadioButton _toggleRadio = null!;
        private RadioButton _holdRadio = null!;
        private Button _keybindBtn = null!;
        private TextBlock _statusText = null!;
        private Ellipse _statusDot = null!;
        private Button _startButton = null!;

        public MainWindow()
        {
            Title = "RaidenClicks";
            Height = 560;
            Width = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanMinimize;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D0D11"));
            Foreground = Brushes.White;
            FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");

            Content = BuildUi();
            KeyDown += MainWindow_KeyDown;
            Task.Run(GlobalHotkeyLoop);
        }

        private UIElement BuildUi()
        {
            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Header with RaidenClicks Branding & Watermark
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            
            var titleStack = new StackPanel();
            var titleText = new TextBlock 
            { 
                Text = "RAIDENCLICKS", 
                FontSize = 20, 
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A855F7"))
            };
            var subtitleText = new TextBlock 
            { 
                Text = "High-Performance Auto Clicker", 
                FontSize = 11, 
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(titleText);
            titleStack.Children.Add(subtitleText);

            var watermarkText = new TextBlock
            {
                Text = "Made by Merakk",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C084FC")),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };

            headerGrid.Children.Add(titleStack);
            headerGrid.Children.Add(watermarkText);
            Grid.SetRow(headerGrid, 0);
            mainGrid.Children.Add(headerGrid);

            // 2. Main Control Cards Body
            var bodyStack = new StackPanel();

            // CPS Card
            var cpsCard = CreateCard();
            var cpsStack = new StackPanel();
            
            var cpsHeader = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            cpsHeader.Children.Add(new TextBlock { Text = "CLICK SPEED", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")) });
            _cpsText = new TextBlock { Text = "10 CPS", HorizontalAlignment = HorizontalAlignment.Right, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C084FC")) };
            cpsHeader.Children.Add(_cpsText);
            cpsStack.Children.Add(cpsHeader);

            _cpsSlider = new Slider
            {
                Minimum = 1,
                Maximum = 100,
                Value = 10,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 12)
            };
            _cpsSlider.ValueChanged += (s, e) => { if (_cpsText != null) _cpsText.Text = $"{Math.Round(e.NewValue)} CPS"; };
            cpsStack.Children.Add(_cpsSlider);

            _randomizeCheck = new CheckBox
            {
                Content = "Randomize CPS Variance (±20%)",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")),
                IsChecked = true,
                Margin = new Thickness(0, 0, 0, 6)
            };
            cpsStack.Children.Add(_randomizeCheck);

            _soundCheck = new CheckBox
            {
                Content = "Play Audio Cue on Toggle",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")),
                IsChecked = true
            };
            cpsStack.Children.Add(_soundCheck);

            cpsCard.Child = cpsStack;
            bodyStack.Children.Add(cpsCard);

            // Mouse Action & Keybind Card
            var configCard = CreateCard();
            var configStack = new StackPanel();

            configStack.Children.Add(new TextBlock { Text = "CONFIGURATION & KEYBIND", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")), Margin = new Thickness(0, 0, 0, 12) });

            // Mouse Button Selector
            var buttonSelectRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            buttonSelectRow.Children.Add(new TextBlock { Text = "Mouse Button:", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")) });

            var radioStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _leftClickRadio = new RadioButton { Content = "Left", IsChecked = true, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")), Margin = new Thickness(0, 0, 12, 0) };
            _rightClickRadio = new RadioButton { Content = "Right", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")) };
            radioStack.Children.Add(_leftClickRadio);
            radioStack.Children.Add(_rightClickRadio);
            buttonSelectRow.Children.Add(radioStack);
            configStack.Children.Add(buttonSelectRow);

            // Keybind Input
            var keybindRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            keybindRow.Children.Add(new TextBlock { Text = "Hotkey:", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")) });

            _keybindBtn = new Button
            {
                Content = "F6",
                Width = 90,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0)
            };
            _keybindBtn.Click += KeybindBtn_Click;
            keybindRow.Children.Add(_keybindBtn);
            configStack.Children.Add(keybindRow);

            // Activation Mode
            _toggleRadio = new RadioButton
            {
                Content = "Toggle Mode (Press once to Start/Stop)",
                IsChecked = true,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _holdRadio = new RadioButton
            {
                Content = "Hold Mode (Active while Key Held)",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB"))
            };
            configStack.Children.Add(_toggleRadio);
            configStack.Children.Add(_holdRadio);

            configCard.Child = configStack;
            bodyStack.Children.Add(configCard);

            Grid.SetRow(bodyStack, 1);
            mainGrid.Children.Add(bodyStack);

            // 3. Bottom Action / Status Card
            var actionCard = CreateCard();
            actionCard.Margin = new Thickness(0);
            actionCard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#13111C"));

            var actionGrid = new Grid();
            var statusStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _statusDot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
                Margin = new Thickness(0, 0, 8, 0)
            };

            _statusText = new TextBlock
            {
                Text = "IDLE",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF")),
                FontSize = 13
            };

            statusStack.Children.Add(_statusDot);
            statusStack.Children.Add(_statusText);

            _startButton = new Button
            {
                Content = "START",
                Width = 110,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA")),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0)
            };
            _startButton.Click += (s, e) => ToggleClicker();

            actionGrid.Children.Add(statusStack);
            actionGrid.Children.Add(_startButton);
            actionCard.Child = actionGrid;

            Grid.SetRow(actionCard, 2);
            mainGrid.Children.Add(actionCard);

            return mainGrid;
        }

        private Border CreateCard()
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161420")),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#262235")),
                BorderThickness = new Thickness(1)
            };
        }

        private void KeybindBtn_Click(object sender, RoutedEventArgs e)
        {
            _isListeningForKey = true;
            _keybindBtn.Content = "Press Key...";
            _keybindBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isListeningForKey) return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                _isListeningForKey = false;
                _keybindBtn.Content = _currentKey.ToString();
                _keybindBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA"));
                return;
            }

            _currentKey = key;
            _currentVk = KeyInterop.VirtualKeyFromKey(key);
            _keybindBtn.Content = key.ToString();
            _keybindBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA"));
            _isListeningForKey = false;
        }

        private void ToggleClicker()
        {
            _isRunning = !_isRunning;
            UpdateUiState(_isRunning);

            bool playSound = false;
            Dispatcher.Invoke(() => playSound = _soundCheck.IsChecked ?? false);

            if (playSound)
            {
                SystemSounds.Asterisk.Play();
            }

            if (_isRunning)
            {
                _clickCts = new CancellationTokenSource();
                Task.Run(() => ClickLoop(_clickCts.Token));
            }
            else
            {
                _clickCts?.Cancel();
            }
        }

        private void UpdateUiState(bool active)
        {
            Dispatcher.Invoke(() =>
            {
                _statusText.Text = active ? "RUNNING" : "IDLE";
                _statusText.Foreground = active 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));

                _statusDot.Fill = active 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

                _startButton.Content = active ? "STOP" : "START";
                _startButton.Background = active 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9333EA"));
            });
        }

        private async Task ClickLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                double targetCps = 10;
                bool randomize = false;
                bool isLeftClick = true;

                Dispatcher.Invoke(() =>
                {
                    targetCps = _cpsSlider.Value;
                    randomize = _randomizeCheck.IsChecked ?? false;
                    isLeftClick = _leftClickRadio.IsChecked ?? true;
                });

                double intervalMs = 1000.0 / targetCps;

                if (randomize)
                {
                    double variance = 0.20 * intervalMs;
                    double offset = (_random.NextDouble() * 2 * variance) - variance;
                    intervalMs = Math.Max(1, intervalMs + offset);
                }

                uint downFlag = isLeftClick ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN;
                uint upFlag = isLeftClick ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP;

                mouse_event(downFlag, 0, 0, 0, 0);
                mouse_event(upFlag, 0, 0, 0, 0);

                try
                {
                    await Task.Delay((int)intervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task GlobalHotkeyLoop()
        {
            bool wasPressed = false;

            while (true)
            {
                if (!_isListeningForKey)
                {
                    short keyState = GetAsyncKeyState(_currentVk);
                    bool isPressed = (keyState & 0x8000) != 0;

                    bool isToggleMode = false;
                    Dispatcher.Invoke(() => isToggleMode = _toggleRadio.IsChecked ?? true);

                    if (isToggleMode)
                    {
                        if (isPressed && !wasPressed)
                        {
                            Dispatcher.Invoke(ToggleClicker);
                        }
                    }
                    else
                    {
                        if (isPressed && !_isRunning)
                        {
                            Dispatcher.Invoke(ToggleClicker);
                        }
                        else if (!isPressed && _isRunning)
                        {
                            Dispatcher.Invoke(ToggleClicker);
                        }
                    }

                    wasPressed = isPressed;
                }

                await Task.Delay(50);
            }
        }
    }
}
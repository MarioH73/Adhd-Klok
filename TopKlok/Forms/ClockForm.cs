using System.Globalization;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using Windows.Devices.Geolocation;

namespace TopKlok;

internal sealed class ClockForm : Form, IMessageFilter
{
    private const int WindowSize = 420;
    private const int Center = WindowSize / 2;
    private const int ClockRadius = 150;
    private static readonly Color ClockAccentLight = Color.FromArgb(227, 212, 209);
    private static readonly Color ClockAccentDark = Color.FromArgb(112, 105, 103);
    private const string WindowsStartupKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string WindowsStartupValueName = "AdhdKlok";
    private static readonly Rectangle MessageArea = new(32, WindowSize - 50, WindowSize - 64, 42);
    private static readonly string[] AlarmFiles = ["Alarm-01.wav", "Alarm-02.wav", "Alarm-03.wav", "Alarm-04.wav"];
    private static readonly Color BackgroundColor = Color.FromArgb(10, 10, 35);
    private static readonly Color ClockColor = Color.FromArgb(183, 190, 255);
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly IntPtr BroadcastWindow = new(0xffff);
    private const uint SystemCommandMessage = 0x0112;
    private const nuint MonitorPowerCommand = 0xF170;
    private const nuint SendMessageAbortIfHung = 0x0002;
    private const int LeftButtonDownMessage = 0x0201;
    private const int LeftButtonUpMessage = 0x0202;
    private const uint PlaySoundAsync = 0x0001;
    private const uint PlaySoundNoDefault = 0x0002;
    private const uint PlaySoundAlias = 0x00010000;
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _weatherTimer = new() { Interval = 600_000 };
    private readonly System.Windows.Forms.Timer _menuAnimationTimer = new() { Interval = 15 };
    private readonly ToolTip _toolTip = new();
    private readonly Image _settingsIcon;
    private readonly Image _lightSettingsIcon;
    private readonly Image _pinnedSettingsIcon;
    private readonly Image _lightPinnedSettingsIcon;
    private readonly string _themePath = Path.Combine(AppContext.BaseDirectory, "theme.txt");
    private readonly string _weatherLocationPath = Path.Combine(AppContext.BaseDirectory, "weather-location.txt");
    private readonly string _pinPath = Path.Combine(AppContext.BaseDirectory, "clock-pinned.txt");
    private readonly string _pauseDurationPath = Path.Combine(AppContext.BaseDirectory, "pause-duration.txt");
    private readonly string _screenOffDuringPausePath = Path.Combine(AppContext.BaseDirectory, "screen-off-during-pause.txt");
    private readonly string _clickSoundPath = Path.Combine(AppContext.BaseDirectory, "click-sound.txt");
    private System.Media.SoundPlayer? _timerSound;
    private ContextMenuStrip? _pendingMenu;
    private ContextMenuStrip? _openMenu;
    private MenuAnimationState _menuAnimationState;
    private Size _menuExpandedSize;
    private bool _menuIsShown;
    private readonly string _positionPath = Path.Combine(AppContext.BaseDirectory, "position.txt");
    private Point _dragOffset;
    private bool _dragging;
    private bool _clockPinned;
    private bool _startWithWindows;
    private bool _isHoveringClock;
    private DateTimeOffset? _timerEndsAt;
    private TimeSpan _workDuration;
    private TimeSpan _pauseDuration = TimeSpan.FromMinutes(5);
    private DateTimeOffset? _pauseEndsAt;
    private bool _isPauseActive;
    private bool _awaitingPauseChoice;
    private bool _screenOffDuringPause = true;
    private bool _clickSoundEnabled;
    private Point _clickStartPosition;
    private bool _leftButtonPressed;
    private bool _monitorIsOff;
    private bool _timerElapsed;
    private bool _timerBlinkVisible = true;
    private int _timerBlinkTransitions;
    private DateTime _timerBlinkChangedAt;
    private string _selectedAlarmFile = AlarmFiles[0];
    private string _messageText = AppLanguage.Text(
        "Stel uw Alarmgeluid, Timertijd in en Geef uw Woonplaats en Land op voor het Weer ------>",
        "Set your alarm sound and timer, and enter your city and country for the weather ------>");
    private DateTime _menuAnimationStarted;
    private float _settingsIconRotation;
    private bool _darkTheme = true;
    private string _city = string.Equals(AppLanguage.CountryCode, "NL", StringComparison.OrdinalIgnoreCase)
        ? "Leiden"
        : string.Empty;
    private string _country = AppLanguage.CountryCode;
    private string _countryName = RegionInfo.CurrentRegion.NativeName;
    private bool _useDeviceLocation = true;
    private bool _showingDeviceLocation;
    private string _weatherIcon = "🌡️";
    private string _temperature = "?°C";
    private DateTimeOffset _weatherUpdatedAt = DateTimeOffset.MinValue;

    public ClockForm()
    {
        Text = AppLanguage.Text("ADHD Klok", "ADHD Clock");
        ClientSize = new Size(WindowSize, WindowSize);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        BackColor = BackgroundColor;
        TransparencyKey = BackgroundColor;
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _darkTheme = LoadTheme();
        _clockPinned = LoadPinnedMode();
        _startWithWindows = LoadStartWithWindows();
        _pauseDuration = LoadPauseDuration();
        _screenOffDuringPause = LoadScreenOffDuringPause();
        _clickSoundEnabled = LoadClickSoundEnabled();
        LoadWeatherLocation();
        TopMost = _clockPinned;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Media", "Images", "Instellingen.png");
        using (var image = Image.FromFile(iconPath))
        {
            _settingsIcon = TintIcon(image, ClockAccentLight);
            _lightSettingsIcon = TintIcon(image, ClockAccentDark);
            _pinnedSettingsIcon = TintIcon(image, Color.FromArgb(175, ClockAccentLight));
            _lightPinnedSettingsIcon = TintIcon(image, Color.FromArgb(175, ClockAccentDark));
        }

        Location = LoadPosition();
        UpdateClockToolTip();
        _refreshTimer.Tick += (_, _) =>
        {
            UpdateTimerState();
            UpdateTimerBlink();
            Invalidate();
        };
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        _menuAnimationTimer.Tick += AnimateMenuOpening;
        FormClosed += (_, _) =>
        {
            Application.RemoveMessageFilter(this);
            _timerSound?.Dispose();
            _pendingMenu?.Dispose();
            _settingsIcon.Dispose();
            _lightSettingsIcon.Dispose();
            _pinnedSettingsIcon.Dispose();
            _lightPinnedSettingsIcon.Dispose();
            _menuAnimationTimer.Dispose();
        };
        FormClosing += (_, _) =>
        {
            SavePosition();
            RestoreMonitorIfNeeded();
        };
        _refreshTimer.Start();
        _weatherTimer.Start();
        Shown += async (_, _) => await RefreshWeatherAsync();
        Application.AddMessageFilter(this);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == LeftButtonDownMessage)
        {
            _clickStartPosition = System.Windows.Forms.Cursor.Position;
            _leftButtonPressed = true;
        }
        else if (m.Msg == LeftButtonUpMessage && _leftButtonPressed)
        {
            _leftButtonPressed = false;
            var dragSize = SystemInformation.DragSize;
            var clickBounds = new Rectangle(
                _clickStartPosition.X - dragSize.Width / 2,
                _clickStartPosition.Y - dragSize.Height / 2,
                dragSize.Width,
                dragSize.Height);
            if (_clickSoundEnabled && clickBounds.Contains(System.Windows.Forms.Cursor.Position))
                PlayClickSound();
        }

        return false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        var clockColor = _darkTheme ? ClockColor : SystemColors.ControlText;
        var textColor = _darkTheme ? Color.FromArgb(238, 236, 224) : SystemColors.ControlText;
        var minuteTickColor = _darkTheme ? ClockAccentDark : ClockAccentLight;
        var hourTickColor = _darkTheme ? Color.Red : ClockAccentLight;
        var cardinalHourTickColor = _darkTheme ? ClockAccentLight : ClockAccentDark;
        using var secondsPen = new Pen(minuteTickColor, 2.5f);
        using var cardinalSecondsPen = new Pen(cardinalHourTickColor, 2.5f);
        using var hourPen = new Pen(_darkTheme ? hourTickColor : Color.Red, _darkTheme ? 2f : 4f);
        using var cardinalHourPen = new Pen(cardinalHourTickColor, 4f);
        using var blackHourPen = new Pen(Color.Black, 4f);
        using var lightCardinalHourPen = new Pen(ClockAccentLight, 4f);
        using var twelveHourPen = new Pen(_darkTheme ? cardinalHourTickColor : ClockAccentLight, 6f);
        using var cardinalNumberBrush = new SolidBrush(cardinalHourTickColor);
        using var redNumberBrush = new SolidBrush(Color.Red);
        using var blackCardinalNumberBrush = new SolidBrush(Color.Black);
        using var lightAccentNumberBrush = new SolidBrush(ClockAccentLight);
        using var lightGrayNumberBrush = new SolidBrush(Color.FromArgb(229, 229, 229));
        using var offWhiteNumberBrush = new SolidBrush(Color.FromArgb(238, 236, 224));
        using var numberFont = new Font("Calibri", 18, FontStyle.Regular);
        using var boldNumberFont = new Font("Calibri", 18, FontStyle.Bold);
        using var alternateCardinalNumberBrush = new SolidBrush(
            _darkTheme ? Color.Red : Color.FromArgb(79, 75, 81));

        for (var i = 0; i < 60; i++)
        {
            var angle = i * 6 * Math.PI / 180;
            var start = PointAt(ClockRadius * 0.92, angle);
            var end = PointAt(ClockRadius, angle);
            var tickPen = i % 15 == 0 ? cardinalSecondsPen : secondsPen;
            graphics.DrawLine(tickPen, start, end);
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = i * 30 * Math.PI / 180;
            var tickPen = i == 0
                ? twelveHourPen
                : i % 3 == 0
                    ? !_darkTheme ? lightCardinalHourPen : cardinalHourPen
                    : hourPen;
            graphics.DrawLine(tickPen, PointAt(ClockRadius * 0.88, angle), PointAt(ClockRadius, angle));
            var numberPosition = PointAt(ClockRadius * 0.75, angle);
            var number = i == 0 ? "12" : i.ToString(CultureInfo.InvariantCulture);
            var isAlternateCardinalNumber = i is 3 or 6 or 9;
            var isLightThemeBoldNumber = !_darkTheme;
            var numberBrush = !_darkTheme && i is 0 or 3 or 6 or 9
                ? blackCardinalNumberBrush
                : !_darkTheme && isAlternateCardinalNumber
                    ? redNumberBrush
                : _darkTheme && i == 0
                    ? redNumberBrush
                : isAlternateCardinalNumber
                ? alternateCardinalNumberBrush
                : _darkTheme && i is (1 or 2 or 4 or 5 or 7 or 8 or 10 or 11)
                    ? lightGrayNumberBrush
                : !_darkTheme && i is (1 or 2 or 4 or 5 or 7 or 8 or 10 or 11)
                    ? lightAccentNumberBrush
                : i is 0 or 3 or 6 or 9
                    ? cardinalNumberBrush
                    : offWhiteNumberBrush;
            DrawCenteredText(graphics, number,
                isAlternateCardinalNumber || isLightThemeBoldNumber ? boldNumberFont : numberFont,
                numberBrush, numberPosition.X, numberPosition.Y);
        }

        var now = DateTime.Now;
        var handColor = _darkTheme ? ClockAccentDark : ClockAccentLight;
        DrawHand(graphics, ClockRadius * 0.9, now.Second * 6, Color.FromArgb(170, Color.Red), 1);
        DrawHand(graphics, ClockRadius * 0.75, now.Minute * 6 + now.Second * 0.1,
            Color.FromArgb(145, handColor), 3);
        DrawHand(graphics, ClockRadius * 0.5, (now.Hour % 12) * 30 + now.Minute * 0.5,
            Color.FromArgb(145, handColor), 5);

        using (var centerBrush = new SolidBrush(Color.FromArgb(170, Color.Red)))
            graphics.FillEllipse(centerBrush, Center - 7, Center - 7, 14, 14);

        using var regularFont = new Font("Calibri", 12, FontStyle.Regular);
        using var dateFont = new Font("Calibri", 12.75f, FontStyle.Regular);
        using var timerFont = new Font("Calibri", 20, FontStyle.Regular);
        using var temperatureFont = new Font("Calibri", 15, FontStyle.Regular);
        using var emojiFont = new Font("Segoe UI Emoji", 20, FontStyle.Regular);
        using var messageFont = new Font("Calibri", 11, FontStyle.Regular);
        using var textBrush = new SolidBrush(textColor);
        using var locationBrush = new SolidBrush(_darkTheme ? Color.FromArgb(229, 229, 229) : ClockAccentLight);
        using var weatherBrush = GetWeatherBrush();

        var remaining = GetRemainingTime();
        DrawTimerLabel(graphics, regularFont, _darkTheme ? Color.Red : Color.Black,
            Center, WindowSize - 290);
        if (!_timerElapsed || _timerBlinkVisible)
        {
            var timerText = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            var timerDigitColor = _darkTheme ? Color.FromArgb(238, 236, 224) : SystemColors.ControlText;
            var timerSeparatorColor = _darkTheme ? Color.Red : Color.DarkRed;
            var timerBorderColor = _darkTheme ? ClockAccentDark : ClockAccentLight;
            using var compactTimerFont = new Font(timerFont.FontFamily, timerFont.Size - 3, timerFont.Style);
            DrawTimerText(graphics, timerText, compactTimerFont, timerDigitColor, timerSeparatorColor, timerBorderColor,
                now.Second % 2 == 0, Center, WindowSize - 260);
        }
        DrawLocalizedDate(graphics, now, dateFont, textColor, _darkTheme ? Color.Red : ClockAccentDark,
            Center, WindowSize - 230);
        var locationText = _showingDeviceLocation
            ? $"{AppLanguage.Text("Huidige locatie", "Current location")}, {_countryName}"
            : string.IsNullOrWhiteSpace(_city) ? _countryName : $"{_city}, {_country}";
        DrawCenteredText(graphics, locationText, regularFont, locationBrush, Center, WindowSize - 190);
        DrawCenteredText(graphics, _weatherIcon, emojiFont, weatherBrush, Center, WindowSize - 160);
        DrawTemperature(graphics, _temperature, temperatureFont, textColor,
            _darkTheme ? Color.Red : ClockAccentDark,
            _darkTheme ? Color.FromArgb(229, 229, 229) : ClockAccentLight, Center, WindowSize - 130);

        var showMessagePanel = !_clockPinned || _timerElapsed;
        var showSettingsIcon = !_clockPinned || _isHoveringClock;
        if (showMessagePanel && !_clockPinned)
        {
            using var messageBackground = new LinearGradientBrush(
            MessageArea,
            Color.FromArgb(205, 48, 48, 88),
            Color.FromArgb(225, 18, 18, 50),
            LinearGradientMode.Vertical);
            using var messageHighlight = new LinearGradientBrush(
            MessageArea,
            Color.FromArgb(55, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical);
            using var messageBorder = new Pen(_darkTheme ? Color.Red : Color.DarkRed, 1);
            using var messageTextBrush = new SolidBrush(Color.FromArgb(238, 236, 224));
            var messageGraphicsState = graphics.Save();
            graphics.SetClip(MessageArea);
            graphics.FillRectangle(messageBackground, MessageArea);
            graphics.FillRectangle(messageHighlight, MessageArea);
            graphics.Restore(messageGraphicsState);
            graphics.DrawRectangle(messageBorder, MessageArea);

            var textArea = new RectangleF(MessageArea.X + 10, MessageArea.Y + 3,
                MessageArea.Width - 52, MessageArea.Height - 6);
            using var messageFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.LineLimit
            };
            graphics.DrawString(_messageText, messageFont, messageTextBrush, textArea, messageFormat);
        }

        var iconBounds = GetSettingsIconBounds();
        if (showSettingsIcon)
        {
            if (showMessagePanel)
            {
                using var dividerPen = new Pen(_darkTheme ? ClockAccentLight : SystemColors.ControlDark, 2);
                graphics.DrawLine(dividerPen, iconBounds.Left - 6, MessageArea.Y + 7,
                    iconBounds.Left - 6, MessageArea.Bottom - 8);
            }

            var graphicsState = graphics.Save();
            graphics.TranslateTransform(iconBounds.X + iconBounds.Width / 2f, iconBounds.Y + iconBounds.Height / 2f);
            graphics.RotateTransform(_settingsIconRotation);
            var settingsIcon = _darkTheme
                ? _clockPinned ? _pinnedSettingsIcon : _settingsIcon
                : _clockPinned ? _lightPinnedSettingsIcon : _lightSettingsIcon;
            graphics.DrawImage(settingsIcon,
                new Rectangle(-iconBounds.Width / 2, -iconBounds.Height / 2, iconBounds.Width, iconBounds.Height));
            graphics.Restore(graphicsState);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        if (GetSettingsIconBounds().Contains(e.Location) && (_clockPinned || MessageArea.Contains(e.Location)))
        {
            OpenMenu();
            return;
        }

        if (_clockPinned)
            return;

        if (MessageArea.Contains(e.Location))
        {
            OpenMenu();
            return;
        }

        _dragging = true;
        _dragOffset = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var wasHovering = _isHoveringClock;
        _isHoveringClock = _clockPinned && GetSettingsIconBounds().Contains(e.Location);
        var overMenuTrigger = GetSettingsIconBounds().Contains(e.Location);
        Cursor = (_clockPinned ? overMenuTrigger : MessageArea.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        if (wasHovering != _isHoveringClock)
            Invalidate();

        if (_clockPinned)
            return;

        if (_dragging && e.Button == MouseButtons.Left)
        {
            Location = new Point(System.Windows.Forms.Cursor.Position.X - _dragOffset.X,
                System.Windows.Forms.Cursor.Position.Y - _dragOffset.Y);
            SavePosition();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_isHoveringClock)
        {
            _isHoveringClock = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_awaitingPauseChoice || e.Control || e.Alt || e.Shift)
            return;

        if (e.KeyCode == Keys.Y)
        {
            _awaitingPauseChoice = false;
            _timerEndsAt = DateTimeOffset.Now.Add(_workDuration);
            _timerElapsed = false;
            _timerBlinkVisible = true;
            _timerBlinkTransitions = 0;
            _messageText = AppLanguage.Text("Timer opnieuw gestart.", "Timer restarted.");
        }
        else if (e.KeyCode == Keys.N)
        {
            _awaitingPauseChoice = false;
            _timerEndsAt = null;
            _timerElapsed = false;
            _timerBlinkVisible = true;
            _timerBlinkTransitions = 0;
            _messageText = AppLanguage.Text("Timer gestopt.", "Timer stopped.");
        }
        else
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        Invalidate();
    }

    private void OpenMenu()
    {
        if (_menuAnimationState == MenuAnimationState.Closing)
            return;

        if (_pendingMenu is not null)
        {
            _menuAnimationTimer.Stop();
            _pendingMenu.Dispose();
            _pendingMenu = null;
            _menuAnimationState = MenuAnimationState.Idle;
            _settingsIconRotation = 0;
            Invalidate(MessageArea);
            return;
        }

        if (_openMenu?.Visible == true)
        {
            _menuAnimationState = MenuAnimationState.Closing;
            _menuAnimationStarted = DateTime.Now;
            _openMenu.Enabled = false;
            _menuAnimationTimer.Start();
            return;
        }

        var menu = new ContextMenuStrip
        {
            BackColor = GetMenuBackgroundColor(),
            ForeColor = GetMenuTextColor(),
            Renderer = new ToolStripProfessionalRenderer(new ClockMenuColorTable(_darkTheme)),
            Padding = new Padding(2)
        };
        var darkThemeItem = new ToolStripMenuItem(AppLanguage.Text("Donker", "Dark"))
        {
            Checked = _darkTheme,
            CheckOnClick = false
        };
        var lightThemeItem = new ToolStripMenuItem(AppLanguage.Text("Licht (Windows)", "Light (Windows)"))
        {
            Checked = !_darkTheme,
            CheckOnClick = false
        };
        darkThemeItem.Click += (_, _) => SetTheme(true, darkThemeItem, lightThemeItem);
        lightThemeItem.Click += (_, _) => SetTheme(false, darkThemeItem, lightThemeItem);
        var appearanceMenu = new ToolStripMenuItem(AppLanguage.Text("Weergave", "Appearance"));
        appearanceMenu.DropDownItems.Add(darkThemeItem);
        appearanceMenu.DropDownItems.Add(lightThemeItem);
        menu.Items.Add(appearanceMenu);
        var clickSoundItem = new ToolStripMenuItem(AppLanguage.Text("Klikgeluid aan/uit", "Click sound on/off"))
        {
            Checked = _clickSoundEnabled,
            CheckOnClick = false
        };
        clickSoundItem.Click += (_, _) => SetClickSoundEnabled(!_clickSoundEnabled, clickSoundItem);
        menu.Items.Add(clickSoundItem);
        menu.Items.Add(new ToolStripSeparator());
        var pinMenuItem = new ToolStripMenuItem(AppLanguage.Text("Klok vastzetten op bureaublad", "Pin clock to desktop"))
        {
            Checked = _clockPinned,
            CheckOnClick = false
        };
        pinMenuItem.Click += (_, _) => SetPinnedMode(!_clockPinned, pinMenuItem);
        menu.Items.Add(pinMenuItem);
        var startupMenuItem = new ToolStripMenuItem(AppLanguage.Text("Met Windows opstarten", "Start with Windows"))
        {
            Checked = _startWithWindows,
            CheckOnClick = false
        };
        startupMenuItem.Click += (_, _) =>
            SetStartWithWindows(!_startWithWindows, startupMenuItem);
        menu.Items.Add(startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());

        var timerMenu = new ToolStripMenuItem(AppLanguage.Text("⏱ Timer & Pauze & Instellen", "⏱ Timer & Break & Settings"));
        timerMenu.DropDownItems.Add(AppLanguage.Text("Timer instellen...", "Set timer..."), null, (_, _) => OpenTimerSettings());
        timerMenu.DropDownItems.Add(AppLanguage.Text("Pauzetijd instellen...", "Set break duration..."), null, (_, _) => OpenPauseSettings());
        var screenOffDuringPauseItem = new ToolStripMenuItem(
            AppLanguage.Text("Scherm uitschakelen tijdens pauze", "Turn off screen during break"))
        {
            Checked = _screenOffDuringPause,
            CheckOnClick = false
        };
        screenOffDuringPauseItem.Click += (_, _) =>
            SetScreenOffDuringPause(!_screenOffDuringPause, screenOffDuringPauseItem);
        timerMenu.DropDownItems.Add(screenOffDuringPauseItem);

        var alarmMenu = new ToolStripMenuItem(AppLanguage.Text("🎼 Alarmgeluid instellen", "🎼 Set alarm sound"));
        foreach (var alarmFile in AlarmFiles)
        {
            var alarmItem = new ToolStripMenuItem(alarmFile)
            {
                Checked = alarmFile == _selectedAlarmFile,
                CheckOnClick = false
            };
            alarmItem.Click += (_, _) => SelectAlarm(alarmFile);
            alarmMenu.DropDownItems.Add(alarmItem);
        }

        menu.Items.Add(timerMenu);
        menu.Items.Add(alarmMenu);
        menu.Items.Add(AppLanguage.Text("🌤 Weer instellen", "🌤 Weather settings"), null, (_, _) => OpenWeatherSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(AppLanguage.Text("Afsluiten", "Exit"), null, (_, _) => Close());
        foreach (ToolStripItem item in menu.Items)
            SetMenuItemColors(item, GetMenuTextColor());
        menu.Closing += (_, args) =>
        {
            var iconScreenBounds = RectangleToScreen(GetSettingsIconBounds());
            if (args.CloseReason == ToolStripDropDownCloseReason.AppClicked &&
                iconScreenBounds.Contains(System.Windows.Forms.Cursor.Position))
            {
                args.Cancel = true;
            }
        };

        _pendingMenu = menu;
        _menuAnimationStarted = DateTime.Now;
        _menuAnimationState = MenuAnimationState.Opening;
        _menuIsShown = false;
        _settingsIconRotation = 0;
        _menuAnimationTimer.Start();
    }

    private void AnimateMenuOpening(object? sender, EventArgs e)
    {
        const double openingDelayMilliseconds = 120;
        const double animationDurationMilliseconds = 500;
        var elapsed = (DateTime.Now - _menuAnimationStarted).TotalMilliseconds;

        if (_menuAnimationState == MenuAnimationState.Opening)
        {
            _settingsIconRotation = (float)(360 * Math.Min(1, elapsed /
                (openingDelayMilliseconds + animationDurationMilliseconds)));

            if (elapsed >= openingDelayMilliseconds && !_menuIsShown)
                ShowMenuCollapsed();

            if (_menuIsShown && _openMenu is not null)
            {
                var progress = Math.Min(1, (elapsed - openingDelayMilliseconds) / animationDurationMilliseconds);
                _openMenu.Height = Math.Max(1, (int)Math.Ceiling(_menuExpandedSize.Height * progress));
                if (progress >= 1)
                {
                    _openMenu.Height = _menuExpandedSize.Height;
                    _openMenu.Enabled = true;
                    FinishMenuAnimation();
                }
            }
        }
        else if (_menuAnimationState == MenuAnimationState.Closing && _openMenu is not null)
        {
            var progress = Math.Min(1, elapsed / animationDurationMilliseconds);
            _settingsIconRotation = (float)(-360 * progress);
            _openMenu.Height = Math.Max(1, (int)Math.Ceiling(_menuExpandedSize.Height * (1 - progress)));
            if (progress >= 1)
            {
                var menu = _openMenu;
                menu.Hide();
                _openMenu = null;
                _pendingMenu = null;
                _menuIsShown = false;
                menu.BeginInvoke(new Action(menu.Dispose));
                FinishMenuAnimation();
            }
        }

        Invalidate(MessageArea);
    }

    private void ShowMenuCollapsed()
    {
        var menu = _pendingMenu;
        _pendingMenu = null;
        if (menu is null)
        {
            FinishMenuAnimation();
            return;
        }

        _menuExpandedSize = menu.GetPreferredSize(Size.Empty);
        menu.AutoSize = false;
        menu.Size = _menuExpandedSize;
        _openMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openMenu, menu))
                _openMenu = null;
            _menuIsShown = false;

            if (_menuAnimationState != MenuAnimationState.Closing)
                FinishMenuAnimation();

            menu.BeginInvoke(new Action(menu.Dispose));
        };

        menu.Show(GetMenuScreenLocation(_menuExpandedSize));
        menu.Height = 1;
        _menuIsShown = true;
    }

    private Point GetMenuScreenLocation(Size menuSize)
    {
        var clockBounds = Bounds;
        var iconBounds = RectangleToScreen(GetSettingsIconBounds());
        var workingArea = Screen.FromRectangle(clockBounds).WorkingArea;
        var preferredTop = iconBounds.Top + (iconBounds.Height - menuSize.Height) / 2;

        if (TryGetMenuLocation(clockBounds.Right, preferredTop, workingArea, clockBounds, menuSize, out var location) ||
            TryGetMenuLocation(clockBounds.Left - menuSize.Width, preferredTop, workingArea, clockBounds, menuSize, out location))
        {
            return location;
        }

        var adjacentPositions = new[]
        {
            (clockBounds.Left, clockBounds.Top - menuSize.Height),
            (clockBounds.Right - menuSize.Width, clockBounds.Top - menuSize.Height),
            (clockBounds.Left, clockBounds.Bottom),
            (clockBounds.Right - menuSize.Width, clockBounds.Bottom)
        };
        foreach (var (x, y) in adjacentPositions)
        {
            if (TryGetMenuLocation(x, y, workingArea, clockBounds, menuSize, out location))
                return location;
        }

        foreach (var screen in Screen.AllScreens.OrderBy(screen =>
                     Math.Pow(screen.WorkingArea.Left - iconBounds.Left, 2) +
                     Math.Pow(screen.WorkingArea.Top - iconBounds.Top, 2)))
        {
            var area = screen.WorkingArea;
            var desktopPositions = new[]
            {
                (area.Left, area.Top),
                (area.Right - menuSize.Width, area.Top),
                (area.Left, area.Bottom - menuSize.Height),
                (area.Right - menuSize.Width, area.Bottom - menuSize.Height)
            };
            foreach (var (x, y) in desktopPositions)
            {
                if (TryGetMenuLocation(x, y, area, clockBounds, menuSize, out location))
                    return location;
            }
        }

        throw new InvalidOperationException("Er is geen plek op het bureaublad waar het menu buiten de klok past.");
    }

    private static bool TryGetMenuLocation(
        int x,
        int y,
        Rectangle workingArea,
        Rectangle clockBounds,
        Size menuSize,
        out Point location)
    {
        if (menuSize.Width > workingArea.Width || menuSize.Height > workingArea.Height)
        {
            location = Point.Empty;
            return false;
        }

        x = Math.Clamp(x, workingArea.Left, workingArea.Right - menuSize.Width);
        y = Math.Clamp(y, workingArea.Top, workingArea.Bottom - menuSize.Height);
        var menuBounds = new Rectangle(new Point(x, y), menuSize);
        if (!workingArea.Contains(menuBounds) || menuBounds.IntersectsWith(clockBounds))
        {
            location = Point.Empty;
            return false;
        }

        location = menuBounds.Location;
        return true;
    }

    private void FinishMenuAnimation()
    {
        _menuAnimationTimer.Stop();
        _menuAnimationState = MenuAnimationState.Idle;
        _settingsIconRotation = 0;
    }

    private enum MenuAnimationState
    {
        Idle,
        Opening,
        Closing
    }

    private void SetTheme(bool darkTheme, ToolStripMenuItem darkThemeItem, ToolStripMenuItem lightThemeItem)
    {
        _darkTheme = darkTheme;
        darkThemeItem.Checked = darkTheme;
        lightThemeItem.Checked = !darkTheme;
        SaveTheme();

        if (_openMenu is { } menu)
        {
            menu.BackColor = GetMenuBackgroundColor();
            menu.ForeColor = GetMenuTextColor();
            menu.Renderer = CreateMenuRenderer();
            foreach (ToolStripItem item in menu.Items)
                SetMenuItemColors(item, GetMenuTextColor());
        }

        Invalidate();
    }

    private void SetPinnedMode(bool pinned, ToolStripMenuItem pinMenuItem)
    {
        _clockPinned = pinned;
        TopMost = pinned;
        pinMenuItem.Checked = pinned;
        SavePinnedMode();
        UpdateClockToolTip();
        Invalidate();
    }

    private void UpdateClockToolTip()
    {
        _toolTip.SetToolTip(this, _clockPinned
            ? string.Empty
            : AppLanguage.Text("Klik en sleep om de klok te verplaatsen.", "Click and drag to move the clock."));
    }

    private void SetMenuItemColors(ToolStripItem item, Color textColor)
    {
        item.ForeColor = textColor;
        if (item is ToolStripDropDownItem dropDownItem)
        {
            dropDownItem.DropDown.BackColor = GetMenuBackgroundColor();
            dropDownItem.DropDown.Renderer = CreateMenuRenderer();
            foreach (ToolStripItem child in dropDownItem.DropDownItems)
                SetMenuItemColors(child, textColor);
        }
    }

    private Color GetMenuBackgroundColor() =>
        _darkTheme ? Color.FromArgb(24, 24, 64) : SystemColors.Menu;

    private Color GetMenuTextColor() =>
        _darkTheme ? ClockColor : SystemColors.MenuText;

    private ToolStripRenderer CreateMenuRenderer() =>
        new ToolStripProfessionalRenderer(new ClockMenuColorTable(_darkTheme));

    private Rectangle GetSettingsIconBounds()
    {
        if (!_clockPinned)
            return new Rectangle(MessageArea.Right - 36, MessageArea.Y + 9, 24, 24);

        var position = PointAt(108, 135 * Math.PI / 180);
        return new Rectangle((int)Math.Round(position.X) - 12, (int)Math.Round(position.Y) - 12, 24, 24);
    }

    private static Image TintIcon(Image source, Color color)
    {
        var tinted = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (var x = 0; x < source.Width; x++)
        {
            for (var y = 0; y < source.Height; y++)
            {
                var sourceColor = ((Bitmap)source).GetPixel(x, y);
                var alpha = sourceColor.A * color.A / 255;
                tinted.SetPixel(x, y, Color.FromArgb(alpha, color.R, color.G, color.B));
            }
        }

        return tinted;
    }

    private sealed class ClockMenuColorTable : ProfessionalColorTable
    {
        private readonly bool _darkTheme;
        private static readonly Color PanelColor = Color.FromArgb(24, 24, 64);
        private static readonly Color DarkRedColor = Color.FromArgb(72, 16, 28);
        private static readonly Color SelectedColor = Color.FromArgb(48, 48, 92);

        public ClockMenuColorTable(bool darkTheme) => _darkTheme = darkTheme;

        public override Color ToolStripDropDownBackground => _darkTheme ? PanelColor : SystemColors.Menu;
        public override Color ImageMarginGradientBegin => _darkTheme ? DarkRedColor : SystemColors.Menu;
        public override Color ImageMarginGradientMiddle => _darkTheme ? DarkRedColor : SystemColors.Menu;
        public override Color ImageMarginGradientEnd => _darkTheme ? DarkRedColor : SystemColors.Menu;
        public override Color MenuBorder => _darkTheme ? DarkRedColor : SystemColors.ActiveBorder;
        public override Color ToolStripBorder => _darkTheme ? DarkRedColor : SystemColors.ActiveBorder;
        public override Color MenuItemBorder => _darkTheme ? Color.FromArgb(120, 48, 62) : SystemColors.Highlight;
        public override Color MenuItemSelected => _darkTheme ? SelectedColor : SystemColors.Highlight;
        public override Color MenuItemSelectedGradientBegin => _darkTheme ? SelectedColor : SystemColors.Highlight;
        public override Color MenuItemSelectedGradientEnd => _darkTheme ? SelectedColor : SystemColors.Highlight;
        public override Color MenuItemPressedGradientBegin => _darkTheme ? DarkRedColor : SystemColors.Control;
        public override Color MenuItemPressedGradientMiddle => _darkTheme ? DarkRedColor : SystemColors.Control;
        public override Color MenuItemPressedGradientEnd => _darkTheme ? DarkRedColor : SystemColors.Control;
        public override Color CheckBackground => _darkTheme ? DarkRedColor : SystemColors.Control;
        public override Color CheckSelectedBackground => _darkTheme ? SelectedColor : SystemColors.Highlight;
        public override Color CheckPressedBackground => _darkTheme ? DarkRedColor : SystemColors.Control;
        public override Color SeparatorDark => _darkTheme ? DarkRedColor : SystemColors.ControlDark;
        public override Color SeparatorLight => _darkTheme ? PanelColor : SystemColors.Menu;
    }

    private void SelectAlarm(string alarmFile)
    {
        if (!AlarmFiles.Contains(alarmFile, StringComparer.Ordinal))
            return;

        _selectedAlarmFile = alarmFile;
        _timerSound?.Dispose();
        _timerSound = null;
    }

    private void OpenTimerSettings()
    {
        using var dialog = new TimerSettingsForm(
            AppLanguage.Text("Timer instellen", "Set timer"),
            startButtonText: AppLanguage.Text("Start timer", "Start timer"));
        ApplyDialogTheme(dialog);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            RestoreMonitorIfNeeded();
            _workDuration = dialog.Duration;
            _timerEndsAt = DateTimeOffset.Now.Add(dialog.Duration);
            _pauseEndsAt = null;
            _isPauseActive = false;
            _awaitingPauseChoice = false;
            _timerElapsed = false;
            _timerBlinkVisible = true;
            _timerBlinkTransitions = 0;
            _messageText = AppLanguage.Text("Timer gestart.", "Timer started.");
        }
        else if (dialog.WasClosedWithCloseButton)
        {
            ResetTimer();
        }

        Invalidate();
    }

    private void ResetTimer()
    {
        var wasPauseActive = _isPauseActive;
        RestoreMonitorIfNeeded();
        _workDuration = TimeSpan.Zero;
        _timerEndsAt = null;
        _pauseEndsAt = null;
        _isPauseActive = false;
        _awaitingPauseChoice = false;
        _timerElapsed = false;
        _timerBlinkVisible = true;
        _timerBlinkTransitions = 0;
        _messageText = wasPauseActive
            ? AppLanguage.Text("Pauzetimer teruggezet naar 00:00:00.", "Break timer reset to 00:00:00.")
            : AppLanguage.Text("Timer teruggezet naar 00:00:00.", "Timer reset to 00:00:00.");
    }

    private void OpenPauseSettings()
    {
        using var dialog = new TimerSettingsForm(
            AppLanguage.Text("Pauzetijd instellen", "Set break duration"),
            _pauseDuration, AppLanguage.Text("Pauzetijd opslaan", "Save break duration"));
        ApplyDialogTheme(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            if (dialog.WasClosedWithCloseButton && _isPauseActive)
            {
                ResetTimer();
                Invalidate();
            }

            return;
        }

        _pauseDuration = dialog.Duration;
        SavePauseDuration();
        _messageText = AppLanguage.IsEnglish
            ? $"Break duration set to {_pauseDuration:hh\\:mm\\:ss}."
            : $"Pauzetijd ingesteld op {_pauseDuration:hh\\:mm\\:ss}.";
        Invalidate();
    }

    private void SetScreenOffDuringPause(bool enabled, ToolStripMenuItem menuItem)
    {
        _screenOffDuringPause = enabled;
        menuItem.Checked = enabled;
        SaveScreenOffDuringPause();
    }

    private void SetClickSoundEnabled(bool enabled, ToolStripMenuItem menuItem)
    {
        var previousValue = _clickSoundEnabled;
        _clickSoundEnabled = enabled;
        try
        {
            File.WriteAllText(_clickSoundPath, enabled.ToString());
            menuItem.Checked = enabled;
        }
        catch (IOException exception)
        {
            _clickSoundEnabled = previousValue;
            ShowClickSoundSaveError(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            _clickSoundEnabled = previousValue;
            ShowClickSoundSaveError(exception);
        }
    }

    private void ShowClickSoundSaveError(Exception exception)
    {
        MessageBox.Show(this,
            AppLanguage.IsEnglish
                ? $"The click sound setting could not be saved:\n{exception.Message}"
                : $"De klikgeluidinstelling kon niet worden opgeslagen:\n{exception.Message}",
            AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OpenWeatherSettings()
    {
        using var dialog = new WeatherSettingsForm(_city, _country);
        ApplyDialogTheme(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _city = dialog.City;
        _country = dialog.Country;
        _countryName = _country;
        _useDeviceLocation = false;
        _showingDeviceLocation = false;
        SaveWeatherLocation();
        _weatherUpdatedAt = DateTimeOffset.MinValue;
        Invalidate();
        _ = RefreshWeatherAsync();
    }

    private void ApplyDialogTheme(Form dialog)
    {
        if (dialog is SettingsDialogForm settingsDialog)
            settingsDialog.ApplyTheme(_darkTheme);
    }

    private bool LoadTheme()
    {
        try
        {
            return File.ReadAllText(_themePath).Trim() switch
            {
                "Light" => false,
                "Dark" => true,
                _ => true
            };
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private bool LoadPinnedMode()
    {
        try
        {
            return bool.TryParse(File.ReadAllText(_pinPath).Trim(), out var pinned) && pinned;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool LoadStartWithWindows()
    {
        try
        {
            using var startupKey = Registry.CurrentUser.OpenSubKey(WindowsStartupKey);
            return startupKey?.GetValue(WindowsStartupValueName) is string command &&
                   !string.IsNullOrWhiteSpace(command);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          SecurityException or InvalidOperationException)
        {
            MessageBox.Show(
                AppLanguage.IsEnglish
                    ? $"The Windows startup setting could not be read:\n{exception.Message}"
                    : $"De instelling voor opstarten met Windows kan niet worden gelezen:\n{exception.Message}",
                AppLanguage.Text("Opstartinstelling niet beschikbaar", "Startup setting unavailable"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void SetStartWithWindows(bool enabled, ToolStripMenuItem menuItem)
    {
        try
        {
            using var startupKey = Registry.CurrentUser.CreateSubKey(WindowsStartupKey, writable: true)
                ?? throw new IOException(AppLanguage.Text(
                    "De Windows-opstartsleutel kon niet worden geopend.",
                    "Could not open the Windows startup registry key."));

            if (enabled)
                startupKey.SetValue(WindowsStartupValueName, GetStartupCommand(), RegistryValueKind.String);
            else
                startupKey.DeleteValue(WindowsStartupValueName, throwOnMissingValue: false);

            _startWithWindows = enabled;
            menuItem.Checked = enabled;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          SecurityException or InvalidOperationException)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The Windows startup setting could not be saved:\n{exception.Message}"
                    : $"De instelling voor opstarten met Windows kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Opstartinstelling niet opgeslagen", "Startup setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            menuItem.Checked = _startWithWindows;
        }
    }

    private static string GetStartupCommand()
    {
        var processPath = Environment.ProcessPath
            ??             throw new InvalidOperationException(AppLanguage.Text(
                "Het pad van de huidige app kon niet worden bepaald.",
                "Could not determine the current application path."));

        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var applicationPath = Environment.GetCommandLineArgs().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(applicationPath))
                return $"\"{processPath}\" \"{applicationPath}\"";
        }

        return $"\"{processPath}\"";
    }

    private TimeSpan LoadPauseDuration()
    {
        try
        {
            return TimeSpan.TryParse(
                       File.ReadAllText(_pauseDurationPath).Trim(),
                       CultureInfo.InvariantCulture,
                       out var duration) && duration > TimeSpan.Zero
                ? duration
                : TimeSpan.FromMinutes(5);
        }
        catch (IOException)
        {
            return TimeSpan.FromMinutes(5);
        }
        catch (UnauthorizedAccessException)
        {
            return TimeSpan.FromMinutes(5);
        }
    }

    private bool LoadScreenOffDuringPause()
    {
        try
        {
            return bool.TryParse(File.ReadAllText(_screenOffDuringPausePath).Trim(), out var enabled)
                ? enabled
                : true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private bool LoadClickSoundEnabled()
    {
        if (!File.Exists(_clickSoundPath))
            return false;

        try
        {
            var value = File.ReadAllText(_clickSoundPath).Trim();
            if (bool.TryParse(value, out var enabled))
                return enabled;

            MessageBox.Show(this,
                AppLanguage.Text(
                    "De opgeslagen klikgeluidinstelling is ongeldig; klikgeluid staat uit.",
                    "The saved click sound setting is invalid; click sound is off."),
                AppLanguage.Text("Instelling niet geladen", "Setting not loaded"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (IOException exception)
        {
            ShowClickSoundLoadError(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowClickSoundLoadError(exception);
        }

        return false;
    }

    private void ShowClickSoundLoadError(Exception exception)
    {
        MessageBox.Show(this,
            AppLanguage.IsEnglish
                ? $"The click sound setting could not be read:\n{exception.Message}"
                : $"De klikgeluidinstelling kon niet worden gelezen:\n{exception.Message}",
            AppLanguage.Text("Instelling niet geladen", "Setting not loaded"),
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static void PlayClickSound()
    {
        PlaySound("MenuCommand", IntPtr.Zero, PlaySoundAlias | PlaySoundAsync | PlaySoundNoDefault);
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string soundName, IntPtr module, uint soundFlags);

    private void LoadWeatherLocation()
    {
        if (!File.Exists(_weatherLocationPath))
            return;

        try
        {
            var values = File.ReadAllLines(_weatherLocationPath);
            if (values.Length == 2 &&
                !string.IsNullOrWhiteSpace(values[0]) &&
                values[1].Trim().Length == 2)
            {
                _city = values[0].Trim();
                _country = values[1].Trim().ToUpperInvariant();
                _countryName = _country;
                return;
            }

            MessageBox.Show(this,
                AppLanguage.Text(
                    "De opgeslagen weerlocatie is ongeldig; de standaardlocatie wordt gebruikt.",
                    "The saved weather location is invalid; the default location will be used."),
                AppLanguage.Text("Weerlocatie niet geladen", "Weather location not loaded"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (IOException exception)
        {
            ShowWeatherLocationLoadError(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowWeatherLocationLoadError(exception);
        }
    }

    private void ShowWeatherLocationLoadError(Exception exception)
    {
        MessageBox.Show(this,
            AppLanguage.IsEnglish
                ? $"The saved weather location could not be read:\n{exception.Message}"
                : $"De opgeslagen weerlocatie kon niet worden gelezen:\n{exception.Message}",
            AppLanguage.Text("Weerlocatie niet geladen", "Weather location not loaded"),
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void SaveWeatherLocation()
    {
        try
        {
            File.WriteAllLines(_weatherLocationPath, [_city, _country]);
        }
        catch (IOException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The weather location could not be saved:\n{exception.Message}"
                    : $"De weerlocatie kon niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The weather location could not be saved:\n{exception.Message}"
                    : $"De weerlocatie kon niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SavePauseDuration()
    {
        try
        {
            File.WriteAllText(_pauseDurationPath, _pauseDuration.ToString("c", CultureInfo.InvariantCulture));
        }
        catch (IOException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The break duration could not be saved:\n{exception.Message}"
                    : $"De pauzetijd kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The break duration could not be saved:\n{exception.Message}"
                    : $"De pauzetijd kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveScreenOffDuringPause()
    {
        try
        {
            File.WriteAllText(_screenOffDuringPausePath, _screenOffDuringPause.ToString());
        }
        catch (IOException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The screen setting could not be saved:\n{exception.Message}"
                    : $"De scherminstelling kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The screen setting could not be saved:\n{exception.Message}"
                    : $"De scherminstelling kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SavePinnedMode()
    {
        try
        {
            File.WriteAllText(_pinPath, _clockPinned.ToString());
        }
        catch (IOException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The pinning setting could not be saved:\n{exception.Message}"
                    : $"De instelling voor vastzetten kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The pinning setting could not be saved:\n{exception.Message}"
                    : $"De instelling voor vastzetten kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveTheme()
    {
        try
        {
            File.WriteAllText(_themePath, _darkTheme ? "Dark" : "Light");
        }
        catch (IOException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The display preference could not be saved:\n{exception.Message}"
                    : $"De weergavekeuze kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The display preference could not be saved:\n{exception.Message}"
                    : $"De weergavekeuze kan niet worden opgeslagen:\n{exception.Message}",
                AppLanguage.Text("Instelling niet opgeslagen", "Setting not saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private TimeSpan GetRemainingTime()
    {
        var end = _isPauseActive ? _pauseEndsAt : _timerEndsAt;
        if (end is not { } endTime)
            return TimeSpan.Zero;

        var remaining = endTime - DateTimeOffset.Now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void UpdateTimerState()
    {
        var now = DateTimeOffset.Now;
        if (_isPauseActive && _pauseEndsAt is { } pauseEnd && now >= pauseEnd)
        {
            RestoreMonitorIfNeeded(force: true);
            _pauseEndsAt = null;
            _isPauseActive = false;
            _timerEndsAt = null;
            _awaitingPauseChoice = true;
            _timerElapsed = true;
            _timerBlinkVisible = true;
            _timerBlinkTransitions = 0;
            _timerBlinkChangedAt = DateTime.Now;
            _messageText = AppLanguage.Text(
                "Pauze voorbij. Dezelfde werktijd opnieuw? Druk Y voor ja of N voor nee.",
                "Break is over. Repeat the same work duration? Press Y for yes or N for no.");
            PlayTimerSound();
            Activate();
            Focus();
            return;
        }

        if (_isPauseActive || _timerEndsAt is not { } workEnd || now < workEnd)
            return;

        _timerEndsAt = null;
        _pauseEndsAt = now.Add(_pauseDuration);
        _isPauseActive = true;
        _timerElapsed = false;
        _messageText = AppLanguage.Text("Werktijd voorbij. Pauze gestart.", "Work time is over. Break started.");
        PlayTimerSound();
        if (_screenOffDuringPause)
            TrySetMonitorPower(false);
    }

    private void TrySetMonitorPower(bool turnOn)
    {
        try
        {
            SetMonitorPower(turnOn);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or
                                          InvalidOperationException or PlatformNotSupportedException)
        {
            var action = AppLanguage.IsEnglish
                ? turnOn ? "turned on" : "turned off"
                : turnOn ? "ingeschakeld" : "uitgeschakeld";
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"The screen could not be {action}:\n{exception.Message}"
                    : $"Het scherm kon niet {action} worden:\n{exception.Message}",
                AppLanguage.Text("Scherminstelling niet toegepast", "Screen setting not applied"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SetMonitorPower(bool turnOn)
    {
        var result = SendMessageTimeout(
            BroadcastWindow,
            SystemCommandMessage,
            MonitorPowerCommand,
            new IntPtr(turnOn ? -1 : 2),
            SendMessageAbortIfHung,
            2_000,
            out _);
        if (result == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0)
                throw new System.ComponentModel.Win32Exception(error);

            throw new InvalidOperationException(AppLanguage.Text(
                "Windows reageerde niet op het verzoek om de monitorstand te wijzigen.",
                "Windows did not respond to the monitor power request."));
        }

        _monitorIsOff = !turnOn;
    }

    private void RestoreMonitorIfNeeded(bool force = false)
    {
        if (force || _monitorIsOff)
            TrySetMonitorPower(true);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint message,
        nuint wParam,
        IntPtr lParam,
        nuint flags,
        uint timeout,
        out IntPtr result);

    private void UpdateTimerBlink()
    {
        if (!_timerElapsed || _timerBlinkTransitions >= 20 ||
            DateTime.Now - _timerBlinkChangedAt < TimeSpan.FromMilliseconds(500))
        {
            return;
        }

        _timerBlinkChangedAt = DateTime.Now;
        _timerBlinkTransitions++;
        _timerBlinkVisible = _timerBlinkTransitions >= 20 || !_timerBlinkVisible;
    }

    private void PlayTimerSound()
    {
        var timerSoundPath = Path.Combine(AppContext.BaseDirectory, "Media", "Sounds", _selectedAlarmFile);
        try
        {
            _timerSound ??= new System.Media.SoundPlayer(timerSoundPath);
            _timerSound.Load();
            _timerSound.Play();
        }
        catch (IOException exception)
        {
            _timerSound?.Dispose();
            _timerSound = null;
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"Could not load the timer sound:\n{timerSoundPath}\n\n{exception.Message}"
                    : $"Kan het timer-geluid niet laden:\n{timerSoundPath}\n\n{exception.Message}",
                AppLanguage.Text("Timer-geluid niet beschikbaar", "Timer sound unavailable"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (InvalidOperationException exception)
        {
            _timerSound?.Dispose();
            _timerSound = null;
            MessageBox.Show(this,
                AppLanguage.IsEnglish
                    ? $"Could not play the timer sound:\n{exception.Message}"
                    : $"Kan het timer-geluid niet afspelen:\n{exception.Message}",
                AppLanguage.Text("Timer-geluid niet beschikbaar", "Timer sound unavailable"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RefreshWeatherAsync()
    {
        if (IsDisposed || DateTimeOffset.Now - _weatherUpdatedAt < TimeSpan.FromMinutes(9))
            return;

        _weatherUpdatedAt = DateTimeOffset.Now;
        try
        {
            var deviceLocation = _useDeviceLocation ? await GetDeviceLocationAsync() : null;
            string weatherUrl;
            if (deviceLocation is { } coordinates)
            {
                _showingDeviceLocation = true;
                weatherUrl = FormattableString.Invariant(
                    $"https://api.open-meteo.com/v1/forecast?latitude={coordinates.Latitude}&longitude={coordinates.Longitude}&current=temperature_2m,is_day,weather_code&timezone=auto");
            }
            else
            {
                _showingDeviceLocation = false;
                if (string.IsNullOrWhiteSpace(_city))
                    throw new InvalidOperationException(AppLanguage.Text(
                        "Stel een plaats in bij de weerinstellingen of schakel Windows-locatie in.",
                        "Set a city in weather settings or enable Windows location."));

                var city = Uri.EscapeDataString(_city.Trim());
                var language = AppLanguage.IsEnglish ? "en" : "nl";
                var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={city}&count=10&language={language}&format=json";
                var geocoding = await HttpClient.GetFromJsonAsync<GeocodingResponse>(geoUrl);
                var location = geocoding?.Results?.FirstOrDefault(result =>
                    string.Equals(result.CountryCode, _country.Trim(), StringComparison.OrdinalIgnoreCase));

                if (location is null)
                    throw new InvalidOperationException(AppLanguage.Text(
                        $"Geen locatie gevonden voor {_city}, {_country}.",
                        $"No location found for {_city}, {_country}."));

                weatherUrl = FormattableString.Invariant(
                    $"https://api.open-meteo.com/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}&current=temperature_2m,is_day,weather_code&timezone=auto");
            }

            var weather = await HttpClient.GetFromJsonAsync<WeatherResponse>(weatherUrl);
            if (weather?.Current is null)
                throw new InvalidOperationException(AppLanguage.Text(
                    "De weerservice gaf geen actuele weersgegevens terug.",
                    "The weather service returned no current weather data."));

            _weatherIcon = GetWeatherIcon(weather.Current.WeatherCode, weather.Current.IsDay == 1);
            _temperature = $"{Math.Round(weather.Current.Temperature):0}°C";
            UpdateClockToolTip();
        }
        catch (HttpRequestException exception)
        {
            SetWeatherUnavailable(exception.Message);
        }
        catch (TaskCanceledException exception)
        {
            SetWeatherUnavailable(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            SetWeatherUnavailable(exception.Message);
        }
        catch (JsonException exception)
        {
            SetWeatherUnavailable(exception.Message);
        }

        if (!IsDisposed)
            Invalidate();
    }

    private static async Task<BasicGeoposition?> GetDeviceLocationAsync()
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
                return null;

            var geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await geolocator.GetGeopositionAsync(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10));
            return position.Coordinate.Point.Position;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private void SetWeatherUnavailable(string reason)
    {
        _weatherIcon = "🌡️";
        _temperature = "?°C";
        _toolTip.SetToolTip(this,
            AppLanguage.IsEnglish
                ? $"Weather is unavailable: {reason}"
                : $"Het weer is niet beschikbaar: {reason}");
    }

    private Brush GetWeatherBrush() => new SolidBrush(_weatherIcon switch
    {
        "☀️" => Color.FromArgb(255, 216, 77),
        "🌙" or "🌤️" => Color.FromArgb(255, 230, 128),
        "☁️" => Color.FromArgb(208, 208, 208),
        "🌧️" or "🌦️" => Color.FromArgb(102, 204, 255),
        "🌩️" => Color.FromArgb(192, 132, 255),
        "❄️" => Color.FromArgb(174, 230, 255),
        "🌫️" => Color.FromArgb(187, 187, 187),
        _ => Color.White
    });

    private void SavePosition()
    {
        try
        {
            var screen = Screen.FromRectangle(new Rectangle(Location, Size));
            var workingArea = screen.WorkingArea;
            var offsetX = Location.X - workingArea.Left;
            var offsetY = Location.Y - workingArea.Top;
            File.WriteAllText(
                _positionPath,
                $"{screen.DeviceName}|{offsetX.ToString(CultureInfo.InvariantCulture)}|{offsetY.ToString(CultureInfo.InvariantCulture)}");
        }
        catch (IOException)
        {
            // Position persistence is optional if the application folder is read-only.
        }
        catch (UnauthorizedAccessException)
        {
            // Position persistence is optional if the application folder is read-only.
        }
    }

    private Point LoadPosition()
    {
        try
        {
            var savedPosition = File.ReadAllText(_positionPath).Trim();
            var parts = savedPosition.Split('|');
            if (parts.Length == 3 &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offsetX) &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offsetY))
            {
                var screen = Screen.AllScreens.FirstOrDefault(candidate =>
                    string.Equals(candidate.DeviceName, parts[0], StringComparison.OrdinalIgnoreCase))
                    ?? Screen.PrimaryScreen
                    ?? Screen.AllScreens[0];
                return GetVisiblePosition(screen.WorkingArea, offsetX, offsetY);
            }

            var legacyParts = savedPosition.Split(',');
            if (legacyParts.Length == 2 &&
                int.TryParse(legacyParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) &&
                int.TryParse(legacyParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                var legacyPosition = new Point(x, y);
                var screen = Screen.AllScreens.FirstOrDefault(candidate =>
                    candidate.Bounds.IntersectsWith(new Rectangle(legacyPosition, Size)));
                if (screen is not null)
                    return legacyPosition;
            }
        }
        catch (IOException)
        {
            // Use the default location if no saved position is available.
        }
        catch (UnauthorizedAccessException)
        {
            // Use the default location if the saved position cannot be read.
        }

        return new Point(100, 100);
    }

    private static Point GetVisiblePosition(Rectangle workingArea, int offsetX, int offsetY)
    {
        var maxX = Math.Max(workingArea.Left, workingArea.Right - WindowSize);
        var maxY = Math.Max(workingArea.Top, workingArea.Bottom - WindowSize);
        return new Point(
            (int)Math.Clamp((long)workingArea.Left + offsetX, workingArea.Left, maxX),
            (int)Math.Clamp((long)workingArea.Top + offsetY, workingArea.Top, maxY));
    }

    private static PointF PointAt(double radius, double angle) =>
        new((float)(Center + radius * Math.Sin(angle)), (float)(Center - radius * Math.Cos(angle)));

    private static void DrawHand(Graphics graphics, double length, double angleDegrees, Color color, float width)
    {
        var angle = angleDegrees * Math.PI / 180;
        using var pen = new Pen(color, width);
        graphics.DrawLine(pen, Center, Center, (float)(Center + length * Math.Sin(angle)),
            (float)(Center - length * Math.Cos(angle)));
    }

    private static void DrawCenteredText(Graphics graphics, string text, Font font, Brush brush, float x, float y)
    {
        var size = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, brush, x - size.Width / 2, y - size.Height / 2);
    }

    private static void DrawTimerText(
        Graphics graphics, string text, Font font, Color digitColor, Color separatorColor, Color borderColor,
        bool separatorsVisible, float centerX, float centerY)
    {
        var segments = text.Split(':');
        var separatorWidth = graphics.MeasureString(":", font).Width;
        const float boxHorizontalPadding = 3.5f;
        const float boxVerticalPadding = 1.5f;
        var segmentSizes = segments.Select(segment => graphics.MeasureString(segment, font)).ToArray();
        var boxWidths = segmentSizes.Select(size => size.Width + boxHorizontalPadding * 2).ToArray();
        var totalWidth = boxWidths.Sum() + separatorWidth * (segments.Length - 1);
        var x = centerX - totalWidth / 2;

        for (var i = 0; i < segments.Length; i++)
        {
            using var digitBrush = new SolidBrush(digitColor);
            var segmentSize = segmentSizes[i];
            var boxHeight = segmentSize.Height + boxVerticalPadding * 2;
            using (var borderPen = new Pen(borderColor, 1))
            {
                graphics.DrawRectangle(borderPen, x, centerY - boxHeight / 2, boxWidths[i], boxHeight);
            }

            graphics.DrawString(segments[i], font, digitBrush, x + boxHorizontalPadding,
                centerY - segmentSize.Height / 2);
            x += boxWidths[i];

            if (i < segments.Length - 1)
            {
                if (separatorsVisible)
                {
                    using var separatorBrush = new SolidBrush(separatorColor);
                    var separatorSize = graphics.MeasureString(":", font);
                    graphics.DrawString(":", font, separatorBrush, x, centerY - separatorSize.Height / 2);
                }

                x += separatorWidth;
            }
        }
    }

    private void DrawTimerLabel(Graphics graphics, Font font, Color color, float centerX, float centerY)
    {
        const string label = "TIMER";
        using var emphasisFont = new Font(font.FontFamily, font.Size + 1, FontStyle.Bold, font.Unit);
        using var brush = new SolidBrush(color);
        using var emphasisBrush = new SolidBrush(
            _darkTheme ? Color.FromArgb(229, 229, 229) : ClockAccentLight);
        var widths = label.Select(character =>
        {
            var characterFont = character == 'M' ? emphasisFont : font;
            return graphics.MeasureString(character.ToString(), characterFont).Width;
        }).ToArray();

        var x = centerX - widths.Sum() / 2;
        for (var i = 0; i < label.Length; i++)
        {
            var character = label[i].ToString();
            var characterFont = label[i] == 'M' ? emphasisFont : font;
            var size = graphics.MeasureString(character, characterFont);
            var y = centerY - size.Height / 3 - (label[i] == 'M' ? 15 : 0);
            graphics.DrawString(character, characterFont, label[i] == 'M' ? emphasisBrush : brush,
                x, y);
            x += widths[i];
        }
    }

    private static void DrawLocalizedDate(
        Graphics graphics, DateTime date, Font font, Color textColor, Color accentColor,
        float centerX, float centerY)
    {
        var weekNumber = ISOWeek.GetWeekOfYear(date);
        var culture = AppLanguage.DisplayCulture;
        var weekday = date.ToString("ddd", culture);
        var month = culture.DateTimeFormat.GetAbbreviatedMonthName(date.Month);
        using var boldFont = new Font(font, FontStyle.Bold);
        var runs = new (string Text, Color Color, Font Font)[]
        {
            (AppLanguage.Text("Week ", "Week "), textColor, boldFont),
            (weekNumber.ToString("00", culture), accentColor, boldFont),
            ($" - {weekday} ", textColor, boldFont),
            (date.Day.ToString(culture), accentColor, boldFont),
            ($" {month} ", textColor, boldFont),
            (date.Year.ToString(culture), accentColor, boldFont)
        };
        var runSizes = runs.Select(run => graphics.MeasureString(run.Text, run.Font)).ToArray();
        var totalWidth = runSizes.Sum(size => size.Width);
        var textHeight = runSizes.Max(size => size.Height);
        var x = centerX - totalWidth / 2;
        var previousTextRenderingHint = graphics.TextRenderingHint;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        try
        {
            for (var i = 0; i < runs.Length; i++)
            {
                using var brush = new SolidBrush(runs[i].Color);
                graphics.DrawString(runs[i].Text, runs[i].Font, brush, x, centerY - textHeight / 2);
                x += runSizes[i].Width;
            }
        }
        finally
        {
            graphics.TextRenderingHint = previousTextRenderingHint;
        }
    }

    private static void DrawTemperature(
        Graphics graphics, string temperature, Font font, Color textColor, Color valueColor, Color unitColor,
        float centerX, float centerY)
    {
        const string unit = "°C";
        if (!temperature.EndsWith(unit, StringComparison.Ordinal))
        {
            using var textBrush = new SolidBrush(textColor);
            DrawCenteredText(graphics, temperature, font, textBrush, centerX, centerY);
            return;
        }

        var value = temperature[..^unit.Length];
        var isNumericValue = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        using var valueFont = new Font(font, isNumericValue ? FontStyle.Bold : font.Style);
        var valueSize = graphics.MeasureString(value, valueFont);
        var unitSize = graphics.MeasureString(unit, font);
        var x = centerX - (valueSize.Width + unitSize.Width) / 2;
        using var valueBrush = new SolidBrush(isNumericValue ? valueColor : textColor);
        using var unitBrush = new SolidBrush(unitColor);
        graphics.DrawString(value, valueFont, valueBrush, x, centerY - valueSize.Height / 2);
        graphics.DrawString(unit, font, unitBrush, x + valueSize.Width, centerY - unitSize.Height / 2);
    }

    private static string GetWeatherIcon(int code, bool isDay) => code switch
    {
        0 => isDay ? "☀️" : "🌙",
        1 => isDay ? "🌤️" : "☁️",
        2 or 3 => "☁️",
        45 or 48 => "🌫️",
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "🌧️",
        71 or 73 or 75 or 77 or 85 or 86 => "❄️",
        95 or 96 or 99 => "🌩️",
        _ => "🌡️"
    };

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult>? Results { get; init; }
    }

    private sealed class GeocodingResult
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; init; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; init; }
    }

    private sealed class WeatherResponse
    {
        [JsonPropertyName("current")]
        public CurrentWeather? Current { get; init; }
    }

    private sealed class CurrentWeather
    {
        [JsonPropertyName("temperature_2m")]
        public double Temperature { get; init; }

        [JsonPropertyName("is_day")]
        public int IsDay { get; init; }

        [JsonPropertyName("weather_code")]
        public int WeatherCode { get; init; }
    }
}

using System.Globalization;
using System.Drawing.Drawing2D;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace TopKlok;

internal sealed class ClockForm : Form
{
    private const int WindowSize = 420;
    private const int Center = WindowSize / 2;
    private const int ClockRadius = 150;
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
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _weatherTimer = new() { Interval = 600_000 };
    private readonly System.Windows.Forms.Timer _menuAnimationTimer = new() { Interval = 15 };
    private readonly ToolTip _toolTip = new();
    private readonly Image _settingsIcon;
    private readonly Image _lightSettingsIcon;
    private readonly Image _pinnedSettingsIcon;
    private readonly string _themePath = Path.Combine(AppContext.BaseDirectory, "theme.txt");
    private readonly string _pinPath = Path.Combine(AppContext.BaseDirectory, "clock-pinned.txt");
    private readonly string _pauseDurationPath = Path.Combine(AppContext.BaseDirectory, "pause-duration.txt");
    private readonly string _screenOffDuringPausePath = Path.Combine(AppContext.BaseDirectory, "screen-off-during-pause.txt");
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
    private bool _monitorIsOff;
    private bool _timerElapsed;
    private bool _timerBlinkVisible = true;
    private int _timerBlinkTransitions;
    private DateTime _timerBlinkChangedAt;
    private string _selectedAlarmFile = AlarmFiles[0];
    private string _messageText = "Stel uw Alarmgeluid, Timertijd in en Geef uw Woonplaats en Land op voor het Weer ------>";
    private DateTime _menuAnimationStarted;
    private float _settingsIconRotation;
    private bool _darkTheme = true;
    private string _city = "Leiden";
    private string _country = "NL";
    private string _weatherIcon = "🌡️";
    private string _temperature = "?°C";
    private DateTimeOffset _weatherUpdatedAt = DateTimeOffset.MinValue;

    public ClockForm()
    {
        Text = "ADHD Klok";
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
        TopMost = _clockPinned;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Media", "Images", "Instellingen.png");
        using (var image = Image.FromFile(iconPath))
        {
            _settingsIcon = new Bitmap(image);
            _lightSettingsIcon = TintIcon(image, Color.Black);
            _pinnedSettingsIcon = TintIcon(image, Color.FromArgb(175, 238, 236, 224));
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
            _timerSound?.Dispose();
            _pendingMenu?.Dispose();
            _settingsIcon.Dispose();
            _lightSettingsIcon.Dispose();
            _pinnedSettingsIcon.Dispose();
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
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        var clockColor = _darkTheme ? ClockColor : SystemColors.ControlText;
        var textColor = _darkTheme ? Color.FromArgb(238, 236, 224) : SystemColors.ControlText;
        var minuteTickColor = _darkTheme ? Color.FromArgb(238, 236, 224) : SystemColors.GrayText;
        var hourTickColor = _darkTheme ? Color.Red : Color.DarkRed;
        using var secondsPen = new Pen(minuteTickColor, 0.5f);
        using var hourPen = new Pen(hourTickColor, 1.5f);
        using var numberBrush = new SolidBrush(textColor);
        using var numberFont = new Font("Calibri", 18, FontStyle.Regular);

        for (var i = 0; i < 60; i++)
        {
            var angle = i * 6 * Math.PI / 180;
            var start = PointAt(ClockRadius * 0.92, angle);
            var end = PointAt(ClockRadius, angle);
            graphics.DrawLine(secondsPen, start, end);
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = i * 30 * Math.PI / 180;
            graphics.DrawLine(hourPen, PointAt(ClockRadius * 0.88, angle), PointAt(ClockRadius, angle));
            var numberPosition = PointAt(ClockRadius * 0.75, angle);
            var number = i == 0 ? "12" : i.ToString(CultureInfo.InvariantCulture);
            DrawCenteredText(graphics, number, numberFont, numberBrush, numberPosition.X, numberPosition.Y);
        }

        var now = DateTime.Now;
        DrawHand(graphics, ClockRadius * 0.9, now.Second * 6, Color.FromArgb(170, Color.Red), 1);
        DrawHand(graphics, ClockRadius * 0.75, now.Minute * 6 + now.Second * 0.1,
            Color.FromArgb(145, clockColor), 3);
        DrawHand(graphics, ClockRadius * 0.5, (now.Hour % 12) * 30 + now.Minute * 0.5,
            Color.FromArgb(145, clockColor), 5);

        using (var centerBrush = new SolidBrush(Color.FromArgb(170, Color.Red)))
            graphics.FillEllipse(centerBrush, Center - 7, Center - 7, 14, 14);

        using var regularFont = new Font("Calibri", 12, FontStyle.Regular);
        using var timerFont = new Font("Calibri", 20, FontStyle.Regular);
        using var temperatureFont = new Font("Calibri", 15, FontStyle.Regular);
        using var emojiFont = new Font("Segoe UI Emoji", 20, FontStyle.Regular);
        using var messageFont = new Font("Calibri", 11, FontStyle.Regular);
        using var textBrush = new SolidBrush(textColor);
        using var weatherBrush = GetWeatherBrush();
        using var temperatureBrush = new SolidBrush(textColor);

        var remaining = GetRemainingTime();
        DrawTimerLabel(graphics, regularFont, _darkTheme ? Color.Red : Color.DarkRed,
            Center, WindowSize - 290);
        if (!_timerElapsed || _timerBlinkVisible)
        {
            var timerText = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            var timerDigitColor = _darkTheme ? Color.FromArgb(238, 236, 224) : SystemColors.ControlText;
            var timerSeparatorColor = _darkTheme ? Color.Red : Color.DarkRed;
            using var compactTimerFont = new Font(timerFont.FontFamily, timerFont.Size - 2, timerFont.Style);
            DrawTimerText(graphics, timerText, compactTimerFont, timerDigitColor, timerSeparatorColor,
                now.Second % 2 == 0, Center, WindowSize - 260);
        }
        DrawCenteredText(graphics, GetDutchDate(now), regularFont, textBrush, Center, WindowSize - 230);
        DrawCenteredText(graphics, $"{_city}, {_country}", regularFont, textBrush, Center, WindowSize - 190);
        DrawCenteredText(graphics, _weatherIcon, emojiFont, weatherBrush, Center, WindowSize - 160);
        DrawCenteredText(graphics, _temperature, temperatureFont, temperatureBrush, Center, WindowSize - 130);

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
                using var dividerPen = new Pen(_darkTheme ? Color.FromArgb(72, 16, 28) : SystemColors.ControlDark, 2);
                graphics.DrawLine(dividerPen, iconBounds.Left - 6, MessageArea.Y + 7,
                    iconBounds.Left - 6, MessageArea.Bottom - 8);
            }

            var graphicsState = graphics.Save();
            graphics.TranslateTransform(iconBounds.X + iconBounds.Width / 2f, iconBounds.Y + iconBounds.Height / 2f);
            graphics.RotateTransform(_settingsIconRotation);
            var settingsIcon = _clockPinned
                ? _pinnedSettingsIcon
                : _darkTheme ? _settingsIcon : _lightSettingsIcon;
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
            _messageText = "Timer opnieuw gestart.";
        }
        else if (e.KeyCode == Keys.N)
        {
            _awaitingPauseChoice = false;
            _timerEndsAt = null;
            _timerElapsed = false;
            _timerBlinkVisible = true;
            _timerBlinkTransitions = 0;
            _messageText = "Timer gestopt.";
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
        var darkThemeItem = new ToolStripMenuItem("Donker")
        {
            Checked = _darkTheme,
            CheckOnClick = false
        };
        var lightThemeItem = new ToolStripMenuItem("Licht (Windows)")
        {
            Checked = !_darkTheme,
            CheckOnClick = false
        };
        darkThemeItem.Click += (_, _) => SetTheme(true, darkThemeItem, lightThemeItem);
        lightThemeItem.Click += (_, _) => SetTheme(false, darkThemeItem, lightThemeItem);
        var appearanceMenu = new ToolStripMenuItem("Weergave");
        appearanceMenu.DropDownItems.Add(darkThemeItem);
        appearanceMenu.DropDownItems.Add(lightThemeItem);
        menu.Items.Add(appearanceMenu);
        menu.Items.Add(new ToolStripSeparator());
        var pinMenuItem = new ToolStripMenuItem("Klok vastzetten op bureaublad")
        {
            Checked = _clockPinned,
            CheckOnClick = false
        };
        pinMenuItem.Click += (_, _) => SetPinnedMode(!_clockPinned, pinMenuItem);
        menu.Items.Add(pinMenuItem);
        var startupMenuItem = new ToolStripMenuItem("Met Windows opstarten")
        {
            Checked = _startWithWindows,
            CheckOnClick = false
        };
        startupMenuItem.Click += (_, _) =>
            SetStartWithWindows(!_startWithWindows, startupMenuItem);
        menu.Items.Add(startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());

        var timerMenu = new ToolStripMenuItem("⏱ Timer & Pauze & Instellen");
        timerMenu.DropDownItems.Add("Timer instellen...", null, (_, _) => OpenTimerSettings());
        timerMenu.DropDownItems.Add("Pauzetijd instellen...", null, (_, _) => OpenPauseSettings());
        var screenOffDuringPauseItem = new ToolStripMenuItem("Scherm uitschakelen tijdens pauze")
        {
            Checked = _screenOffDuringPause,
            CheckOnClick = false
        };
        screenOffDuringPauseItem.Click += (_, _) =>
            SetScreenOffDuringPause(!_screenOffDuringPause, screenOffDuringPauseItem);
        timerMenu.DropDownItems.Add(screenOffDuringPauseItem);

        var alarmMenu = new ToolStripMenuItem("🎼 Alarm Geluid Instellen");
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
        menu.Items.Add("🌤 Weer instellen", null, (_, _) => OpenWeatherSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Afsluiten", null, (_, _) => Close());
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
        _toolTip.SetToolTip(this, _clockPinned ? string.Empty : "Klik en sleep om de klok te verplaatsen.");
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
        using var dialog = new TimerSettingsForm();
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
            _messageText = "Timer gestart.";
        }
        Invalidate();
    }

    private void OpenPauseSettings()
    {
        using var dialog = new TimerSettingsForm(
            "Pauzetijd instellen", _pauseDuration, "Pauzetijd opslaan");
        ApplyDialogTheme(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _pauseDuration = dialog.Duration;
        SavePauseDuration();
        _messageText = $"Pauzetijd ingesteld op {_pauseDuration:hh\\:mm\\:ss}.";
        Invalidate();
    }

    private void SetScreenOffDuringPause(bool enabled, ToolStripMenuItem menuItem)
    {
        _screenOffDuringPause = enabled;
        menuItem.Checked = enabled;
        SaveScreenOffDuringPause();
    }

    private void OpenWeatherSettings()
    {
        using var dialog = new WeatherSettingsForm(_city, _country);
        ApplyDialogTheme(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _city = dialog.City;
        _country = dialog.Country;
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
                $"De instelling voor opstarten met Windows kan niet worden gelezen:\n{exception.Message}",
                "Opstartinstelling niet beschikbaar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void SetStartWithWindows(bool enabled, ToolStripMenuItem menuItem)
    {
        try
        {
            using var startupKey = Registry.CurrentUser.CreateSubKey(WindowsStartupKey, writable: true)
                ?? throw new IOException("De Windows-opstartsleutel kon niet worden geopend.");

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
                $"De instelling voor opstarten met Windows kan niet worden opgeslagen:\n{exception.Message}",
                "Opstartinstelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            menuItem.Checked = _startWithWindows;
        }
    }

    private static string GetStartupCommand()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Het pad van de huidige app kon niet worden bepaald.");

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

    private void SavePauseDuration()
    {
        try
        {
            File.WriteAllText(_pauseDurationPath, _pauseDuration.ToString("c", CultureInfo.InvariantCulture));
        }
        catch (IOException exception)
        {
            MessageBox.Show(this, $"De pauzetijd kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this, $"De pauzetijd kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show(this, $"De scherminstelling kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this, $"De scherminstelling kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show(this, $"De instelling voor vastzetten kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this, $"De instelling voor vastzetten kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MessageBox.Show(this, $"De weergavekeuze kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            MessageBox.Show(this, $"De weergavekeuze kan niet worden opgeslagen:\n{exception.Message}",
                "Instelling niet opgeslagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _messageText = "Pauze voorbij. Dezelfde werktijd opnieuw? Druk Y voor ja of N voor nee.";
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
        _messageText = "Werktijd voorbij. Pauze gestart.";
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
            var action = turnOn ? "inschakelen" : "uitschakelen";
            MessageBox.Show(this, $"Het scherm kon niet {action} worden:\n{exception.Message}",
                "Scherminstelling niet toegepast", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            throw new InvalidOperationException("Windows reageerde niet op het verzoek om de monitorstand te wijzigen.");
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
            MessageBox.Show(this, $"Kan het timer-geluid niet laden:\n{timerSoundPath}\n\n{exception.Message}",
                "Timer-geluid niet beschikbaar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (InvalidOperationException exception)
        {
            _timerSound?.Dispose();
            _timerSound = null;
            MessageBox.Show(this, $"Kan het timer-geluid niet afspelen:\n{exception.Message}",
                "Timer-geluid niet beschikbaar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RefreshWeatherAsync()
    {
        if (IsDisposed || DateTimeOffset.Now - _weatherUpdatedAt < TimeSpan.FromMinutes(9))
            return;

        _weatherUpdatedAt = DateTimeOffset.Now;
        try
        {
            var city = Uri.EscapeDataString(_city.Trim());
            var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={city}&count=10&language=nl&format=json";
            var geocoding = await HttpClient.GetFromJsonAsync<GeocodingResponse>(geoUrl);
            var location = geocoding?.Results?.FirstOrDefault(result =>
                string.Equals(result.CountryCode, _country.Trim(), StringComparison.OrdinalIgnoreCase));

            if (location is null)
                throw new InvalidOperationException($"Geen locatie gevonden voor {_city}, {_country}.");

            var weatherUrl = FormattableString.Invariant(
                $"https://api.open-meteo.com/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}&current=temperature_2m,is_day,weather_code&timezone=auto");
            var weather = await HttpClient.GetFromJsonAsync<WeatherResponse>(weatherUrl);
            if (weather?.Current is null)
                throw new InvalidOperationException("De weerservice gaf geen actuele weersgegevens terug.");

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

    private void SetWeatherUnavailable(string reason)
    {
        _weatherIcon = "🌡️";
        _temperature = "?°C";
        _toolTip.SetToolTip(this, $"Het Weer is niet beschikbaar: {reason}");
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
            File.WriteAllText(_positionPath, $"{Location.X},{Location.Y}");
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
            var parts = File.ReadAllText(_positionPath).Split(',');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var x) &&
                int.TryParse(parts[1], out var y))
            {
                return new Point(x, y);
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
        Graphics graphics, string text, Font font, Color digitColor, Color separatorColor,
        bool separatorsVisible, float centerX, float centerY)
    {
        var segments = text.Split(':');
        var separatorWidth = graphics.MeasureString(":", font).Width;
        const float boxHorizontalPadding = 4;
        const float boxVerticalPadding = 2;
        var segmentSizes = segments.Select(segment => graphics.MeasureString(segment, font)).ToArray();
        var boxWidths = segmentSizes.Select(size => size.Width + boxHorizontalPadding * 2).ToArray();
        var totalWidth = boxWidths.Sum() + separatorWidth * (segments.Length - 1);
        var x = centerX - totalWidth / 2;

        for (var i = 0; i < segments.Length; i++)
        {
            using var digitBrush = new SolidBrush(digitColor);
            var segmentSize = segmentSizes[i];
            var boxHeight = segmentSize.Height + boxVerticalPadding * 2;
            using (var borderPen = new Pen(separatorColor, 1))
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

    private static void DrawTimerLabel(Graphics graphics, Font font, Color color, float centerX, float centerY)
    {
        const string label = "TIMER";
        using var emphasisFont = new Font(font.FontFamily, font.Size + 1, font.Style, font.Unit);
        using var brush = new SolidBrush(color);
        using var emphasisBrush = new SolidBrush(Color.FromArgb(238, 236, 224));
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
            graphics.DrawString(character, characterFont, label[i] == 'M' ? emphasisBrush : brush,
                x, centerY - size.Height / 2);
            x += widths[i];
        }
    }

    private static string GetDutchDate(DateTime date)
    {
        string[] months = ["Jan", "Feb", "Mrt", "Apr", "Mei", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dec"];
        string[] weekdays = ["Ma", "Di", "Wo", "Do", "Vr", "Za", "Zo"];

        var weekNumber = ISOWeek.GetWeekOfYear(date);
        var weekday = weekdays[((int)date.DayOfWeek + 6) % 7];

        return $"Week {weekNumber:00} - {weekday} {date.Day} {months[date.Month - 1]} {date.Year}";
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

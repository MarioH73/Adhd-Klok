using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdhdKlok;

internal sealed class ClockForm : Form
{
    private const int WindowSize = 420;
    private const int Center = WindowSize / 2;
    private const int ClockRadius = 150;
    private static readonly Rectangle MessageArea = new(32, WindowSize - 50, WindowSize - 64, 42);
    private static readonly string[] AlarmFiles = ["Alarm-01.wav", "Alarm-02.wav", "Alarm-03.wav", "Alarm-04.wav"];
    private static readonly Color BackgroundColor = Color.FromArgb(10, 10, 35);
    private static readonly Color ClockColor = Color.FromArgb(183, 190, 255);
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _weatherTimer = new() { Interval = 600_000 };
    private readonly System.Windows.Forms.Timer _menuAnimationTimer = new() { Interval = 15 };
    private readonly ToolTip _toolTip = new();
    private readonly Image _settingsIcon;
    private readonly Image _lightSettingsIcon;
    private readonly string _themePath = Path.Combine(AppContext.BaseDirectory, "theme.txt");
    private System.Media.SoundPlayer? _timerSound;
    private ContextMenuStrip? _pendingMenu;
    private ContextMenuStrip? _openMenu;
    private MenuAnimationState _menuAnimationState;
    private Size _menuExpandedSize;
    private bool _menuIsShown;
    private readonly string _positionPath = Path.Combine(AppContext.BaseDirectory, "position.txt");
    private Point _dragOffset;
    private bool _dragging;
    private DateTimeOffset? _timerEndsAt;
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
        BackColor = BackgroundColor;
        TransparencyKey = BackgroundColor;
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _darkTheme = LoadTheme();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Media", "Images", "Instellingen.png");
        using (var image = Image.FromFile(iconPath))
        {
            _settingsIcon = new Bitmap(image);
            _lightSettingsIcon = TintIcon(image, Color.Black);
        }

        Location = LoadPosition();
        _toolTip.SetToolTip(this, "Klik in de klok en sleep om teverplaatsen.");
        _refreshTimer.Tick += (_, _) => Invalidate();
        _weatherTimer.Tick += async (_, _) => await RefreshWeatherAsync();
        _menuAnimationTimer.Tick += AnimateMenuOpening;
        FormClosed += (_, _) =>
        {
            _timerSound?.Dispose();
            _pendingMenu?.Dispose();
            _settingsIcon.Dispose();
            _lightSettingsIcon.Dispose();
            _menuAnimationTimer.Dispose();
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
        var tickColor = _darkTheme ? Color.FromArgb(123, 136, 252) : SystemColors.GrayText;
        using var secondsPen = new Pen(tickColor, 0.5f);
        using var hourPen = new Pen(clockColor, 1.5f);
        using var numberBrush = new SolidBrush(clockColor);
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
        using var textBrush = new SolidBrush(clockColor);
        using var weatherBrush = GetWeatherBrush();
        using var temperatureBrush = new SolidBrush(Color.FromArgb(102, 204, 255));

        var remaining = GetRemainingTime();
        DrawCenteredText(graphics, "Timer", regularFont, textBrush, Center, WindowSize - 290);
        DrawCenteredText(graphics, $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}",
            timerFont, textBrush, Center, WindowSize - 260);
        DrawCenteredText(graphics, GetDutchDate(now), regularFont, textBrush, Center, WindowSize - 230);
        DrawCenteredText(graphics, $"{_city}, {_country}", regularFont, textBrush, Center, WindowSize - 190);
        DrawCenteredText(graphics, _weatherIcon, emojiFont, weatherBrush, Center, WindowSize - 160);
        DrawCenteredText(graphics, _temperature, temperatureFont, temperatureBrush, Center, WindowSize - 130);

        using var messageBackground = new SolidBrush(_darkTheme ? Color.FromArgb(24, 24, 64) : SystemColors.Control);
        using var messageBorder = new Pen(_darkTheme ? Color.FromArgb(110, ClockColor) : SystemColors.ActiveBorder, 1);
        graphics.FillRectangle(messageBackground, MessageArea);
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
        graphics.DrawString(_messageText, messageFont, textBrush, textArea, messageFormat);

        var iconBounds = GetSettingsIconBounds();
        using (var dividerPen = new Pen(_darkTheme ? Color.FromArgb(72, 16, 28) : SystemColors.ControlDark, 2))
            graphics.DrawLine(dividerPen, iconBounds.Left - 6, MessageArea.Y + 7,
                iconBounds.Left - 6, MessageArea.Bottom - 8);

        var graphicsState = graphics.Save();
        graphics.TranslateTransform(iconBounds.X + iconBounds.Width / 2f, iconBounds.Y + iconBounds.Height / 2f);
        graphics.RotateTransform(_settingsIconRotation);
        graphics.DrawImage(_darkTheme ? _settingsIcon : _lightSettingsIcon,
            new Rectangle(-iconBounds.Width / 2, -iconBounds.Height / 2, iconBounds.Width, iconBounds.Height));
        graphics.Restore(graphicsState);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
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
        Cursor = MessageArea.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        if (_dragging && e.Button == MouseButtons.Left)
        {
            Location = new Point(System.Windows.Forms.Cursor.Position.X - _dragOffset.X,
                System.Windows.Forms.Cursor.Position.Y - _dragOffset.Y);
            SavePosition();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
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

        var timerMenu = new ToolStripMenuItem("⏱ Timer");
        timerMenu.DropDownItems.Add("Timer instellen...", null, (_, _) => OpenTimerSettings());

        var alarmMenu = new ToolStripMenuItem("Alarmgeluid");
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

        timerMenu.DropDownItems.Add(new ToolStripSeparator());
        timerMenu.DropDownItems.Add(alarmMenu);
        menu.Items.Add(timerMenu);
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
        const double animationDurationMilliseconds = 600;
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

        var iconBounds = GetSettingsIconBounds();
        menu.Show(this, new Point(iconBounds.Right, iconBounds.Bottom), ToolStripDropDownDirection.BelowLeft);
        menu.Height = 1;
        _menuIsShown = true;
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

    private Rectangle GetSettingsIconBounds() => new(MessageArea.Right - 36, MessageArea.Y + 9, 24, 24);

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
            _timerEndsAt = DateTimeOffset.Now.Add(dialog.Duration);
            _messageText = "Timer gestart.";
        }
        Invalidate();
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
        if (!_darkTheme)
            return;

        dialog.BackColor = BackgroundColor;
        dialog.ForeColor = ClockColor;
        ApplyControlTheme(dialog.Controls);
    }

    private static void ApplyControlTheme(Control.ControlCollection controls)
    {
        foreach (Control control in controls)
        {
            control.ForeColor = ClockColor;
            switch (control)
            {
                case Button button:
                    button.BackColor = Color.FromArgb(24, 24, 64);
                    button.FlatStyle = FlatStyle.Flat;
                    break;
                case TextBox or NumericUpDown:
                    control.BackColor = Color.FromArgb(32, 32, 56);
                    break;
                default:
                    control.BackColor = BackgroundColor;
                    break;
            }

            ApplyControlTheme(control.Controls);
        }
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
        if (_timerEndsAt is not { } end)
            return TimeSpan.Zero;

        var remaining = end - DateTimeOffset.Now;
        if (remaining > TimeSpan.Zero)
            return remaining;

        _timerEndsAt = null;
        _messageText = "Neem nu 10 minuten pauze.";
        PlayTimerSound();
        return TimeSpan.Zero;
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
            _toolTip.SetToolTip(this, "Klik en sleep om de klok te verplaatsen.");
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

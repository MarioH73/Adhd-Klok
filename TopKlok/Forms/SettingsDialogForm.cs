namespace TopKlok;

internal abstract class SettingsDialogForm : Form
{
    private static readonly Color BorderColor = Color.Red;
    private static readonly Color TitleBarColor = Color.FromArgb(72, 16, 28);
    private static readonly Color TitleTextColor = Color.FromArgb(238, 236, 224);
    private const int BorderWidth = 1;
    private const int TitleBarHeight = 34;

    private readonly Panel _titleBar;
    private readonly Label _titleLabel;
    private Point _dragStart;
    private Point _formStart;

    protected SettingsDialogForm(string title, Size contentSize)
    {
        Text = title;
        ClientSize = new Size(contentSize.Width, contentSize.Height + TitleBarHeight + BorderWidth * 2);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = BorderColor;
        Padding = new Padding(BorderWidth);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _titleBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = TitleBarColor,
            Margin = Padding.Empty
        };
        _titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = TitleTextColor,
            BackColor = TitleBarColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        var closeButton = new Button
        {
            Text = "×",
            Dock = DockStyle.Right,
            Width = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = TitleBarColor,
            ForeColor = TitleTextColor,
            TabStop = false
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(120, 24, 36);
        closeButton.Click += (_, _) =>
        {
            WasClosedWithCloseButton = true;
            Close();
        };
        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(closeButton);

        ContentArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control,
            Margin = Padding.Empty
        };

        layout.Controls.Add(_titleBar, 0, 0);
        layout.Controls.Add(ContentArea, 0, 1);
        Controls.Add(layout);

        _titleBar.MouseDown += BeginTitleBarDrag;
        _titleLabel.MouseDown += BeginTitleBarDrag;
        _titleBar.MouseMove += ContinueTitleBarDrag;
        _titleLabel.MouseMove += ContinueTitleBarDrag;
    }

    protected Control ContentArea { get; }

    public bool WasClosedWithCloseButton { get; private set; }

    public void ApplyTheme(bool darkTheme)
    {
        ContentArea.BackColor = darkTheme ? Color.FromArgb(10, 10, 35) : SystemColors.Control;
        ApplyControlTheme(ContentArea.Controls, darkTheme);
    }

    private void BeginTitleBarDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _dragStart = Cursor.Position;
        _formStart = Location;
    }

    private void ContinueTitleBarDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var cursor = Cursor.Position;
            Location = new Point(_formStart.X + cursor.X - _dragStart.X, _formStart.Y + cursor.Y - _dragStart.Y);
        }
    }

    private static void ApplyControlTheme(Control.ControlCollection controls, bool darkTheme)
    {
        foreach (Control control in controls)
        {
            control.ForeColor = darkTheme ? TitleTextColor : SystemColors.ControlText;
            switch (control)
            {
                case Button button when darkTheme:
                    button.BackColor = Color.FromArgb(24, 24, 64);
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = BorderColor;
                    break;
                case TextBox or NumericUpDown when darkTheme:
                    control.BackColor = Color.FromArgb(32, 32, 56);
                    break;
                default:
                    control.BackColor = darkTheme ? Color.FromArgb(10, 10, 35) : SystemColors.Control;
                    break;
            }

            ApplyControlTheme(control.Controls, darkTheme);
        }
    }
}
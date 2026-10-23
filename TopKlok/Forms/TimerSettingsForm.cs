namespace TopKlok;

internal sealed class TimerSettingsForm : SettingsDialogForm
{
    private readonly NumericUpDown _hours = CreateInput(24);
    private readonly NumericUpDown _minutes = CreateInput(59);
    private readonly NumericUpDown _seconds = CreateInput(59);

    public TimeSpan Duration { get; private set; }

    public TimerSettingsForm(
        string title = "Set timer",
        TimeSpan? initialDuration = null,
        string startButtonText = "Start timer") : base(title, new Size(260, 205))
    {
        var duration = initialDuration.GetValueOrDefault();
        _hours.Value = Math.Min((decimal)Math.Floor(duration.TotalHours), _hours.Maximum);
        _minutes.Value = duration.Minutes;
        _seconds.Value = duration.Seconds;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        AddRow(layout, 0, AppLanguage.Text("Uren:", "Hours:"), _hours);
        AddRow(layout, 1, AppLanguage.Text("Minuten:", "Minutes:"), _minutes);
        AddRow(layout, 2, AppLanguage.Text("Seconden:", "Seconds:"), _seconds);

        var startButton = new Button { Text = startButtonText, Dock = DockStyle.Fill };
        startButton.Click += (_, _) =>
        {
            Duration = TimeSpan.FromHours((double)_hours.Value) +
                       TimeSpan.FromMinutes((double)_minutes.Value) +
                       TimeSpan.FromSeconds((double)_seconds.Value);
            if (Duration <= TimeSpan.Zero)
            {
                MessageBox.Show(this,
                    AppLanguage.Text("Stel een tijd groter dan nul in.", "Enter a duration greater than zero."),
                    AppLanguage.Text("Ongeldige timer", "Invalid timer"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        layout.Controls.Add(startButton, 0, 3);
        layout.SetColumnSpan(startButton, 2);
        ContentArea.Controls.Add(layout);
        AcceptButton = startButton;
    }

    private static NumericUpDown CreateInput(int maximum) => new()
    {
        Minimum = 0,
        Maximum = maximum,
        Dock = DockStyle.Fill
    };

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control input)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(input, 1, row);
    }
}

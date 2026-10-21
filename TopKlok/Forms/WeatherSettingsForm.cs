namespace TopKlok;

internal sealed class WeatherSettingsForm : SettingsDialogForm
{
    private readonly TextBox _cityInput;
    private readonly TextBox _countryInput;

    public string City => _cityInput.Text.Trim();
    public string Country => _countryInput.Text.Trim().ToUpperInvariant();

    public WeatherSettingsForm(string city, string country) : base("Weerinstellingen", new Size(300, 185))
    {
        _cityInput = new TextBox { Text = city, Dock = DockStyle.Fill };
        _countryInput = new TextBox { Text = country, Dock = DockStyle.Fill, MaxLength = 2 };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        layout.Controls.Add(new Label { Text = "Plaats:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_cityInput, 1, 0);
        layout.Controls.Add(new Label { Text = "Landcode:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_countryInput, 1, 1);

        var saveButton = new Button { Text = "Opslaan", Dock = DockStyle.Fill };
        saveButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(City) || Country.Length != 2)
            {
                MessageBox.Show(this, "Vul een plaats en een landcode van twee letters in.",
                    "Ongeldige invoer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        layout.Controls.Add(saveButton, 0, 2);
        layout.SetColumnSpan(saveButton, 2);
        ContentArea.Controls.Add(layout);
        AcceptButton = saveButton;
    }
}


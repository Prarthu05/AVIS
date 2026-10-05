using Avis.Faults;

namespace Avis.App.Ui;

/// <summary>
/// Blocking fault popup for the operator (from the JabilEye middleware), styled
/// with the Jabil brand palette: Fail red banner, Dark Navy body, Bright Blue Reset.
/// </summary>
internal class ErrorDialogForm : Form
{
    private static readonly Color BrandBg = Color.FromArgb(0x00, 0x2B, 0x49);       // Very Dark Navy
    private static readonly Color BrandPanel = Color.FromArgb(0x00, 0x3C, 0x6B);    // Dark Navy
    private static readonly Color BrandPanelBorder = Color.FromArgb(0x0E, 0x54, 0x90);
    private static readonly Color BrandFail = Color.FromArgb(0xBD, 0x28, 0x32);     // Red
    private static readonly Color BrandAccent = Color.FromArgb(0x43, 0xB7, 0xFF);   // Bright Blue

    public ErrorDialogForm(FaultCode code, string message, string stationLabel)
    {
        Text = stationLabel;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        TopMost = true;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        BackColor = BrandBg;
        ClientSize = new Size(560, 320);

        var banner = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = BrandFail };
        var logoBox = new PictureBox
        {
            Image = AssetImages.TryLoad("jabil-logo.jpeg"),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(40, 40),
            Location = new Point(20, 16),
        };
        var codeLabel = new Label
        {
            Text = $"{code.ToDisplayCode()} - {code.ToTitle()}",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(72, 0, 20, 0),
        };
        banner.Controls.Add(codeLabel);
        banner.Controls.Add(logoBox);

        var resetButton = new Button
        {
            Text = "Reset",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = BrandAccent,
            ForeColor = BrandPanel,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
        };
        resetButton.FlatAppearance.BorderSize = 0;
        resetButton.Click += (_, _) => Close();
        AcceptButton = resetButton;

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 57, BackColor = BrandPanel };
        footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = BrandPanelBorder });
        footer.Controls.Add(resetButton);
        resetButton.BringToFront();

        var messagePanel = new Panel { Dock = DockStyle.Fill, BackColor = BrandPanel, Padding = new Padding(24) };
        messagePanel.Controls.Add(new Label
        {
            Text = message,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11),
            Dock = DockStyle.Fill,
        });

        Controls.Add(messagePanel);
        Controls.Add(footer);
        Controls.Add(banner);
    }
}

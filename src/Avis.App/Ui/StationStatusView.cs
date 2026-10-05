using Avis.Station;

namespace Avis.App.Ui;

/// <summary>
/// The operator's view of the current part: a big colour-coded banner (what to do
/// now - scan badge, rework, escalate, move to next station), the part's IDs,
/// LightGuide program/step and the latest result of every check.
/// </summary>
public class StationStatusView : UserControl
{
    private readonly Label _banner;
    private readonly Label _operator = ValueLabel();
    private readonly Label _asset = ValueLabel();
    private readonly Label _wip = ValueLabel();
    private readonly Label _model = ValueLabel();
    private readonly Label _program = ValueLabel();
    private readonly Label _step = ValueLabel();
    private readonly ListView _checks;

    public StationStatusView()
    {
        BackColor = Color.White;

        _banner = new Label
        {
            Dock = DockStyle.Top,
            Height = 120,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Padding = new Padding(12),
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 6 * 34 + 12,
            ColumnCount = 2,
            Padding = new Padding(12, 6, 12, 6),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(grid, "OPERATOR (NTID)", _operator);
        AddRow(grid, "ASSET ID", _asset);
        AddRow(grid, "WIP / MATERIAL", _wip);
        AddRow(grid, "MODEL", _model);
        AddRow(grid, "LIGHTGUIDE PROGRAM", _program);
        AddRow(grid, "STEP", _step);

        _checks = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 12F),
        };
        _checks.Columns.Add("Check", 110);
        _checks.Columns.Add("Result", 90);
        _checks.Columns.Add("Value", 140);
        _checks.Columns.Add("Attempts", 90);
        _checks.Columns.Add("Fails", 70);
        _checks.Columns.Add("Time", 100);
        _checks.Columns.Add("Image", 300);

        var checksTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "  CHECKS (LJ / IV4) - LATEST RESULT PER CHECK",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(0xE8, 0xEE, 0xF4),
        };

        Controls.Add(_checks);
        Controls.Add(checksTitle);
        Controls.Add(grid);
        Controls.Add(_banner);
    }

    public void Render(StationState state)
    {
        _banner.Text = state.Message;
        _banner.BackColor = PhaseColor(state.Phase, state.MessageIsError);

        _operator.Text = state.OperatorId ?? "-";
        _asset.Text = state.AssetId ?? "-";
        _wip.Text = state.WipId is null ? "-" : $"{state.WipId}  /  {state.Material}";
        _model.Text = state.Model ?? "-";
        _program.Text = state.Program ?? "-";
        _step.Text = state.Step is null ? "-" : string.IsNullOrWhiteSpace(state.StepComment) ? $"{state.Step}" : $"{state.Step} - {state.StepComment}";

        _checks.BeginUpdate();
        _checks.Items.Clear();
        foreach (var check in state.Checks.OrderBy(c => c.Name))
        {
            var item = new ListViewItem(new[]
            {
                check.Name,
                check.Passed ? "OK" : "NG",
                check.Value,
                check.Attempts.ToString(),
                check.Failures.ToString(),
                check.At.ToLocalTime().ToString("HH:mm:ss"),
                check.ImagePath is null ? "" : Path.GetFileName(check.ImagePath),
            })
            {
                BackColor = check.Passed ? Color.FromArgb(0xDF, 0xF5, 0xE3) : Color.FromArgb(0xFB, 0xDE, 0xDE),
            };
            _checks.Items.Add(item);
        }
        _checks.EndUpdate();
    }

    private static Color PhaseColor(StationPhase phase, bool isError) => phase switch
    {
        StationPhase.Completed => Color.FromArgb(0x1E, 0x8E, 0x3E),
        StationPhase.WaitingForBadge => Color.FromArgb(0xE0, 0x8A, 0x00),
        StationPhase.ReworkRequired => Color.FromArgb(0xD9, 0x4F, 0x00),
        StationPhase.EscalationRequired => Color.FromArgb(0x8B, 0x00, 0x00),
        StationPhase.NotConfirmed => Color.FromArgb(0xBD, 0x28, 0x32),
        StationPhase.StartingWip or StationPhase.Completing or StationPhase.InProcess => Color.FromArgb(0x00, 0x3C, 0x6B),
        _ => isError ? Color.FromArgb(0xBD, 0x28, 0x32) : Color.FromArgb(0x5A, 0x67, 0x75),
    };

    private static Label ValueLabel() => new()
    {
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 14F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true,
        Text = "-",
    };

    private static void AddRow(TableLayoutPanel grid, string caption, Label value)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        grid.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = caption,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(0x5A, 0x67, 0x75),
            TextAlign = ContentAlignment.MiddleLeft,
        });
        grid.Controls.Add(value);
    }
}

using System.ComponentModel;
using System.Diagnostics;
using Avis.Faults;

namespace Avis.App.Ui;

/// <summary>
/// "Dashboard: fault feed" - live list of every fault at this station, newest
/// first, colour-coded by severity and filterable by integration point. The
/// same records are in faults\faults-yyyyMMdd.jsonl (and the shared folder, if
/// configured) for the techs'/engineers' central dashboard.
/// </summary>
public class FaultFeedView : UserControl
{
    private const string AllPoints = "All integration points";

    private readonly FaultLog _faults;
    private readonly FaultLogOptions _options;
    private readonly DataGridView _grid;
    private readonly ComboBox _filter;
    private readonly Label _summary;
    private readonly BindingList<FaultRow> _rows = new();
    private readonly List<FaultRecord> _all = new();

    public FaultFeedView(FaultLog faults, FaultLogOptions options)
    {
        _faults = faults;
        _options = options;
        BackColor = Color.White;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 6, 8, 0) };
        _filter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Font = new Font("Segoe UI", 10F) };
        _filter.Items.Add(AllPoints);
        foreach (var point in Enum.GetValues<FaultCode>().Select(c => c.ToIntegrationPoint()).Distinct())
        {
            _filter.Items.Add(point);
        }
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => Rebuild();

        var openFolder = new Button { Text = "Open fault log folder", AutoSize = true, Font = new Font("Segoe UI", 10F) };
        openFolder.Click += (_, _) => OpenFolder();

        _summary = new Label { AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Padding = new Padding(12, 6, 0, 0) };

        toolbar.Controls.Add(_filter);
        toolbar.Controls.Add(openFolder);
        toolbar.Controls.Add(_summary);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            BackgroundColor = Color.White,
            Font = new Font("Segoe UI", 10F),
            DataSource = _rows,
        };
        _grid.DataBindingComplete += (_, _) =>
        {
            if (_grid.Columns[nameof(FaultRow.Message)] is { } message)
            {
                message.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        };
        _grid.CellFormatting += OnCellFormatting;

        Controls.Add(_grid);
        Controls.Add(toolbar);

        _all.AddRange(faults.Recent);
        Rebuild();
        faults.FaultRecorded += OnFaultRecorded;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _faults.FaultRecorded -= OnFaultRecorded;
        }
        base.Dispose(disposing);
    }

    private void OnFaultRecorded(FaultRecord record)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            lock (_all)
            {
                _all.Insert(0, record);
            }
            return;
        }
        try
        {
            BeginInvoke(() =>
            {
                lock (_all)
                {
                    _all.Insert(0, record);
                    if (_all.Count > Math.Max(1, _options.MaxInMemory))
                    {
                        _all.RemoveAt(_all.Count - 1);
                    }
                }
                if (Matches(record))
                {
                    _rows.Insert(0, new FaultRow(record));
                    if (_rows.Count > Math.Max(1, _options.MaxInMemory))
                    {
                        _rows.RemoveAt(_rows.Count - 1);
                    }
                }
                UpdateSummary();
            });
        }
        catch (InvalidOperationException)
        {
            // window closing
        }
    }

    private void Rebuild()
    {
        _rows.RaiseListChangedEvents = false;
        _rows.Clear();
        lock (_all)
        {
            foreach (var record in _all.Where(Matches))
            {
                _rows.Add(new FaultRow(record));
            }
        }
        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();
        UpdateSummary();
    }

    private bool Matches(FaultRecord record) =>
        _filter.SelectedItem is not string point || point == AllPoints || record.IntegrationPoint == point;

    private void UpdateSummary()
    {
        lock (_all)
        {
            var today = _all.Where(f => f.Timestamp.Date == DateTime.Today).ToList();
            _summary.Text = $"Today: {today.Count} faults  ({today.Count(f => f.Severity >= FaultSeverity.Error)} errors, " +
                            $"{today.Count(f => f.Severity == FaultSeverity.Critical)} escalations)";
        }
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.CellStyle is null)
        {
            return;
        }
        e.CellStyle.BackColor = _rows[e.RowIndex].SeverityLevel switch
        {
            FaultSeverity.Critical => Color.FromArgb(0xF4, 0xB6, 0xB6),
            FaultSeverity.Error => Color.FromArgb(0xFB, 0xDE, 0xDE),
            FaultSeverity.Warning => Color.FromArgb(0xFF, 0xF4, 0xD6),
            _ => Color.White,
        };
    }

    private void OpenFolder()
    {
        try
        {
            var dir = Path.IsPathRooted(_options.Directory) ? _options.Directory : Path.Combine(AppContext.BaseDirectory, _options.Directory);
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AVIS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private sealed class FaultRow
    {
        public FaultRow(FaultRecord record)
        {
            Time = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            Code = record.Code;
            Point = record.IntegrationPoint;
            Severity = record.Severity.ToString();
            SeverityLevel = record.Severity;
            Title = record.Title;
            Message = record.Message;
            Asset = record.AssetId ?? "";
            Operator = record.OperatorId ?? "";
            Wip = record.WipId?.ToString() ?? "";
        }

        public string Time { get; }
        public string Code { get; }
        public string Point { get; }
        public string Severity { get; }
        [Browsable(false)]
        public FaultSeverity SeverityLevel { get; }
        public string Title { get; }
        public string Message { get; }
        public string Asset { get; }
        public string Operator { get; }
        public string Wip { get; }
    }
}

using Avis.App.Simulation;
using Avis.App.Ui;
using Avis.Configuration;
using Avis.Faults;
using Avis.Station;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace Avis.App
{
    /// <summary>
    /// The station window. One window, three screens that swap in place: the
    /// station screen (iFactory web UI + live part status), the visual aid for the
    /// current LightGuide step, and the fault feed. ScreenSelector decides when the
    /// visual aid or the station screen comes forward; the sidebar switches by hand.
    /// All station logic lives in the workers/StationSequencer - this form only
    /// renders StationStatus snapshots and hosts operator popups.
    /// </summary>
    public partial class Main : Form
    {
        private static readonly Color LinkOk = Color.FromArgb(0x6B, 0xE0, 0x8A);
        private static readonly Color LinkError = Color.FromArgb(0xFF, 0x7A, 0x7A);
        private static readonly Color LinkUnknown = Color.FromArgb(0xC8, 0xD0, 0xD8);

        private readonly StationStatus _status;
        private readonly AvisOptions _avis;
        private readonly EnvironmentSelection _environment;
        private readonly ILogger<Main> _logger;
        private readonly StationStatusView _stationView;
        private readonly SimulatorView? _simulator;
        private readonly Label _visualAidStatus;

        private bool _visualAidReady;
        private string? _shownVisualAidUrl;
        private StationState? _lastRendered;

        public Main(
            StationStatus status,
            FaultLog faults,
            FaultLogOptions faultLogOptions,
            AvisOptions avis,
            EnvironmentSelection environment,
            OperatorAlertService alerts,
            ILogger<Main> logger,
            SimulatorView? simulator = null)
        {
            InitializeComponent();

            _status = status;
            _avis = avis;
            _environment = environment;
            _logger = logger;
            _simulator = simulator;

            _stationView = new StationStatusView { Dock = DockStyle.Fill };
            pnl_MainShow.Controls.Add(_stationView);
            tab_Faults.Controls.Add(new FaultFeedView(faults, faultLogOptions) { Dock = DockStyle.Fill });

            // Slim strip over the visual aid so the operator still sees which part /
            // step they're on without leaving it. Added after the Fill-docked
            // WebView2 so it is docked first and takes the top edge.
            _visualAidStatus = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(0x00, 0x3C, 0x6B),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                AutoEllipsis = true,
            };
            tab_VisualAid.Controls.Add(_visualAidStatus);

            // The screens swap in place like pages of one window: hide the tab
            // strip at runtime (it stays visible in the designer so each page can
            // still be edited) and switch with the sidebar / ScreenSelector instead.
            tab_Main.Appearance = TabAppearance.FlatButtons;
            tab_Main.ItemSize = new Size(0, 1);
            tab_Main.SizeMode = TabSizeMode.Fixed;
            tab_Main.SelectedIndexChanged += (_, _) => HighlightSidebar();
            HighlightSidebar();

            ApplyImages();
            ApplyMode();

            // A fault popup means the operator is needed - bring the station screen forward first.
            alerts.Attach(this, onAlert: () => ShowScreen(StationScreen.Station));
            _status.Changed += OnStatusChanged;
            Load += Main_Load;
            FormClosed += (_, _) => _status.Changed -= OnStatusChanged;
        }

        #region -- STARTUP --

        private async void Main_Load(object? sender, EventArgs e)
        {
            Render(_status.Current);
            ShowSimulatorWindow();

            // async void is unavoidable for an event handler - so nothing may escape it.
            try
            {
                await InitWebViewsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebView2 initialisation failed");
                ShowIFactoryError(ex is WebView2RuntimeNotFoundException
                    ? "Microsoft Edge WebView2 Runtime is not installed on this PC - iFactory and visual aids can't be shown."
                    : $"Browser panel failed to start: {ex.Message}");
            }
        }

        private async Task InitWebViewsAsync()
        {
            // A fixed, always-writable profile folder (not the exe folder, which may
            // be read-only under Program Files) so the iFactory login is remembered.
            var userDataFolder = string.IsNullOrWhiteSpace(_avis.WebView2UserDataFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVIS", "WebView2")
                : _avis.WebView2UserDataFolder;
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            await vv_IFactory.EnsureCoreWebView2Async(env);
            KeepInThisWindow(vv_IFactory.CoreWebView2);
            vv_IFactory.ZoomFactor = _avis.WebZoomFactor;
            if (TryCreateUri(_environment.Urls.WebUrl, out var ifactoryUri))
            {
                vv_IFactory.CoreWebView2.Navigate(ifactoryUri.AbsoluteUri);
            }
            else
            {
                ShowIFactoryError($"No iFactory web URL configured for {_environment.Name} (Avis:{_environment.Name}:WebUrl).");
            }

            await vv_VisualAid.EnsureCoreWebView2Async(env);
            KeepInThisWindow(vv_VisualAid.CoreWebView2);
            vv_VisualAid.ZoomFactor = 1.0;
            _visualAidReady = true;
            ShowVisualAid(_status.Current.VisualAidUrl);

            // A step may already be running by the time the browser is ready.
            if (_avis.AutoShowVisualAid && ScreenSelector.Decide(null, _status.Current, _visualAidReady) == StationScreen.VisualAid)
            {
                ShowScreen(StationScreen.VisualAid);
            }
        }

        /// <summary>
        /// Links that would open a new browser window (target="_blank",
        /// window.open) open in the same panel instead - the station never spawns
        /// extra windows. Alt+Left goes back.
        /// </summary>
        private void KeepInThisWindow(CoreWebView2 webView)
        {
            webView.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile))
                {
                    webView.Navigate(uri.AbsoluteUri);
                }
                else
                {
                    _logger.LogInformation("Blocked a pop-up window for {Uri}", e.Uri);
                }
            };
        }

        /// <summary>
        /// Simulation mode: the simulator lives in its own window next to the station
        /// screen, so the station screen behaves exactly as it would on the floor
        /// (including switching to the visual aid on each step).
        /// </summary>
        private void ShowSimulatorWindow()
        {
            if (_simulator is null)
            {
                return;
            }
            var window = new Form
            {
                Text = "AVIS Simulator - drives the station without hardware",
                Size = new Size(1220, 720),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(
                    Math.Max(Screen.FromControl(this).WorkingArea.Left, Screen.FromControl(this).WorkingArea.Right - 1240),
                    Math.Max(Screen.FromControl(this).WorkingArea.Top, Screen.FromControl(this).WorkingArea.Bottom - 740)),
                ShowInTaskbar = false,
                MinimizeBox = true,
            };
            _simulator.Dock = DockStyle.Fill;
            window.Controls.Add(_simulator);
            // Owned by the station window: stays on top of it and closes with it.
            window.FormClosing += (_, args) =>
            {
                if (args.CloseReason == CloseReason.UserClosing)
                {
                    args.Cancel = true;
                    window.WindowState = FormWindowState.Minimized;
                }
            };
            window.Show(this);
        }

        private void ApplyImages()
        {
            if (AssetImages.TryLoad("cvg.png", "cvg.jpg", "CVG.png") is { } cvg)
            {
                btn_Exit.BackgroundImage = cvg;
                btn_Exit.Text = "";
            }
            if (AssetImages.TryLoad("home.png", "Home.png") is { } home)
            {
                btn_Station.BackgroundImage = home;
                btn_Station.Text = "";
            }
            pic_Logo.BackgroundImage = AssetImages.TryLoad("jabil.png", "JABIL.png", "jabil-logo.jpeg");
            if (pic_Logo.BackgroundImage is { } logo && logo.Width < logo.Height * 2)
            {
                // Zoom centres the image - a square logo would float mid-header in the
                // 778px box sized for the wide JABIL wordmark, so hug the right edge.
                pic_Logo.Width = pnl_Header.Height;
            }
        }

        private void ApplyMode()
        {
            lbl_Mode.Text = _environment.Name == "SIMULATION" ? "SIMULATION" : $"{_environment.Name} MODE";
            (lbl_Mode.ForeColor, lbl_Mode.BackColor) = _environment.Name switch
            {
                "PRD" => (Color.White, Color.FromArgb(0x1E, 0x8E, 0x3E)),
                "SIMULATION" => (Color.Black, Color.FromArgb(0xFF, 0xC1, 0x07)),
                _ => (Color.Black, Color.FromArgb(0xFF, 0x5A, 0x5A)),
            };
        }

        #endregion

        #region -- RENDERING --

        private void OnStatusChanged(StationState state)
        {
            // Raised from worker threads - marshal to the UI thread, never block the worker.
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke(() => Render(state));
            }
            catch (InvalidOperationException)
            {
                // handle destroyed while closing
            }
        }

        private void Render(StationState state)
        {
            if (IsDisposed)
            {
                return;
            }
            // BeginInvoke can deliver snapshots out of order - always render the latest.
            state = _status.Current;

            lbl_Title.Text = string.IsNullOrWhiteSpace(state.ResourceName) ? "AVIS" : $"AVIS  -  {state.ResourceName}";
            lbl_Model.Text = state.Model ?? state.Material ?? "-";
            lbl_Station.Text = state.StationName;
            lbl_PassCount.Text = state.PassCount.ToString();
            lbl_FailCount.Text = state.FailCount.ToString();
            lbl_Yield.Text = state.YieldPercent is { } yield ? $"{yield:0.#} %" : "-";

            SetLink(lbl_LinkScanner, "BADGE SCANNER", state.Scanner);
            SetLink(lbl_LinkCamera, "JABILEYE", state.Camera);
            SetLink(lbl_LinkIFactory, "IFACTORY", state.IFactory);
            SetLink(lbl_LinkLightGuide, "LIGHTGUIDE", state.LightGuide);

            _stationView.Render(state);
            ShowVisualAid(state.VisualAidUrl);
            _visualAidStatus.Text = string.Join("   •   ", new[]
            {
                state.AssetId,
                state.Model ?? state.Program,
                state.Step is null ? null : string.IsNullOrWhiteSpace(state.StepComment) ? $"Step {state.Step}" : $"Step {state.Step} - {state.StepComment}",
                state.Message,
            }.Where(t => !string.IsNullOrWhiteSpace(t)));

            // Visual aid while the operator is building, station screen whenever
            // they're needed - swapped in place, only when something changed.
            if (_avis.AutoShowVisualAid && ScreenSelector.Decide(_lastRendered, state, _visualAidReady) is { } screen)
            {
                ShowScreen(screen);
            }
            _lastRendered = state;
        }

        private void ShowScreen(StationScreen screen)
        {
            if (IsDisposed)
            {
                return;
            }
            var page = screen == StationScreen.VisualAid ? tab_VisualAid : tab_Production;
            if (tab_Main.SelectedTab != page)
            {
                tab_Main.SelectedTab = page;
            }
        }

        private void HighlightSidebar()
        {
            foreach (var (button, page) in new[] { (btn_Station, tab_Production), (btn_VisualAid, tab_VisualAid), (btn_Faults, tab_Faults) })
            {
                var active = tab_Main.SelectedTab == page;
                button.UseVisualStyleBackColor = !active;
                button.BackColor = active ? Color.FromArgb(0xCC, 0xE4, 0xF7) : SystemColors.Control;
                button.ForeColor = active ? Color.FromArgb(0x00, 0x3C, 0x6B) : SystemColors.ControlText;
            }
        }

        private void ShowVisualAid(string? url)
        {
            if (!_visualAidReady || url == _shownVisualAidUrl)
            {
                return;
            }
            _shownVisualAidUrl = url;
            if (url is null)
            {
                return;
            }
            if (TryCreateUri(url, out var uri))
            {
                try
                {
                    vv_VisualAid.CoreWebView2.Navigate(uri.AbsoluteUri);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not open visual aid {Url}", url);
                }
            }
            else
            {
                _logger.LogWarning("Visual aid '{Url}' is not a valid URL or file path", url);
            }
        }

        private static void SetLink(Label label, string name, ConnectionState state)
        {
            label.Text = $"● {name}";
            label.ForeColor = state switch
            {
                ConnectionState.Ok => LinkOk,
                ConnectionState.Error => LinkError,
                _ => LinkUnknown,
            };
        }

        private void ShowIFactoryError(string message)
        {
            lbl_IFactoryError.Text = message;
            lbl_IFactoryError.Visible = true;
        }

        /// <summary>
        /// Accepts http(s) URLs, local/UNC file paths (e.g. a PDF visual aid on a
        /// share) and paths relative to the AVIS folder.
        /// </summary>
        private static bool TryCreateUri(string? value, out Uri uri)
        {
            uri = null!;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            var text = value.Trim();
            if (!Uri.TryCreate(text, UriKind.Absolute, out uri!))
            {
                if (Path.IsPathRooted(text) || !File.Exists(Path.Combine(AppContext.BaseDirectory, text)))
                {
                    return false;
                }
                uri = new Uri(Path.Combine(AppContext.BaseDirectory, text));
            }
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile;
        }

        #endregion

        #region -- ACTUATORS --

        private void btn_Station_Click(object sender, EventArgs e) => tab_Main.SelectedTab = tab_Production;

        private void btn_VisualAid_Click(object sender, EventArgs e) => tab_Main.SelectedTab = tab_VisualAid;

        private void btn_Faults_Click(object sender, EventArgs e) => tab_Main.SelectedTab = tab_Faults;

        private void btn_Exit_Click(object sender, EventArgs e)
        {
            // Closing stops badge/camera/LightGuide processing for the station - confirm first.
            var answer = MessageBox.Show(this,
                "Close AVIS? Badge scans, JabilEye captures and LightGuide results will stop being processed.",
                "AVIS", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
            {
                Close();
            }
        }

        #endregion
    }
}

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
    /// The station window: iFactory web UI on the left, the live part status on
    /// the right, the per-step visual aid and the fault feed on their own tabs.
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

        private bool _visualAidReady;
        private string? _shownVisualAidUrl;
        private int? _lastStep;
        private StationPhase _lastPhase = StationPhase.Idle;

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

            ApplyImages();
            ApplyMode();

            alerts.Attach(this);
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
            vv_VisualAid.ZoomFactor = 1.0;
            _visualAidReady = true;
            ShowVisualAid(_status.Current.VisualAidUrl);
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

            // Follow the operator: visual aid while LightGuide steps through the
            // program, back to the station view whenever action is needed.
            if (_avis.AutoShowVisualAid)
            {
                if (state.Step != _lastStep && state.Step is not null && state.VisualAidUrl is not null
                    && state.Phase == StationPhase.InProcess)
                {
                    tab_Main.SelectedTab = tab_VisualAid;
                }
                else if (state.Phase != _lastPhase && state.Phase is not StationPhase.InProcess)
                {
                    tab_Main.SelectedTab = tab_Production;
                }
            }
            _lastStep = state.Step;
            _lastPhase = state.Phase;
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

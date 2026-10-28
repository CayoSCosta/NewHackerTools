using System.Text.Json;

namespace RubinotTunnelAgent
{
    public partial class MainForm : Form
    {
        private TcpForwarder? _forwarder;
        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RubinotTunnelAgent", "config.json");

        public MainForm()
        {
            InitializeComponent();

            LoadConfig();

            if (_chkAutoStart.Checked)
            {
                // Pequeno atraso pra UI terminar de montar antes de iniciar
                Shown += (_, _) => ToggleTunnel();
            }
        }

        // --- Handlers wireados no InitializeComponent (aparecem na aba Eventos do Designer) ---

        private void BtnToggle_Click(object sender, EventArgs e) => ToggleTunnel();

        private void MenuAbrir_Click(object? sender, EventArgs e) => RestoreFromTray();

        private void MenuToggleTunnel_Click(object? sender, EventArgs e) => ToggleTunnel();

        private void MenuSair_Click(object? sender, EventArgs e) => ExitApplication();

        private void TrayIcon_DoubleClick(object? sender, EventArgs e) => RestoreFromTray();

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_chkMinimizeToTray.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.Visible = true;
                _trayIcon.ShowBalloonTip(1500, "Rubinot Tunnel Agent",
                    "O agente continua rodando em segundo plano.", ToolTipIcon.Info);
                return;
            }

            SaveConfig();
            _forwarder?.Stop();
            _forwarder?.Dispose();
            _trayIcon.Visible = false;
        }

        // --- Lógica do túnel ---

        private void ToggleTunnel()
        {
            if (_forwarder is { IsRunning: true })
            {
                _forwarder.Stop();
                _forwarder.Dispose();
                _forwarder = null;
                SetStatus(running: false);
                SaveConfig();
                return;
            }

            var relayHost = _txtRelayHost.Text.Trim();
            if (string.IsNullOrWhiteSpace(relayHost))
            {
                AppendLog("Informe o endereço do relay antes de iniciar.");
                MessageBox.Show(this, "Informe o endereço do relay antes de iniciar o túnel.",
                    "Configuração incompleta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _forwarder = new TcpForwarder(
                localPort: (int)_numLocalPort.Value,
                relayHost: relayHost,
                relayPort: (int)_numRelayPort.Value);

            _forwarder.OnLog += msg => BeginInvoke(() => AppendLog(msg));
            _forwarder.OnActiveConnectionsChanged += count =>
                BeginInvoke(() => _lblConnections.Text = $"Conexões ativas: {count}");

            try
            {
                _forwarder.Start();
                SetStatus(running: true);
                SaveConfig();
            }
            catch (Exception ex)
            {
                AppendLog($"Falha ao iniciar: {ex.Message}");
                MessageBox.Show(this, $"Não foi possível iniciar o túnel na porta {_numLocalPort.Value}.\n\n{ex.Message}",
                    "Erro ao iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _forwarder = null;
            }
        }

        private void SetStatus(bool running)
        {
            _lblStatus.Text = running ? "● Ativo" : "● Parado";
            _lblStatus.ForeColor = running ? Color.SeaGreen : Color.Firebrick;
            _btnToggle.Text = running ? "Parar Túnel" : "Iniciar Túnel";
            _txtRelayHost.Enabled = !running;
            _numRelayPort.Enabled = !running;
            _numLocalPort.Enabled = !running;
            _trayIcon.Text = running
                ? $"Rubinot Tunnel Agent - Ativo (porta {_numLocalPort.Value})"
                : "Rubinot Tunnel Agent - Parado";
        }

        private void AppendLog(string message)
        {
            _txtLog.AppendText(message + Environment.NewLine);
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _trayIcon.Visible = false;
        }

        private void ExitApplication()
        {
            _chkMinimizeToTray.Checked = false;
            Close();
            Application.Exit();
        }

        // --- Persistência simples de configuração em %LocalAppData% ---

        private record AgentConfig(string RelayHost, int RelayPort, int LocalPort, bool AutoStart, bool MinimizeToTray);

        private void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath)!;
                Directory.CreateDirectory(dir);

                var config = new AgentConfig(
                    _txtRelayHost.Text.Trim(),
                    (int)_numRelayPort.Value,
                    (int)_numLocalPort.Value,
                    _chkAutoStart.Checked,
                    _chkMinimizeToTray.Checked);

                File.WriteAllText(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Não é crítico se falhar em salvar a config
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return;

                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AgentConfig>(json);
                if (config is null) return;

                _txtRelayHost.Text = config.RelayHost;
                _numRelayPort.Value = Math.Clamp(config.RelayPort, (int)_numRelayPort.Minimum, (int)_numRelayPort.Maximum);
                _numLocalPort.Value = Math.Clamp(config.LocalPort, (int)_numLocalPort.Minimum, (int)_numLocalPort.Maximum);
                _chkAutoStart.Checked = config.AutoStart;
                _chkMinimizeToTray.Checked = config.MinimizeToTray;
            }
            catch
            {
                // Config corrompida ou ausente: segue com os valores padrão
            }
        }
    }
}

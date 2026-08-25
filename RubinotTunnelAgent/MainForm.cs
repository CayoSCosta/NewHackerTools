using System.Text.Json;

namespace RubinotTunnelAgent
{
    public class MainForm : Form
    {
        private readonly TextBox _txtRelayHost = new() { PlaceholderText = "ex: relay.seudominio.com" };
        private readonly NumericUpDown _numRelayPort = new() { Minimum = 1, Maximum = 65535, Value = 7171 };
        private readonly NumericUpDown _numLocalPort = new() { Minimum = 1, Maximum = 65535, Value = 7171 };
        private readonly Button _btnToggle = new() { Text = "Iniciar Túnel", Width = 140 };
        private readonly Label _lblStatus = new() { Text = "● Parado", ForeColor = Color.Firebrick, AutoSize = true };
        private readonly Label _lblConnections = new() { Text = "Conexões ativas: 0", AutoSize = true };
        private readonly CheckBox _chkAutoStart = new() { Text = "Iniciar túnel automaticamente ao abrir" };
        private readonly CheckBox _chkMinimizeToTray = new() { Text = "Minimizar para a bandeja ao fechar", Checked = true };
        private readonly TextBox _txtLog = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9)
        };

        private readonly NotifyIcon _trayIcon;
        private TcpForwarder? _forwarder;
        private readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RubinotTunnelAgent", "config.json");

        public MainForm()
        {
            Text = "Rubinot Tunnel Agent";
            Width = 560;
            Height = 480;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(480, 400);

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Visible = false,
                Text = "Rubinot Tunnel Agent"
            };
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Abrir", null, (_, _) => RestoreFromTray());
            trayMenu.Items.Add(_forwarderToggleTrayLabel(), null, (_, _) => ToggleTunnel());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Sair", null, (_, _) => ExitApplication());
            _trayIcon.ContextMenuStrip = trayMenu;

            BuildLayout();
            LoadConfig();

            _btnToggle.Click += (_, _) => ToggleTunnel();
            FormClosing += MainForm_FormClosing;

            if (_chkAutoStart.Checked)
            {
                // Pequeno atraso pra UI terminar de montar antes de iniciar
                Shown += (_, _) => ToggleTunnel();
            }
        }

        private string _forwarderToggleTrayLabel() => "Iniciar/Parar Túnel";

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // --- Configuração ---
            var configGroup = new GroupBox { Text = "Configuração do túnel", Dock = DockStyle.Fill, Height = 130 };
            var configLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            configLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            configLayout.Controls.Add(new Label { Text = "Endereço do relay:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
            configLayout.Controls.Add(_txtRelayHost, 1, 0);
            _txtRelayHost.Dock = DockStyle.Fill;

            configLayout.Controls.Add(new Label { Text = "Porta do relay:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, 1);
            configLayout.Controls.Add(_numRelayPort, 1, 1);

            configLayout.Controls.Add(new Label { Text = "Porta local (aponte o client aqui):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, 2);
            configLayout.Controls.Add(_numLocalPort, 1, 2);

            configGroup.Controls.Add(configLayout);
            root.Controls.Add(configGroup, 0, 0);

            // --- Opções ---
            var optionsPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill };
            optionsPanel.Controls.Add(_chkAutoStart);
            optionsPanel.Controls.Add(_chkMinimizeToTray);
            root.Controls.Add(optionsPanel, 0, 1);

            // --- Status / botão ---
            var statusPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Fill };
            _btnToggle.Margin = new Padding(0, 0, 15, 0);
            statusPanel.Controls.Add(_btnToggle);
            statusPanel.Controls.Add(_lblStatus);
            _lblConnections.Margin = new Padding(20, 6, 0, 0);
            statusPanel.Controls.Add(_lblConnections);
            root.Controls.Add(statusPanel, 0, 2);

            // --- Log ---
            var logGroup = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
            logGroup.Controls.Add(_txtLog);
            root.Controls.Add(logGroup, 0, 3);

            Controls.Add(root);
        }

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

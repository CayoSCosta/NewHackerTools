namespace RubinotTunnelAgent
{
    partial class MainForm
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpa os recursos que estão sendo usados.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this._root = new System.Windows.Forms.TableLayoutPanel();
            this._configGroup = new System.Windows.Forms.GroupBox();
            this._configLayout = new System.Windows.Forms.TableLayoutPanel();
            this._lblRelayHost = new System.Windows.Forms.Label();
            this._txtRelayHost = new System.Windows.Forms.TextBox();
            this._lblRelayPort = new System.Windows.Forms.Label();
            this._numRelayPort = new System.Windows.Forms.NumericUpDown();
            this._lblLocalPort = new System.Windows.Forms.Label();
            this._numLocalPort = new System.Windows.Forms.NumericUpDown();
            this._optionsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._chkAutoStart = new System.Windows.Forms.CheckBox();
            this._chkMinimizeToTray = new System.Windows.Forms.CheckBox();
            this._statusPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._btnToggle = new System.Windows.Forms.Button();
            this._lblStatus = new System.Windows.Forms.Label();
            this._lblConnections = new System.Windows.Forms.Label();
            this._logGroup = new System.Windows.Forms.GroupBox();
            this._txtLog = new System.Windows.Forms.TextBox();
            this._trayIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this._trayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this._menuAbrir = new System.Windows.Forms.ToolStripMenuItem();
            this._menuToggleTunnel = new System.Windows.Forms.ToolStripMenuItem();
            this._menuSeparator = new System.Windows.Forms.ToolStripSeparator();
            this._menuSair = new System.Windows.Forms.ToolStripMenuItem();

            this._root.SuspendLayout();
            this._configGroup.SuspendLayout();
            this._configLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._numRelayPort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._numLocalPort)).BeginInit();
            this._optionsPanel.SuspendLayout();
            this._statusPanel.SuspendLayout();
            this._logGroup.SuspendLayout();
            this._trayMenu.SuspendLayout();
            this.SuspendLayout();
            //
            // _root
            //
            this._root.ColumnCount = 1;
            this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._root.Controls.Add(this._configGroup, 0, 0);
            this._root.Controls.Add(this._optionsPanel, 0, 1);
            this._root.Controls.Add(this._statusPanel, 0, 2);
            this._root.Controls.Add(this._logGroup, 0, 3);
            this._root.Dock = System.Windows.Forms.DockStyle.Fill;
            this._root.Location = new System.Drawing.Point(0, 0);
            this._root.Name = "_root";
            this._root.Padding = new System.Windows.Forms.Padding(12);
            this._root.RowCount = 4;
            // IMPORTANTE: linhas Absolute (não AutoSize) porque os filhos usam
            // Dock=Fill. AutoSize + Dock=Fill juntos fazem a linha colapsar
            // pra altura 0 e os campos somem visualmente.
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 55F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._root.Size = new System.Drawing.Size(560, 480);
            this._root.TabIndex = 0;
            //
            // _configGroup
            //
            this._configGroup.Controls.Add(this._configLayout);
            this._configGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._configGroup.Location = new System.Drawing.Point(15, 15);
            this._configGroup.Name = "_configGroup";
            this._configGroup.Size = new System.Drawing.Size(530, 150);
            this._configGroup.TabIndex = 0;
            this._configGroup.TabStop = false;
            this._configGroup.Text = "Configuração do túnel";
            //
            // _configLayout
            //
            this._configLayout.ColumnCount = 2;
            this._configLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this._configLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._configLayout.Controls.Add(this._lblRelayHost, 0, 0);
            this._configLayout.Controls.Add(this._txtRelayHost, 1, 0);
            this._configLayout.Controls.Add(this._lblRelayPort, 0, 1);
            this._configLayout.Controls.Add(this._numRelayPort, 1, 1);
            this._configLayout.Controls.Add(this._lblLocalPort, 0, 2);
            this._configLayout.Controls.Add(this._numLocalPort, 1, 2);
            this._configLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._configLayout.Location = new System.Drawing.Point(3, 19);
            this._configLayout.Name = "_configLayout";
            this._configLayout.Padding = new System.Windows.Forms.Padding(10);
            this._configLayout.RowCount = 3;
            this._configLayout.Size = new System.Drawing.Size(524, 128);
            this._configLayout.TabIndex = 0;
            //
            // _lblRelayHost
            //
            this._lblRelayHost.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._lblRelayHost.AutoSize = true;
            this._lblRelayHost.Location = new System.Drawing.Point(13, 18);
            this._lblRelayHost.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this._lblRelayHost.Name = "_lblRelayHost";
            this._lblRelayHost.Size = new System.Drawing.Size(97, 15);
            this._lblRelayHost.TabIndex = 0;
            this._lblRelayHost.Text = "Endereço do relay:";
            //
            // _txtRelayHost
            //
            this._txtRelayHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtRelayHost.Location = new System.Drawing.Point(116, 13);
            this._txtRelayHost.Name = "_txtRelayHost";
            this._txtRelayHost.PlaceholderText = "ex: relay.seudominio.com";
            this._txtRelayHost.Size = new System.Drawing.Size(395, 23);
            this._txtRelayHost.TabIndex = 1;
            //
            // _lblRelayPort
            //
            this._lblRelayPort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._lblRelayPort.AutoSize = true;
            this._lblRelayPort.Location = new System.Drawing.Point(13, 47);
            this._lblRelayPort.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this._lblRelayPort.Name = "_lblRelayPort";
            this._lblRelayPort.Size = new System.Drawing.Size(85, 15);
            this._lblRelayPort.TabIndex = 2;
            this._lblRelayPort.Text = "Porta do relay:";
            //
            // _numRelayPort
            //
            this._numRelayPort.Location = new System.Drawing.Point(116, 42);
            this._numRelayPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this._numRelayPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._numRelayPort.Name = "_numRelayPort";
            this._numRelayPort.Size = new System.Drawing.Size(120, 23);
            this._numRelayPort.TabIndex = 3;
            this._numRelayPort.Value = new decimal(new int[] { 7171, 0, 0, 0 });
            //
            // _lblLocalPort
            //
            this._lblLocalPort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._lblLocalPort.AutoSize = true;
            this._lblLocalPort.Location = new System.Drawing.Point(13, 76);
            this._lblLocalPort.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this._lblLocalPort.Name = "_lblLocalPort";
            this._lblLocalPort.Size = new System.Drawing.Size(211, 15);
            this._lblLocalPort.TabIndex = 4;
            this._lblLocalPort.Text = "Porta local (aponte o client aqui):";
            //
            // _numLocalPort
            //
            this._numLocalPort.Location = new System.Drawing.Point(116, 71);
            this._numLocalPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this._numLocalPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._numLocalPort.Name = "_numLocalPort";
            this._numLocalPort.Size = new System.Drawing.Size(120, 23);
            this._numLocalPort.TabIndex = 5;
            this._numLocalPort.Value = new decimal(new int[] { 7171, 0, 0, 0 });
            //
            // _optionsPanel
            //
            this._optionsPanel.AutoSize = true;
            this._optionsPanel.Controls.Add(this._chkAutoStart);
            this._optionsPanel.Controls.Add(this._chkMinimizeToTray);
            this._optionsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._optionsPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this._optionsPanel.Location = new System.Drawing.Point(15, 171);
            this._optionsPanel.Name = "_optionsPanel";
            this._optionsPanel.Size = new System.Drawing.Size(530, 64);
            this._optionsPanel.TabIndex = 1;
            //
            // _chkAutoStart
            //
            this._chkAutoStart.AutoSize = true;
            this._chkAutoStart.Location = new System.Drawing.Point(3, 3);
            this._chkAutoStart.Name = "_chkAutoStart";
            this._chkAutoStart.Size = new System.Drawing.Size(240, 19);
            this._chkAutoStart.TabIndex = 0;
            this._chkAutoStart.Text = "Iniciar túnel automaticamente ao abrir";
            this._chkAutoStart.UseVisualStyleBackColor = true;
            //
            // _chkMinimizeToTray
            //
            this._chkMinimizeToTray.AutoSize = true;
            this._chkMinimizeToTray.Checked = true;
            this._chkMinimizeToTray.CheckState = System.Windows.Forms.CheckState.Checked;
            this._chkMinimizeToTray.Location = new System.Drawing.Point(3, 28);
            this._chkMinimizeToTray.Name = "_chkMinimizeToTray";
            this._chkMinimizeToTray.Size = new System.Drawing.Size(221, 19);
            this._chkMinimizeToTray.TabIndex = 1;
            this._chkMinimizeToTray.Text = "Minimizar para a bandeja ao fechar";
            this._chkMinimizeToTray.UseVisualStyleBackColor = true;
            //
            // _statusPanel
            //
            this._statusPanel.AutoSize = true;
            this._statusPanel.Controls.Add(this._btnToggle);
            this._statusPanel.Controls.Add(this._lblStatus);
            this._statusPanel.Controls.Add(this._lblConnections);
            this._statusPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._statusPanel.Location = new System.Drawing.Point(15, 241);
            this._statusPanel.Name = "_statusPanel";
            this._statusPanel.Size = new System.Drawing.Size(530, 49);
            this._statusPanel.TabIndex = 2;
            //
            // _btnToggle
            //
            this._btnToggle.Location = new System.Drawing.Point(3, 3);
            this._btnToggle.Margin = new System.Windows.Forms.Padding(0, 0, 15, 0);
            this._btnToggle.Name = "_btnToggle";
            this._btnToggle.Size = new System.Drawing.Size(140, 29);
            this._btnToggle.TabIndex = 0;
            this._btnToggle.Text = "Iniciar Túnel";
            this._btnToggle.UseVisualStyleBackColor = true;
            this._btnToggle.Click += new System.EventHandler(this.BtnToggle_Click);
            //
            // _lblStatus
            //
            this._lblStatus.AutoSize = true;
            this._lblStatus.ForeColor = System.Drawing.Color.Firebrick;
            this._lblStatus.Location = new System.Drawing.Point(158, 7);
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new System.Drawing.Size(52, 15);
            this._lblStatus.TabIndex = 1;
            this._lblStatus.Text = "● Parado";
            //
            // _lblConnections
            //
            this._lblConnections.AutoSize = true;
            this._lblConnections.Location = new System.Drawing.Point(230, 13);
            this._lblConnections.Margin = new System.Windows.Forms.Padding(20, 6, 0, 0);
            this._lblConnections.Name = "_lblConnections";
            this._lblConnections.Size = new System.Drawing.Size(110, 15);
            this._lblConnections.TabIndex = 2;
            this._lblConnections.Text = "Conexões ativas: 0";
            //
            // _logGroup
            //
            this._logGroup.Controls.Add(this._txtLog);
            this._logGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._logGroup.Location = new System.Drawing.Point(15, 296);
            this._logGroup.Name = "_logGroup";
            this._logGroup.Size = new System.Drawing.Size(530, 169);
            this._logGroup.TabIndex = 3;
            this._logGroup.TabStop = false;
            this._logGroup.Text = "Log";
            //
            // _txtLog
            //
            this._txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this._txtLog.Font = new System.Drawing.Font("Consolas", 9F);
            this._txtLog.Location = new System.Drawing.Point(3, 19);
            this._txtLog.Multiline = true;
            this._txtLog.Name = "_txtLog";
            this._txtLog.ReadOnly = true;
            this._txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._txtLog.Size = new System.Drawing.Size(524, 147);
            this._txtLog.TabIndex = 0;
            //
            // _trayIcon
            //
            this._trayIcon.ContextMenuStrip = this._trayMenu;
            this._trayIcon.Icon = System.Drawing.SystemIcons.Shield;
            this._trayIcon.Text = "Rubinot Tunnel Agent";
            this._trayIcon.DoubleClick += new System.EventHandler(this.TrayIcon_DoubleClick);
            //
            // _trayMenu
            //
            this._trayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._menuAbrir,
            this._menuToggleTunnel,
            this._menuSeparator,
            this._menuSair});
            this._trayMenu.Name = "_trayMenu";
            this._trayMenu.Size = new System.Drawing.Size(181, 76);
            //
            // _menuAbrir
            //
            this._menuAbrir.Name = "_menuAbrir";
            this._menuAbrir.Size = new System.Drawing.Size(180, 22);
            this._menuAbrir.Text = "Abrir";
            this._menuAbrir.Click += new System.EventHandler(this.MenuAbrir_Click);
            //
            // _menuToggleTunnel
            //
            this._menuToggleTunnel.Name = "_menuToggleTunnel";
            this._menuToggleTunnel.Size = new System.Drawing.Size(180, 22);
            this._menuToggleTunnel.Text = "Iniciar/Parar Túnel";
            this._menuToggleTunnel.Click += new System.EventHandler(this.MenuToggleTunnel_Click);
            //
            // _menuSeparator
            //
            this._menuSeparator.Name = "_menuSeparator";
            this._menuSeparator.Size = new System.Drawing.Size(177, 6);
            //
            // _menuSair
            //
            this._menuSair.Name = "_menuSair";
            this._menuSair.Size = new System.Drawing.Size(180, 22);
            this._menuSair.Text = "Sair";
            this._menuSair.Click += new System.EventHandler(this.MenuSair_Click);
            //
            // MainForm
            //
            this.ClientSize = new System.Drawing.Size(560, 480);
            this.Controls.Add(this._root);
            this.MinimumSize = new System.Drawing.Size(496, 439);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Rubinot Tunnel Agent";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this._root.ResumeLayout(false);
            this._configGroup.ResumeLayout(false);
            this._configLayout.ResumeLayout(false);
            this._configLayout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._numRelayPort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._numLocalPort)).EndInit();
            this._optionsPanel.ResumeLayout(false);
            this._optionsPanel.PerformLayout();
            this._statusPanel.ResumeLayout(false);
            this._statusPanel.PerformLayout();
            this._logGroup.ResumeLayout(false);
            this._logGroup.PerformLayout();
            this._trayMenu.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel _root;
        private System.Windows.Forms.GroupBox _configGroup;
        private System.Windows.Forms.TableLayoutPanel _configLayout;
        private System.Windows.Forms.Label _lblRelayHost;
        private System.Windows.Forms.TextBox _txtRelayHost;
        private System.Windows.Forms.Label _lblRelayPort;
        private System.Windows.Forms.NumericUpDown _numRelayPort;
        private System.Windows.Forms.Label _lblLocalPort;
        private System.Windows.Forms.NumericUpDown _numLocalPort;
        private System.Windows.Forms.FlowLayoutPanel _optionsPanel;
        private System.Windows.Forms.CheckBox _chkAutoStart;
        private System.Windows.Forms.CheckBox _chkMinimizeToTray;
        private System.Windows.Forms.FlowLayoutPanel _statusPanel;
        private System.Windows.Forms.Button _btnToggle;
        private System.Windows.Forms.Label _lblStatus;
        private System.Windows.Forms.Label _lblConnections;
        private System.Windows.Forms.GroupBox _logGroup;
        private System.Windows.Forms.TextBox _txtLog;
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private System.Windows.Forms.ContextMenuStrip _trayMenu;
        private System.Windows.Forms.ToolStripMenuItem _menuAbrir;
        private System.Windows.Forms.ToolStripMenuItem _menuToggleTunnel;
        private System.Windows.Forms.ToolStripSeparator _menuSeparator;
        private System.Windows.Forms.ToolStripMenuItem _menuSair;
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Core.Telemetry;
using ZeroGraphics.Direct2D.Controls;
using ZeroGraphics.DirectX.Controls;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.Waveform.Controls;

namespace ZeroGraphics.Samples.Demo
{
    public sealed class MainDemoForm : Form
    {
        private readonly TabControl _tabControl;
        private readonly System.Windows.Forms.Timer _waveformTimer;
        private readonly float[] _waveformBuffer = new float[100000];
        private float _wavePhase = 0f;
        private ZeroWaveformCanvas? _waveformCanvas;
        private ZeroDirectXCanvas? _directXCanvas;
        private ZeroDirect2DCanvas? _direct2DCanvas;

        public MainDemoForm()
        {
            Text = "ZeroGraphics - High-Performance Hardware Graphics Ecosystem Demo";
            Size = new Size(1100, 750);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(16, 18, 24);
            ForeColor = Color.FromArgb(220, 225, 235);

            // Initialize GPU Hardware & Telemetry
            D3D11DeviceManager.EnsureInitialized();

            // Top Status Panel (GPU Telemetry)
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblGpuInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 229, 255),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"[GPU Adapter] {GpuCapabilities.AdapterName} | Tier: {GpuCapabilities.CurrentTier} | VRAM: {GpuCapabilities.DedicatedVramMb:F0} MB | D3D11 Direct VTable P/Invoke"
            };
            topPanel.Controls.Add(lblGpuInfo);
            Controls.Add(topPanel);

            // Tab Control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };

            // Build Tabs
            BuildDirectXTab();
            BuildWaveformTab();
            BuildDirect2DTab();

            Controls.Add(_tabControl);
            _tabControl.BringToFront();

            // High-resolution Waveform Animation Timer (60 FPS)
            _waveformTimer = new System.Windows.Forms.Timer
            {
                Interval = 16
            };
            _waveformTimer.Tick += OnWaveformTick;
            _waveformTimer.Start();
        }

        private void BuildDirectXTab()
        {
            var tab = new TabPage("Direct3D 11 SDF Cards & Glow");
            tab.BackColor = Color.FromArgb(16, 18, 24);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                SplitterDistance = 750,
                BackColor = Color.FromArgb(26, 32, 44)
            };

            _directXCanvas = new ZeroDirectXCanvas
            {
                Dock = DockStyle.Fill,
                Elevation = 10f,
                BlurRadius = 20f,
                CornerRadius = 16f,
                BorderWidth = 1.5f,
                GlowIntensity = 0.5f,
                CardColor = Color.FromArgb(28, 34, 48)
            };
            split.Panel1.Controls.Add(_directXCanvas);

            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(12),
                AutoScroll = true
            };

            int y = 10;

            void AddSlider(string labelText, int min, int max, int value, Action<int> onChanged)
            {
                var lbl = new Label
                {
                    Text = $"{labelText}: {value}",
                    Location = new Point(12, y),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(200, 210, 225),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };
                y += 22;

                var track = new TrackBar
                {
                    Minimum = min,
                    Maximum = max,
                    Value = value,
                    TickFrequency = (max - min) / 10 > 0 ? (max - min) / 10 : 1,
                    Location = new Point(12, y),
                    Width = 240
                };
                track.ValueChanged += (s, e) =>
                {
                    lbl.Text = $"{labelText}: {track.Value}";
                    onChanged(track.Value);
                };
                y += 45;

                controlPanel.Controls.Add(lbl);
                controlPanel.Controls.Add(track);
            }

            AddSlider("Corner Radius", 0, 60, 16, v => { if (_directXCanvas != null) _directXCanvas.CornerRadius = v; });
            AddSlider("Elevation (Offset)", 0, 40, 10, v => { if (_directXCanvas != null) _directXCanvas.Elevation = v; });
            AddSlider("Shadow Blur Radius", 0, 60, 20, v => { if (_directXCanvas != null) _directXCanvas.BlurRadius = v; });
            AddSlider("Neon Glow Intensity", 0, 20, 5, v => { if (_directXCanvas != null) _directXCanvas.GlowIntensity = v / 10.0f; });

            var chkVsync = new CheckBox
            {
                Text = "Enable Hardware VSync",
                Location = new Point(12, y),
                AutoSize = true,
                Checked = false,
                ForeColor = Color.FromArgb(200, 210, 225)
            };
            chkVsync.CheckedChanged += (s, e) =>
            {
                if (_directXCanvas != null) _directXCanvas.Vsync = chkVsync.Checked;
            };
            controlPanel.Controls.Add(chkVsync);

            split.Panel2.Controls.Add(controlPanel);
            tab.Controls.Add(split);
            _tabControl.TabPages.Add(tab);
        }

        private void BuildWaveformTab()
        {
            var tab = new TabPage("Real-Time Waveform (100k Pts @ 60 FPS)");
            tab.BackColor = Color.FromArgb(16, 18, 24);

            _waveformCanvas = new ZeroWaveformCanvas
            {
                Dock = DockStyle.Fill,
                AutoScale = true,
                TraceColor = Color.FromArgb(0, 255, 136)
            };

            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(0, 255, 136),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "⚡ Streaming 100,000 live data points | D3D11 LineStrip Topology | Native GPU Geometry Pipeline"
            };
            bottomBar.Controls.Add(lblInfo);

            tab.Controls.Add(_waveformCanvas);
            tab.Controls.Add(bottomBar);
            _tabControl.TabPages.Add(tab);
        }

        private void BuildDirect2DTab()
        {
            var tab = new TabPage("Direct2D Subpixel ClearType");
            tab.BackColor = Color.FromArgb(16, 18, 24);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 80,
                BackColor = Color.FromArgb(26, 32, 44)
            };

            var topBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(16, 12, 16, 12)
            };

            var lbl = new Label
            {
                Text = "Interactive DirectWrite Sample Text:",
                Location = new Point(16, 12),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 210, 225)
            };

            var txtInput = new TextBox
            {
                Text = "ZeroGraphics DirectWrite: Subpixel ClearType & Per-Monitor V2 High-DPI Crispness (Zero GDI Scaling Artifacts)",
                Location = new Point(16, 36),
                Width = 750,
                Font = new Font("Segoe UI", 10f)
            };
            txtInput.TextChanged += (s, e) =>
            {
                if (_direct2DCanvas != null) _direct2DCanvas.SampleText = txtInput.Text;
            };

            topBar.Controls.Add(lbl);
            topBar.Controls.Add(txtInput);
            split.Panel1.Controls.Add(topBar);

            _direct2DCanvas = new ZeroDirect2DCanvas
            {
                Dock = DockStyle.Fill,
                TextFontFamily = "Segoe UI",
                TextSize = 18f,
                SampleText = txtInput.Text
            };
            split.Panel2.Controls.Add(_direct2DCanvas);

            tab.Controls.Add(split);
            _tabControl.TabPages.Add(tab);
        }

        private void OnWaveformTick(object? sender, EventArgs e)
        {
            if (_waveformCanvas == null || !_waveformCanvas.Visible)
                return;

            _wavePhase += 0.08f;
            float freq1 = 0.002f;
            float freq2 = 0.015f;

            for (int i = 0; i < _waveformBuffer.Length; i++)
            {
                float t = i + _wavePhase * 50f;
                _waveformBuffer[i] = (float)(Math.Sin(t * freq1) * 35.0 + Math.Sin(t * freq2) * 12.0);
            }

            _waveformCanvas.SetData(_waveformBuffer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _waveformTimer.Stop();
                _waveformTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

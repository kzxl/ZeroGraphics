# ZeroGraphics

High-performance, hardware-accelerated desktop graphics ecosystem for .NET (WinForms / WPF / Headless). Built with direct COM VTable P/Invoke — **zero external dependencies** (no SharpDX, no Vortice.Windows, no runtime D3DCompiler required).

Dual-targeted for legacy **.NET Framework 4.6.2** enterprise apps (e.g., Industrial ERP, MES, SCADA) and modern **.NET 8.0 / 9.0+ Windows**.

---

## 🏛️ Ecosystem Architecture

```
ZeroGraphics/
├── src/
│   ├── ZeroGraphics.Core/          # Multi-target: netstandard2.0; net462; net8.0
│   │   ├── Math/                   # Analytical SDF calculations, Gaussian kernels
│   │   ├── Data/                   # Zero-allocation LTTB downsampling algorithm
│   │   └── Telemetry/              # DXGI GPU hardware telemetry (Tiers, VRAM, Vendor)
│   │
│   ├── ZeroGraphics.Direct2D/      # Multi-target: net462; net8.0-windows
│   │   ├── Native/                 # Direct2D 1.0 & DirectWrite COM VTable interfaces
│   │   ├── Core/                   # D2DFactory, DWriteFactory, IDWriteTextFormat
│   │   └── Controls/               # ZeroDirect2DCanvas (Per-Monitor V2 Subpixel ClearType)
│   │
│   ├── ZeroGraphics.DirectX/       # Multi-target: net462; net8.0-windows
│   │   ├── Native/                 # Direct3D 11, DXGI 1.1, DXBC Bytecode VTable bindings
│   │   ├── Core/                   # D3D11DeviceManager, SwapChain, RenderTargetView
│   │   ├── Pipeline/               # SdfCardPipeline (Single-pass quad, precompiled HLSL)
│   │   └── Controls/               # ZeroDirectXCanvas (Analytical rounded cards, blur, glow)
│   │
│   └── ZeroGraphics.Waveform/      # Multi-target: net462; net8.0-windows
│       ├── Pipeline/               # WaveformPipeline (LineStrip topology, vertex buffer stream)
│       └── Controls/               # ZeroWaveformCanvas (100k points @ 60 FPS oscilloscope)
│
├── samples/
│   └── ZeroGraphics.Samples.Demo/  # Interactive WinForms demo application
└── tests/
    └── ZeroGraphics.Tests/         # Comprehensive xUnit automated verification suite
```

---

## 🚀 Key Advantages

1. **Zero External Dependencies**:
   - Uses zero third-party NuGet packages for graphics.
   - All COM interfaces (`ID3D11Device`, `IDXGISwapChain`, `ID2D1Factory`, `IDWriteFactory`) communicate directly via pure C# `[UnmanagedFunctionPointer]` VTable delegates.
   - Shaders are precompiled via `fxc.exe` (Shader Model 4.0) and embedded directly as byte arrays — eliminating the need for `d3dcompiler_47.dll` at runtime.

2. **Enterprise WinForms Integration (Single HWND)**:
   - Renders directly into standard WinForms `Control.Handle` via Direct3D 11 swap chains or Direct2D `HwndRenderTarget`.
   - On-demand rendering: redraws only when properties change or upon OS invalidation events (`0.0% CPU` and `0.0% GPU` when idle).

3. **Multi-Target Universal Compatibility**:
   - `ZeroGraphics.Core` targets `.NET Standard 2.0`, `.NET Framework 4.6.2`, and `.NET 8.0`.
   - UI canvas libraries support `.NET Framework 4.6.2` and `.NET 8.0-windows`.

---

## 📦 Package Matrix

| Package | Target Frameworks | Primary Use Case |
| :--- | :--- | :--- |
| **`ZeroGraphics.Core`** | `netstandard2.0`, `net462`, `net8.0` | Math, SDF kernels, LTTB decimation, GPU telemetry |
| **`ZeroGraphics.Direct2D`** | `net462`, `net8.0-windows` | Subpixel ClearType typography, High-DPI text rendering |
| **`ZeroGraphics.DirectX`** | `net462`, `net8.0-windows` | Analytical SDF cards, drop shadows, neon glow effects |
| **`ZeroGraphics.Waveform`** | `net462`, `net8.0-windows` | Real-time oscilloscope, high-speed time-series streaming |

---

## ⚡ Quick Start

### 1. Direct3D 11 Analytical SDF Card (WinForms)

```csharp
using ZeroGraphics.DirectX.Controls;

var card = new ZeroDirectXCanvas
{
    Dock = DockStyle.Fill,
    Elevation = 12f,
    BlurRadius = 24f,
    CornerRadius = 16f,
    BorderWidth = 1.5f,
    GlowIntensity = 0.6f,
    CardColor = Color.FromArgb(24, 28, 38)
};
this.Controls.Add(card);
```

### 2. Direct2D DirectWrite Typography

```csharp
using ZeroGraphics.Direct2D.Controls;

var textCanvas = new ZeroDirect2DCanvas
{
    Dock = DockStyle.Fill,
    TextFontFamily = "Segoe UI",
    TextSize = 16f,
    SampleText = "Subpixel ClearType with Per-Monitor V2 High-DPI Crispness"
};
this.Controls.Add(textCanvas);
```

### 3. Waveform High-Speed Oscilloscope (100k points)

```csharp
using ZeroGraphics.Waveform.Controls;

var scope = new ZeroWaveformCanvas
{
    Dock = DockStyle.Fill,
    AutoScale = true,
    TraceColor = Color.FromArgb(0, 255, 136)
};
this.Controls.Add(scope);

// Stream live telemetry points
float[] points = GetTelemetryBuffer(); // 100,000 points
scope.SetData(points);
```

---

## 🧪 Verification & Tests

To execute the automated test suite:

```bash
dotnet test tests/ZeroGraphics.Tests/ZeroGraphics.Tests.csproj
```

To run the interactive demonstration application:

```bash
dotnet run --project samples/ZeroGraphics.Samples.Demo/ZeroGraphics.Samples.Demo.csproj -f net8.0-windows
```

---

## 📄 License

MIT License. Designed and engineered by Phong Võ.

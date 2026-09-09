# ZeroGraphics ⚡

> **Ultra-High-Performance, Zero-External-Dependency GPU Acceleration Engine for .NET (WinForms, WPF & Headless)**

[![ZeroPlatform Ecosystem](https://img.shields.io/badge/ZeroPlatform-Ecosystem-blueviolet.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet - ZeroGraphics.Core](https://img.shields.io/badge/nuget-ZeroGraphics.Core%20v1.0.1-blue.svg)](https://www.nuget.org/packages/ZeroGraphics.Core/1.0.1)
[![Unit Tests](https://img.shields.io/badge/tests-124%20passed%20(100%25)-brightgreen.svg)](#-automated-testing--verification)
[![Target Frameworks](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20net462%20%7C%20net8.0--windows-blue.svg)](#-package-matrix)
[![Input Latency](https://img.shields.io/badge/Input%20Latency-%3C%201%20Frame%20(~4ms)-brightgreen.svg)](docs/BENCHMARKS.md)
[![Stream Capacity](https://img.shields.io/badge/Streaming-10M%2B%20Points%20%40%20144Hz-purple.svg)](docs/BENCHMARKS.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#-license)

---

## 📖 Executive Summary

**ZeroGraphics** is a sovereign graphics and industrial computer vision suite engineered in 100% pure C#. It provides hardware-accelerated rendering, oscilloscope waveform streaming, and automated machine vision without relying on heavyweight third-party wrappers like SharpDX, Silk.NET, or OpenCV.

### Core Architectural Pillars
- **Pure COM VTable Interop**: Direct3D 11, DXGI, Direct2D, and DirectWrite invoked directly via pre-indexed COM VTable pointers in pure C# (0 external dependencies).
- **Modern Flip Model (`DXGI_SWAP_EFFECT_FLIP_DISCARD`)**: Direct hardware flip to HWND eliminating DWM redirection copy stalls (0% CPU at idle).
- **Sub-4ms Input Latency**: Hardware-enforced `SetMaximumFrameLatency(1)` reducing input lag by ~67%.
- **Massive Waveform Streaming**: 10,000,000+ points rendered at 144+ FPS via dynamic `D3D11_MAP_WRITE_DISCARD` buffer renaming.
- **Self-Healing Device Recovery**: Transparent recovery from GPU driver crashes and resets (`DXGI_ERROR_DEVICE_REMOVED`, `D2DERR_RECREATE_TARGET`).
- **GPU Render Graph & Operation Fusion**: 13 precompiled HLSL kernels with automatic multi-pass fusion reducing VRAM bandwidth by up to 75%.

---

## 📚 Technical Documentation & Guides

Comprehensive technical details and guides are modularized within the [`docs/`](docs/) directory:

| Document | Description |
| :--- | :--- |
| 📊 **[Verified Benchmarks](docs/BENCHMARKS.md)** | FPS, CPU load, input latency, and decimation throughput vs GDI+ and SkiaSharp. |
| 🏛️ **[Core Architecture](docs/ARCHITECTURE.md)** | COM VTable interop, Flip presentation, device loss recovery, and Render Graph. |
| 👁️ **[Vision & Metrology](docs/VISION_AND_METROLOGY.md)** | Sub-pixel Caliper, NCC template matching, RANSAC fitting, Barcodes & Reed-Solomon. |
| 🍳 **[Code Recipes Cookbook](docs/RECIPES.md)** | 14 ready-to-use code recipes from WinForms controls to headless WebAPI rendering. |
| 🔮 **[Future Proposals](docs/ECOSYSTEM_EXPANSION_PROPOSALS.md)** | Compute Shaders, Vulkan/Metal research, and 3D surface scanning roadmap. |

---

## 📦 Package Matrix

| Package | Targets | Primary Capabilities |
| :--- | :--- | :--- |
| **`ZeroGraphics.Core`** | `netstandard2.0`, `net462`, `net8.0` | Peak-preserving decimation (MinMax, LTTB), FFT, 2D QuadTree/Grid, SPC analytics |
| **`ZeroGraphics.DirectX`** | `net462`, `net8.0-windows` | D3D11 device management, Flip Model SwapChain, latency tuning, SDF cards, SRV/RTV |
| **`ZeroGraphics.Direct2D`** | `net462`, `net8.0-windows` | Headless `D2DOffscreenTarget`, DirectWrite ClearType typography, High-DPI `SetDpi` |
| **`ZeroGraphics.Waveform`** | `net462`, `net8.0-windows` | LineStrip waveform pipeline, dynamic buffer map streaming, 144Hz oscilloscope |
| **`ZeroGraphics.Imaging`** | `net462`, `net8.0-windows` | GPU Image Pipeline, Render Graph, Operation Fusion, `GpuTexturePool`, CIEDE2000 |
| **`ZeroGraphics.Vision`** | `net462`, `net8.0-windows` | Sub-pixel NCC, 1D caliper, TLS/Taubin/Fitzgibbon fit, RANSAC, Barcodes & Reed-Solomon |

---

## ⚡ Quick Start: 144Hz Oscilloscope (WinForms)

```csharp
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Waveform.Controls;
using ZeroGraphics.Waveform.Pipeline;

var oscilloscope = new ZeroWaveformCanvas
{
    Dock = DockStyle.Fill,
    BackColor = Color.FromArgb(13, 17, 23),
    TraceColor = Color.FromArgb(16, 185, 129), // Emerald phosphor
    DecimationMode = WaveformDecimationMode.MinMax,
    AutoScale = true
};
this.Controls.Add(oscilloscope);

// Push multi-million point array directly to GPU
float[] sensorData = GetSensorReadings(); // 1,000,000 points
oscilloscope.SetData(sensorData);
```

👉 **[Browse All 14 Code Recipes in the Developer Cookbook](docs/RECIPES.md)**

---

## 🧪 Automated Testing & Verification

```bash
# Run all automated tests (124 tests, 100% pass)
dotnet test tests/ZeroGraphics.Tests/ZeroGraphics.Tests.csproj

# Launch interactive 144Hz GPU demonstration application
dotnet run --project samples/ZeroGraphics.Samples.Demo/ZeroGraphics.Samples.Demo.csproj -f net8.0-windows
```

---

## 🌐 Part of the ZeroPlatform Ecosystem

ZeroGraphics is the hardware graphics and machine vision pillar of the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** suite — unifying 12 sovereign subsystems including `ZeroUI`, `ZeroPipeline`, `ZeroTensor`, `ZeroCompute`, `ZeroComm`, and `ZeroStorage`.

---

## 📄 License & Author

Released under the permissive **MIT License**.  
Architected and developed by **Phong Võ**.

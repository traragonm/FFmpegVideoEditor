# FFmpeg Video Editor

A powerful, modern WPF-based video editor powered by **FFmpeg**. This application provides a rich user interface for previewing, trimming, and processing videos using FFmpeg commands.

## ✨ Features

- 🎬 **Video Preview**: Integrated media player with frame-accurate scrubbing.
- 📊 **Interactive Timeline**: 
  - Adaptive ruler with time markers.
  - Multi-track visualization (clip bar).
  - Draggable playhead for quick seeking.
  - Dual-handle trim system (In/Out points).
- ✂️ **Fast Trimming**: Export trimmed segments instantly using FFmpeg's stream copying (no re-encoding if supported).
- ⌨ **Embedded FFmpeg Terminal**:
  - Run custom FFmpeg commands directly within the app.
  - Automatic working directory management (sets to the video's location).
  - Real-time output streaming (stdout/stderr).
  - Command history (↑/↓) and copy/clear functions.
- 🎨 **Dynamic Themes**: Choose between **Dark**, **Light**, and **Midnight Green** themes.
- 📋 **Media Metadata**: Detailed view of resolution, FPS, codec, and duration.

## 🚀 Getting Started

### Prerequisites

- **.NET 10 SDK** (windows-windows target).
- **FFmpeg Binaries**: The app requires `ffmpeg.exe` to be available.
  - You can download them from [ffmpeg.org](https://ffmpeg.org/download.html).
  - Place `ffmpeg.exe` in the application directory, or add it to your system's `PATH`.

### Installation

1. Clone or download this project.
2. Open a terminal in the project directory:
   ```powershell
   cd "FFmpegVideoEditor"
   dotnet run
   ```

## 🛠 Project Structure

- `FFmpegVideoEditor/`
  - `Controls/`: Custom UI components like `TimelineControl`.
  - `Services/`: `FFmpegService` for wrapping FFmpeg logic via `Xabe.FFmpeg`.
  - `Themes/`: XAML resource dictionaries for the design system.
  - `Converters/`: Data binding helpers (e.g., Visibility toggles).

## 📄 License

This project is open-source. Feel free to use and modify it.

---
*Built with ❤️ using WPF and FFmpeg.*
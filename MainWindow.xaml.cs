using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FFmpegVideoEditor.Services;
using Microsoft.Win32;

namespace FFmpegVideoEditor
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // ── INotifyPropertyChanged ────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? nm = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nm));

        private bool _isVideoLoaded;
        public bool IsVideoLoaded
        {
            get => _isVideoLoaded;
            set { _isVideoLoaded = value; OnPropertyChanged(); }
        }

        // ── State ─────────────────────────────────────────────────────
        private string _currentFilePath = string.Empty;
        private string _videoDirectory  = string.Empty;
        private TimeSpan _duration = TimeSpan.Zero;
        private readonly DispatcherTimer _timer;
        private CancellationTokenSource? _exportCts;

        // ── Terminal state ────────────────────────────────────────────
        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;

        // ── Ctor ──────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += Timer_Tick;

            AppendTerminal("FFmpeg Terminal ready.\r\nOpen a video file to set the working directory.\r\n");
            AppendTerminal("Usage: type ffmpeg arguments here (without 'ffmpeg'), then press Enter.\r\n");
            AppendTerminal("Example:  -i \"input.mp4\" -vf scale=1280:720 \"output.mp4\"\r\n");
            AppendTerminal("─────────────────────────────────────────────────────\r\n");
        }

        // ─────────────────────────────────────────────────────────────
        // OPEN VIDEO
        // ─────────────────────────────────────────────────────────────
        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Open Video File",
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mpg;*.mpeg|All Files|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            _currentFilePath = dlg.FileName;
            _videoDirectory  = Path.GetDirectoryName(_currentFilePath) ?? string.Empty;
            SetStatus($"Loading: {Path.GetFileName(_currentFilePath)} …");

            // Update terminal directory label
            lblTerminalDir.Text = _videoDirectory;
            AppendTerminal($"\r\n[Working directory set to: {_videoDirectory}]\r\n");

            try
            {
                var info = await FFmpegService.GetMediaInfoAsync(_currentFilePath);
                _duration = info.Duration;

                lblFileName.Text   = Path.GetFileName(_currentFilePath);
                lblFileName.ToolTip = _currentFilePath;
                lblDuration.Text   = FormatTime(_duration);
                lblTrimStart.Text  = FormatTime(TimeSpan.Zero);
                lblTrimEnd.Text    = FormatTime(_duration);

                var stream = System.Linq.Enumerable.FirstOrDefault(info.VideoStreams);
                if (stream != null)
                {
                    lblResolution.Text = $"{stream.Width}×{stream.Height}";
                    lblFps.Text        = $"{stream.Framerate:F2}";
                    lblCodec.Text      = stream.Codec;
                }

                mediaPlayer.Source  = new Uri(_currentFilePath);
                mediaPlayer.Volume  = sliderVolume.Value;
                placeholderPanel.Visibility = Visibility.Collapsed;

                timeline.Duration = _duration;
                IsVideoLoaded = true;

                SetStatus($"Opened: {Path.GetFileName(_currentFilePath)} — {FormatTime(_duration)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open video:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Error loading video.");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // PLAYBACK
        // ─────────────────────────────────────────────────────────────
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Play();
            _timer.Start();
            SetStatus("Playing…");
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            _timer.Stop();
            SetStatus("Paused.");
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            _timer.Stop();
            timeline.PlayheadTime = TimeSpan.Zero;
            lblCurrentTime.Text = FormatTimeFull(TimeSpan.Zero);
            SetStatus("Stopped.");
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaPlayer != null) mediaPlayer.Volume = e.NewValue;
        }

        // ─────────────────────────────────────────────────────────────
        // MEDIA EVENTS
        // ─────────────────────────────────────────────────────────────
        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
                _duration = mediaPlayer.NaturalDuration.TimeSpan;
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            SetStatus("Playback finished.");
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
            => SetStatus($"Playback error: {e.ErrorException.Message}");

        // ─────────────────────────────────────────────────────────────
        // TIMER
        // ─────────────────────────────────────────────────────────────
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (mediaPlayer.Position is TimeSpan pos)
            {
                timeline.PlayheadTime = pos;
                lblCurrentTime.Text   = FormatTimeFull(pos);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // TIMELINE EVENTS
        // ─────────────────────────────────────────────────────────────
        private void Timeline_PlayheadMoved(object? sender, TimeSpan time)
        {
            mediaPlayer.Position = time;
            lblCurrentTime.Text  = FormatTimeFull(time);
        }

        private void Timeline_TrimChanged(object? sender, (TimeSpan Start, TimeSpan End) trim)
        {
            lblTrimStart.Text = FormatTime(trim.Start);
            lblTrimEnd.Text   = FormatTime(trim.End);
        }

        private void ComboTheme_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (comboTheme.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string themePath)
            {
                try
                {
                    var uri = new Uri(themePath, UriKind.Relative);
                    var dict = new ResourceDictionary { Source = uri };

                    // Find and remove existing theme dictionary (they all contain BackgroundBrush etc)
                    // Or just clear and add if we don't have other global dictionaries
                    Application.Current.Resources.MergedDictionaries.Clear();
                    Application.Current.Resources.MergedDictionaries.Add(dict);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error changing theme: {ex.Message}");
                }
            }
        }

        private void TabBottom_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }

        private void BtnSetTrim_Click(object sender, RoutedEventArgs e)
        {
            lblTrimStart.Text = FormatTime(timeline.TrimStart);
            lblTrimEnd.Text   = FormatTime(timeline.TrimEnd);
            SetStatus($"Trim set: {FormatTime(timeline.TrimStart)} → {FormatTime(timeline.TrimEnd)}");
        }

        // ─────────────────────────────────────────────────────────────
        // EXPORT
        // ─────────────────────────────────────────────────────────────
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var saveDlg = new SaveFileDialog
            {
                Title    = "Export Trimmed Video",
                Filter   = "MP4 Video|*.mp4|MKV Video|*.mkv|All Files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_currentFilePath) + "_trimmed",
                InitialDirectory = _videoDirectory
            };
            if (saveDlg.ShowDialog() != true) return;

            string outputPath = saveDlg.FileName;
            _exportCts = new CancellationTokenSource();

            btnExport.IsEnabled  = false;
            progressExport.Value = 0;
            lblExportStatus.Text = "Exporting…";
            SetStatus("Exporting video…");

            var progress = new Progress<double>(p =>
            {
                progressExport.Value = p;
                lblExportStatus.Text = $"Exporting… {p:F0}%";
            });

            try
            {
                await FFmpegService.TrimVideoAsync(
                    _currentFilePath, outputPath,
                    timeline.TrimStart, timeline.TrimEnd,
                    progress, _exportCts.Token);

                progressExport.Value = 100;
                lblExportStatus.Text = "Export complete!";
                SetStatus($"Exported to: {outputPath}");
                MessageBox.Show($"Export complete!\n{outputPath}", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                lblExportStatus.Text = "Export cancelled.";
                SetStatus("Export cancelled.");
            }
            catch (Exception ex)
            {
                lblExportStatus.Text = "Export failed.";
                SetStatus($"Export error: {ex.Message}");
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnExport.IsEnabled = true;
                _exportCts?.Dispose();
                _exportCts = null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // FFMPEG TERMINAL
        // ─────────────────────────────────────────────────────────────
        private void TxtTerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RunTerminalCommand();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                NavigateHistory(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                NavigateHistory(+1);
                e.Handled = true;
            }
        }

        private void BtnTerminalRun_Click(object sender, RoutedEventArgs e) => RunTerminalCommand();

        private void BtnTerminalClear_Click(object sender, RoutedEventArgs e)
        {
            txtTerminalOutput.Text = string.Empty;
        }

        private void BtnTerminalCopy_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(txtTerminalOutput.Text); }
            catch { /* ignore */ }
        }

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;
            _historyIndex = Math.Clamp(_historyIndex + direction, 0, _commandHistory.Count - 1);
            txtTerminalInput.Text = _commandHistory[_historyIndex];
            txtTerminalInput.CaretIndex = txtTerminalInput.Text.Length;
        }

        private async void RunTerminalCommand()
        {
            string args = txtTerminalInput.Text.Trim();
            if (string.IsNullOrEmpty(args)) return;

            // Save history
            _commandHistory.Add(args);
            _historyIndex = _commandHistory.Count;
            txtTerminalInput.Text = string.Empty;

            // Working dir = video folder (or app folder as fallback)
            string workDir = string.IsNullOrEmpty(_videoDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : _videoDirectory;

            AppendTerminal($"\r\n$ ffmpeg {args}\r\n");

            var psi = new ProcessStartInfo
            {
                FileName               = "ffmpeg",
                Arguments              = args,
                WorkingDirectory       = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8,
            };

            try
            {
                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, ev) =>
                {
                    if (ev.Data != null)
                        Dispatcher.InvokeAsync(() => AppendTerminal(ev.Data + "\r\n"));
                };
                proc.ErrorDataReceived += (s, ev) =>
                {
                    // FFmpeg sends most output (including progress) to stderr
                    if (ev.Data != null)
                        Dispatcher.InvokeAsync(() => AppendTerminal(ev.Data + "\r\n"));
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await System.Threading.Tasks.Task.Run(() => proc.WaitForExit());

                Dispatcher.Invoke(() =>
                {
                    AppendTerminal($"\r\n[Process exited with code {proc.ExitCode}]\r\n");
                    AppendTerminal("─────────────────────────────────────────────────────\r\n");
                    SetStatus($"ffmpeg finished (exit code {proc.ExitCode})");
                });
            }
            catch (Exception ex)
            {
                AppendTerminal($"[ERROR] {ex.Message}\r\n");
                AppendTerminal("Make sure ffmpeg is available in PATH or in the app directory.\r\n");
                AppendTerminal("─────────────────────────────────────────────────────\r\n");
            }
        }

        private void AppendTerminal(string text)
        {
            txtTerminalOutput.AppendText(text);
            terminalScroll.ScrollToBottom();
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────
        private void SetStatus(string msg) => lblStatus.Text = msg;
        private static string FormatTime(TimeSpan t)     => t.ToString(@"hh\:mm\:ss");
        private static string FormatTimeFull(TimeSpan t) => t.ToString(@"hh\:mm\:ss\.fff");
    }
}
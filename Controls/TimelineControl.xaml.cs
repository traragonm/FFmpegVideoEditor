using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FFmpegVideoEditor.Controls
{
    public partial class TimelineControl : UserControl
    {
        // ── Events ──────────────────────────────────────────────────
        public event EventHandler<TimeSpan>? PlayheadMoved;
        public event EventHandler<(TimeSpan Start, TimeSpan End)>? TrimChanged;

        // ── State ────────────────────────────────────────────────────
        private TimeSpan _duration = TimeSpan.Zero;
        private TimeSpan _playheadTime = TimeSpan.Zero;
        private TimeSpan _trimStart = TimeSpan.Zero;
        private TimeSpan _trimEnd = TimeSpan.Zero;

        private bool _isDraggingPlayhead;
        private bool _isDraggingTrimLeft;
        private bool _isDraggingTrimRight;

        // ── Public Properties ────────────────────────────────────────
        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                _duration = value;
                _trimEnd = value;
                _playheadTime = TimeSpan.Zero;
                Redraw();
            }
        }

        public TimeSpan PlayheadTime
        {
            get => _playheadTime;
            set
            {
                _playheadTime = Clamp(value, TimeSpan.Zero, _duration);
                UpdatePlayheadPosition();
            }
        }

        public TimeSpan TrimStart => _trimStart;
        public TimeSpan TrimEnd   => _trimEnd;

        // ── Ctor ─────────────────────────────────────────────────────
        public TimelineControl()
        {
            InitializeComponent();
            SizeChanged += (_, _) => Redraw();
        }

        // ── Layout ───────────────────────────────────────────────────
        private void Redraw()
        {
            if (_duration == TimeSpan.Zero || ActualWidth == 0) return;

            DrawRuler();
            UpdateClipBar();
            UpdatePlayheadPosition();
            UpdateTrimHandles();
        }

        private void DrawRuler()
        {
            canvasRuler.Children.Clear();
            double w = ActualWidth;
            double totalSec = _duration.TotalSeconds;
            if (totalSec == 0) return;

            // Choose tick interval (adaptive)
            double[] intervals = { 1, 2, 5, 10, 15, 30, 60, 120, 300 };
            double targetTicks = w / 80.0;
            double interval = intervals[0];
            foreach (var inv in intervals)
            {
                interval = inv;
                if (totalSec / inv <= targetTicks) break;
            }

            var pen = new Pen(new SolidColorBrush(Color.FromArgb(180, 150, 170, 200)), 1);
            var textBrush = new SolidColorBrush(Color.FromArgb(200, 154, 171, 184));
            var font = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            for (double t = 0; t <= totalSec; t += interval)
            {
                double x = t / totalSec * w;
                // Tick line
                var line = new Line
                {
                    X1 = x, Y1 = 14, X2 = x, Y2 = 24,
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 150, 170, 200)),
                    StrokeThickness = 1
                };
                canvasRuler.Children.Add(line);
                Canvas.SetLeft(line, 0);

                // Label
                var ts = TimeSpan.FromSeconds(t);
                string label = ts.TotalHours >= 1
                    ? ts.ToString(@"h\:mm\:ss")
                    : ts.ToString(@"m\:ss");

                var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, font, 10, textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                var tb = new TextBlock
                {
                    Text = label,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    Foreground = textBrush
                };
                canvasRuler.Children.Add(tb);
                Canvas.SetLeft(tb, x + 2);
                Canvas.SetTop(tb, 2);
            }
        }

        private void UpdateClipBar()
        {
            if (_duration == TimeSpan.Zero) return;
            double w = ActualWidth;
            rectClip.Width = Math.Max(0, w);
            Canvas.SetLeft(rectClip, 0);
            Canvas.SetLeft(lblClipName, 8);
        }

        private void UpdatePlayheadPosition()
        {
            if (_duration == TimeSpan.Zero) return;
            double x = TimeToX(_playheadTime);
            Canvas.SetLeft(canvasPlayhead, x - 6);
        }

        private void UpdateTrimHandles()
        {
            if (_duration == TimeSpan.Zero) return;
            double xStart = TimeToX(_trimStart);
            double xEnd   = TimeToX(_trimEnd);

            // Trim region
            Canvas.SetLeft(rectTrimRegion, xStart);
            rectTrimRegion.Width = Math.Max(0, xEnd - xStart);

            // Handles
            Canvas.SetLeft(rectTrimLeft,  xStart);
            Canvas.SetLeft(rectTrimRight, xEnd - 4);
        }

        // ── Mouse: Playhead drag ──────────────────────────────────────
        private void CanvasTrack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if near trim handles (handled separately)
            _isDraggingPlayhead = true;
            canvasTrack.CaptureMouse();
            MovePlayheadTo(e.GetPosition(canvasTrack).X);
        }

        private void CanvasTrack_MouseMove(object sender, MouseEventArgs e)
        {
            double x = e.GetPosition(canvasTrack).X;

            if (_isDraggingPlayhead)
                MovePlayheadTo(x);
            else if (_isDraggingTrimLeft)
                MoveTrimStart(x);
            else if (_isDraggingTrimRight)
                MoveTrimEnd(x);
        }

        private void CanvasTrack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPlayhead = false;
            _isDraggingTrimLeft = false;
            _isDraggingTrimRight = false;
            canvasTrack.ReleaseMouseCapture();
        }

        // ── Mouse: Trim handle drag ───────────────────────────────────
        private void TrimHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var rect = (Rectangle)sender;
            if ((string)rect.Tag == "Left")
            {
                _isDraggingTrimLeft = true;
                _isDraggingPlayhead = false;
            }
            else
            {
                _isDraggingTrimRight = true;
                _isDraggingPlayhead = false;
            }
            canvasTrack.CaptureMouse();
        }

        // ── Helpers ───────────────────────────────────────────────────
        private void MovePlayheadTo(double x)
        {
            if (_duration == TimeSpan.Zero) return;
            _playheadTime = Clamp(XToTime(x), TimeSpan.Zero, _duration);
            UpdatePlayheadPosition();
            PlayheadMoved?.Invoke(this, _playheadTime);
        }

        private void MoveTrimStart(double x)
        {
            if (_duration == TimeSpan.Zero) return;
            var t = Clamp(XToTime(x), TimeSpan.Zero, _trimEnd - TimeSpan.FromMilliseconds(100));
            _trimStart = t;
            UpdateTrimHandles();
            TrimChanged?.Invoke(this, (_trimStart, _trimEnd));
        }

        private void MoveTrimEnd(double x)
        {
            if (_duration == TimeSpan.Zero) return;
            var t = Clamp(XToTime(x), _trimStart + TimeSpan.FromMilliseconds(100), _duration);
            _trimEnd = t;
            UpdateTrimHandles();
            TrimChanged?.Invoke(this, (_trimStart, _trimEnd));
        }

        private double TimeToX(TimeSpan t) =>
            _duration == TimeSpan.Zero ? 0 : t.TotalSeconds / _duration.TotalSeconds * ActualWidth;

        private TimeSpan XToTime(double x) =>
            TimeSpan.FromSeconds(x / ActualWidth * _duration.TotalSeconds);

        private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) =>
            value < min ? min : value > max ? max : value;
    }
}

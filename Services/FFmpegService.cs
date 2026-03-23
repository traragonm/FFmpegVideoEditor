using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace FFmpegVideoEditor.Services
{
    public class FFmpegService
    {
        static FFmpegService()
        {
            // Try to auto-detect ffmpeg from PATH or common locations
            var paths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg"),
                @"C:\ffmpeg\bin",
                @"C:\Program Files\ffmpeg\bin",
            };

            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    FFmpeg.SetExecutablesPath(p);
                    return;
                }
            }
            // Fallback: let Xabe find from PATH
        }

        /// <summary>Returns metadata of a video file.</summary>
        public static async Task<IMediaInfo> GetMediaInfoAsync(string filePath)
        {
            return await FFmpeg.GetMediaInfo(filePath);
        }

        /// <summary>
        /// Trim a video from startTime to endTime and save to outputPath.
        /// Reports progress 0-100 via the progress callback.
        /// </summary>
        public static async Task TrimVideoAsync(
            string inputPath,
            string outputPath,
            TimeSpan startTime,
            TimeSpan endTime,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var info = await FFmpeg.GetMediaInfo(inputPath);
            var duration = endTime - startTime;

            var conversion = FFmpeg.Conversions.New()
                .AddParameter($"-ss {startTime:hh\\:mm\\:ss\\.fff}")
                .AddParameter($"-i \"{inputPath}\"")
                .AddParameter($"-t {duration:hh\\:mm\\:ss\\.fff}")
                .AddParameter("-c copy")
                .SetOutput(outputPath)
                .SetOverwriteOutput(true);

            if (progress != null)
            {
                conversion.OnProgress += (sender, args) =>
                {
                    // args.Percent is 0-100
                    progress.Report(args.Percent);
                };
            }

            await conversion.Start(cancellationToken);
        }

        /// <summary>
        /// Extract a single frame as a JPEG thumbnail.
        /// </summary>
        public static async Task ExtractThumbnailAsync(string inputPath, string outputPath, TimeSpan position)
        {
            var conversion = FFmpeg.Conversions.New()
                .AddParameter($"-ss {position:hh\\:mm\\:ss\\.fff}")
                .AddParameter($"-i \"{inputPath}\"")
                .AddParameter("-frames:v 1")
                .SetOutput(outputPath)
                .SetOverwriteOutput(true);

            await conversion.Start();
        }
    }
}

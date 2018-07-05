using NLog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Impl
{
    public class YtdlOperation
    {
        private readonly Logger _log;

        public YtdlOperation()
        {
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<string> GetDataAsync(string url, bool tryForBestAudio)
        {
            var arguments = $"--geo-bypass";
            if (tryForBestAudio)
            {
                arguments += $" -f bestaudio";
            }
            arguments += $" -e --get-url --get-id --get-thumbnail --get-duration --no-check-certificate --default-search \"ytsearch:\" \"{url}\"";

            using (Process process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "youtube-dl",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            })
            {
                process.Start();
                var str = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                var err = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(err))
                    _log.Warn(err);
                return str;
            }
        }
    }
}

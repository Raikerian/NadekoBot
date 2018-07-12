using NadekoBot.Core.Modules.Music.Common;
using NLog;
using System;
using System.Diagnostics;
using System.IO;

namespace NadekoBot.Modules.Music.Common
{
    public sealed class SongBuffer : IDisposable
    {
        private Process p;
        private readonly PoopyBufferReborn _buffer;
        private Stream _outStream;
        private bool _ffmpegProcessFinished;

        private readonly Logger _log;

        public string SongUri { get; private set; }

        public SongBuffer(string songUri, bool isLocal, bool isHls)
        {
            _log = LogManager.GetCurrentClassLogger();
            this.SongUri = songUri;
            this._isLocal = isLocal;
            this._isHls = isHls;

            try
            {
                this.p = StartFFmpegProcess(SongUri);
                this._outStream = this.p.StandardOutput.BaseStream;
                this._buffer = new PoopyBufferReborn(this._outStream);
                this.p.EnableRaisingEvents = true;
                this.p.Exited += new EventHandler(FfmpegProcessExited);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                _log.Error(@"You have not properly installed or configured FFMPEG. 
Please install and configure FFMPEG to play music. 
Check the guides for your platform on how to setup ffmpeg correctly:
    Windows Guide: https://goo.gl/OjKk8F
    Linux Guide:  https://goo.gl/ShjCUo");
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { } // when ffmpeg is disposed
            catch (Exception ex)
            {
                _log.Info(ex);
            }
        }

        private Process StartFFmpegProcess(string songUri)
        {
            if (_isHls)
                songUri = "hls+" + songUri;

            var args = $"-err_detect ignore_err -i \"{songUri}\" -f s16le -ar 48000 -vn -ac 2 pipe:1 -loglevel error";
            if (!_isLocal && !_isHls)
                args = "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 " + args;

            _ffmpegProcessFinished = false;
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true,
            });
        }

        internal void FfmpegProcessExited(object sender, System.EventArgs e)
        {
            _log.Info("ffmpeg process finished");
            _ffmpegProcessFinished = true;
        }

        private readonly bool _isLocal;
        private readonly bool _isHls;

        public byte[] Read(int toRead)
        {
            return this._buffer.Read(toRead).ToArray();
        }

        public void Dispose()
        {
            try
            {
                this.p.StandardOutput.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            try
            {
                if (!this.p.HasExited)
                    this.p.Kill();
            }
            catch
            {
            }
            _buffer.Stop();
            _outStream.Dispose();
            this.p.Dispose();
        }

        public void StartBuffering()
        {
            this._buffer.StartBuffering();
        }

        public bool EmptyBuffer()
        {
            // by determining empty buffer this way, we will no longer drop streams
            // due to possible slow internet issue, as ffmpeg process always runs trying to reconnect
            // for more bytes
            return _ffmpegProcessFinished && this._buffer.EmptyBuffer();
        }
    }
}
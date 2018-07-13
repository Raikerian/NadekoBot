using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Services.Impl;
using NLog;
using YoutubeExplode;

namespace NadekoBot.Modules.Music.Common.SongResolver.Strategies
{
    public class YoutubeResolveStrategy : IResolveStrategy
    {
        private readonly Logger _log;

        public YoutubeResolveStrategy()
        {
            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task<SongInfo> ResolveSong(string query)
        {
            try
            {
                SongInfo s = await ResolveWithYtExplode(query).ConfigureAwait(false);
                if (s != null)
                    return s;
            }
            catch { }
            return await ResolveWithYtDl(query).ConfigureAwait(false);
        }

        private async Task<SongInfo> ResolveWithYtExplode(string query)
        {
            YoutubeExplode.Models.Video video;
            var client = new YoutubeClient();
            if (!YoutubeClient.TryParseVideoId(query, out var id))
            {
                _log.Info("Searching for video");
                var videos = await client.SearchVideosAsync(query, 1).ConfigureAwait(false);

                video = videos.FirstOrDefault();
            }
            else
            {
                _log.Info("Getting video with id");
                video = await client.GetVideoAsync(id).ConfigureAwait(false);
            }

            if (video == null)
                return null;

            _log.Info("Video found");
            var streamInfo = await client.GetVideoMediaStreamInfosAsync(video.Id).ConfigureAwait(false);

            bool isLivestream = streamInfo.HlsLiveStreamUrl != null;
            string streamUrl = null;
            if (isLivestream)
            {
                streamUrl = streamInfo.HlsLiveStreamUrl;
            }
            else
            {
                var stream = streamInfo.Audio
                .OrderByDescending(x => x.Bitrate)
                .FirstOrDefault();
                streamUrl = stream.Url;
            }

            _log.Info("Got stream url");

            if (streamUrl == null)
                return null;

            return new SongInfo
            {
                Provider = "YouTube",
                ProviderType = MusicType.YouTube,
                Query = "https://youtube.com/watch?v=" + video.Id,
                Thumbnail = video.Thumbnails.MediumResUrl,
                TotalTime = TimeSpan.Compare(video.Duration, TimeSpan.Zero) == 0 ? TimeSpan.MaxValue : video.Duration,
                Uri = async () =>
                {
                    await Task.Yield();
                    return streamUrl;
                },
                VideoId = video.Id,
                Title = video.Title,
            };
        }

        private async Task<SongInfo> ResolveWithYtDl(string query)
        {
            string[] data;
            try
            {
				bool tryForBestAudio = true;
                var ytdl = new YtdlOperation();
                data = (await ytdl.GetDataAsync(query, tryForBestAudio).ConfigureAwait(false)).Split('\n');
				if (data.Length < 6)
				{
					// try without best audio flag as it might be youtube stream
					// which does not have bestaudio stream
					_log.Info("Trying to request stream without bestaudio flag.");
					tryForBestAudio = false;
					data = (await ytdl.GetDataAsync(query, tryForBestAudio).ConfigureAwait(false)).Split('\n');
				}

                if (data.Length < 6)
                {
                    _log.Info("No song found. Data less than 6");
                    return null;
                }

                if (!TimeSpan.TryParseExact(data[4], new[] { "ss", "m\\:ss", "mm\\:ss", "h\\:mm\\:ss", "hh\\:mm\\:ss", "hhh\\:mm\\:ss" }, CultureInfo.InvariantCulture, out var time))
                {
                    time = TimeSpan.MaxValue;
                }

                return new SongInfo()
                {
                    Title = data[0],
                    VideoId = data[1],
                    Uri = async () =>
                    {
                        var ytdlo = new YtdlOperation();
                        data = (await ytdlo.GetDataAsync(query, tryForBestAudio).ConfigureAwait(false)).Split('\n');
                        if (data.Length < 6)
                        {
                            _log.Info("No song found. Data less than 6");
                            return null;
                        }
                        return data[2];
                    },
                    Thumbnail = data[3],
                    TotalTime = time,
                    Provider = "YouTube",
                    ProviderType = MusicType.YouTube,
                    Query = "https://youtube.com/watch?v=" + data[1],
                };
            }
            catch (Exception ex)
            {
                _log.Warn(ex);
                return null;
            }
        }
    }
}

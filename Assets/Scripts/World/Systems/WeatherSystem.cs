using System;

namespace SoccerIsland.World
{
    public enum Season
    {
        Spring, // 春
        Summer, // 夏（台風あり）
        Autumn, // 秋（台風あり）
        Winter, // 冬（まれに雪）
    }

    public enum Weather
    {
        Sunny,  // 晴れ
        Cloudy, // くもり
        Rain,   // 雨：ボールが滑る
        Storm,  // 嵐（台風）：試合は延期
        Snow,   // 雪：足が重い
    }

    /// <summary>
    /// 天気と季節。1年365日を4つの季節に分け、毎日の天気はシードと日付から決まる（同じ日はいつも同じ天気）。
    /// </summary>
    public static class WeatherSystem
    {
        public static Season SeasonOf(int day)
        {
            int d = ((day % 365) + 365) % 365;
            return d < 90 ? Season.Spring : d < 180 ? Season.Summer : d < 270 ? Season.Autumn : Season.Winter;
        }

        public static Weather WeatherOf(int seed, int day)
        {
            unchecked
            {
                uint h = (uint)(seed * 2654435761u) ^ (uint)(day * 40503);
                h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15;
                float r = (h % 10000) / 10000f;
                switch (SeasonOf(day))
                {
                    case Season.Spring: return r < 0.45f ? Weather.Sunny : r < 0.75f ? Weather.Cloudy : Weather.Rain;
                    case Season.Summer: return r < 0.55f ? Weather.Sunny : r < 0.7f ? Weather.Cloudy : r < 0.93f ? Weather.Rain : Weather.Storm;
                    case Season.Autumn: return r < 0.45f ? Weather.Sunny : r < 0.7f ? Weather.Cloudy : r < 0.94f ? Weather.Rain : Weather.Storm;
                    default: return r < 0.4f ? Weather.Sunny : r < 0.75f ? Weather.Cloudy : r < 0.9f ? Weather.Rain : Weather.Snow;
                }
            }
        }

        public static string Label(Weather w)
        {
            switch (w)
            {
                case Weather.Sunny: return "晴れ";
                case Weather.Cloudy: return "くもり";
                case Weather.Rain: return "雨";
                case Weather.Storm: return "嵐";
                default: return "雪";
            }
        }

        public static string Label(Season s) => s == Season.Spring ? "春" : s == Season.Summer ? "夏" : s == Season.Autumn ? "秋" : "冬";

        /// <summary>外で何かするのに向かない天気か。</summary>
        public static bool IsBad(Weather w) => w == Weather.Rain || w == Weather.Storm || w == Weather.Snow;
    }
}

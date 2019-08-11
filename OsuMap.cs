using Newtonsoft.Json;

namespace TournamentUtilities
{
    public class OsuMap
    {
        [JsonProperty("beatmap_id")]
        public int Id;

        [JsonProperty("artist")]
        public string Artist;

        [JsonProperty("title")]
        public string Title;

        [JsonProperty("version")]
        public string DifficultyName;

        [JsonProperty("creator")]
        public string Creator;

        [JsonProperty("difficultyrating")]
        public double StarRating;

        [JsonProperty("diff_size")]
        public double CircleSize;

        [JsonProperty("diff_overall")]
        public double OverallDifficulty;

        [JsonProperty("diff_approach")]
        public double ApproachRate;

        [JsonProperty("diff_drain")]
        public double HealthDrain;

        [JsonProperty("hit_length")]
        public double Length;

        [JsonProperty("bpm")]
        public double Bpm;

        [JsonProperty("max_combo")]
        public int MaxCombo;
    }
}

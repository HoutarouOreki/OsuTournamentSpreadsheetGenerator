using Newtonsoft.Json;

namespace TournamentUtilities
{
    public class BanchoScoreAPI
    {
        [JsonProperty("user_id")]
        public int PlayerId;

        [JsonProperty("score")]
        public int Score;

        [JsonProperty("maxcombo")]
        public int Combo;

        [JsonProperty("count50")]
        public int Count50;

        [JsonProperty("count100")]
        public int Count100;

        [JsonProperty("count300")]
        public int Count300;

        [JsonProperty("countmiss")]
        public int CountMiss;

        [JsonProperty("enabled_mods")]
        public Mods? Mods;

        public double Accuracy => ((50 * Count50) + (100 * Count100) + (300 * Count300)) / (double)(300 * countRatingsTotal);

        public string Grade => Count300 == countRatingsTotal ? "SS" :
            Count300 > 0.9f * countRatingsTotal && Count50 < 0.01f * countRatingsTotal && CountMiss == 0 ? "S" :
            (Count300 > 0.8f * countRatingsTotal && CountMiss == 0) || Count300 > 0.9f * countRatingsTotal ? "A" :
            (Count300 > 0.7f * countRatingsTotal && CountMiss == 0) || Count300 > 0.8f * countRatingsTotal ? "B" :
            Count300 > 0.6f * countRatingsTotal ? "C" : "D";

        private int countRatingsTotal => CountMiss + Count50 + Count100 + Count300;
    }
}

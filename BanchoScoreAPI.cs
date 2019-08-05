using Newtonsoft.Json;

namespace TournamentUtilities
{
    public class BanchoScoreAPI
    {
        [JsonProperty("user_id")]
        public int PlayerId;

        [JsonProperty("score")]
        public int Score;
    }
}

using Newtonsoft.Json;

namespace TournamentUtilities
{
    public class OsuPlayer
    {
        [JsonProperty("user_id")]
        public int Id;

        [JsonProperty("username")]
        public string Username;

        [JsonProperty("country")]
        public string Country;
    }
}

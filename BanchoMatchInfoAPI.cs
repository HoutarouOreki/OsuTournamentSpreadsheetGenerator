using Newtonsoft.Json;

namespace TournamentUtilities
{
    public class BanchoMatchInfoAPI
    {
        [JsonProperty("match_id")]
        public int Id;

        [JsonProperty("name")]
        public string Name;
    }
}

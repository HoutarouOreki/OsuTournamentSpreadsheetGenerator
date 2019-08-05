using Newtonsoft.Json;
using System.Collections.Generic;

namespace TournamentUtilities
{
    public class BanchoMatchAPI
    {
        [JsonProperty("match")]
        public BanchoMatchInfoAPI MatchInfo;

        [JsonProperty("games")]
        public IEnumerable<BanchoGameAPI> Games;
    }
}

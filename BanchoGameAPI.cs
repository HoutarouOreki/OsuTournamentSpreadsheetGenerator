using Newtonsoft.Json;
using System.Collections.Generic;

namespace TournamentUtilities
{
    public class BanchoGameAPI
    {
        [JsonProperty("beatmap_id")]
        public int BeatmapId;

        [JsonProperty("scores")]
        public IEnumerable<BanchoScoreAPI> Scores;
    }
}

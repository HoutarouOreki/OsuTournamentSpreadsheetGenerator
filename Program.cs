using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TournamentUtilities
{
    public class Program
    {
        private static readonly string storage = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/osuTournamentUtilities";
        private static string api_key;
        private static readonly RestClient client = new RestClient("https://osu.ppy.sh/api");

        public static void Main()
        {
            Console.WriteLine("Provide API Key: ");
            api_key = Console.ReadLine();

            var roomIdsFilePath = $"{storage}/roomIds.txt";
            var averagesFilePath = $"{storage}/averages.txt";
            var participantsIdsFilePath = $"{storage}/participants.txt";

            var roomIds = new List<int>();
            var participantIds = new List<int>();

            foreach (var roomId in File.ReadAllLines(roomIdsFilePath))
            {
                var match = Regex.Match(roomId, @"(\d+)");
                if (match.Success)
                    roomIds.Add(int.Parse(match.Groups[1].Value));
            }

            foreach (var participant in File.ReadAllLines(participantsIdsFilePath))
            {
                var match = Regex.Match(participant, @"(\d+)");
                if (match.Success)
                    participantIds.Add(int.Parse(match.Groups[1].Value));
            }

            var s = new StringBuilder();
            var mapScores = MapScores(roomIds, participantIds);

            foreach (var mapAverage in mapScores)
                s.AppendLine($"{mapAverage.Key} has {mapAverage.Value.Count} scores");

            s.AppendLine();

            foreach (var mapAverage in mapScores)
                s.AppendLine($"{mapAverage.Key}\t{mapAverage.Value.Average()}");

            File.WriteAllText(averagesFilePath, s.ToString());
            Console.Write(s.ToString());
        }

        private static Dictionary<int, List<int>> MapScores(IEnumerable<int> roomIds, IEnumerable<int> participantIds)
        {
            var mapScores = new Dictionary<int, List<int>>();

            var matchDownloadTasks = new List<Task<BanchoMatchAPI>>();

            Console.WriteLine("Downloading matches..");

            foreach (var roomId in roomIds)
                matchDownloadTasks.Add(DownloadMatchAsync(roomId));

            Task.WaitAll(matchDownloadTasks.ToArray());
            Console.WriteLine("Matches downloaded!");

            foreach (var match in matchDownloadTasks.Select(t => t.Result))
            {
                Console.WriteLine(match.MatchInfo.Name);

                foreach (var game in match.Games)
                {
                    Console.WriteLine($"{game.BeatmapId} has {game.Scores.Where(s => participantIds.Contains(s.PlayerId)).Count()} scores");
                }

                foreach (var game in match.Games)
                {
                    if (!mapScores.ContainsKey(game.BeatmapId))
                        mapScores.Add(game.BeatmapId, new List<int>());
                    mapScores[game.BeatmapId].AddRange(game.Scores.Where(s => participantIds.Contains(s.PlayerId)).Select(s => s.Score));
                }

                Console.WriteLine();
            }

            return mapScores;
        }

        private static async Task<BanchoMatchAPI> DownloadMatchAsync(int matchId)
        {
            var request = new RestRequest("get_match", Method.GET, DataFormat.None);
            request.AddParameter("k", api_key);
            request.AddParameter("mp", matchId);

            var response = await client.ExecuteGetTaskAsync(request);
            Console.WriteLine($"Received match response: {response.Content.Substring(0, 40)}");

            return JsonConvert.DeserializeObject<BanchoMatchAPI>(response.Content);
        }
    }
}

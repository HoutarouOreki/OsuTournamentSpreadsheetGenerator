using Newtonsoft.Json;
using OfficeOpenXml;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TournamentUtilities
{
    public class Program
    {
        private static readonly string storage = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\osuTournamentUtilities";
        private static string api_key;
        private static readonly RestClient client = new RestClient("https://osu.ppy.sh/api");
        private static readonly Regex number_regex = new Regex(@"^(\d+)$");
        private static readonly Regex loose_number_regex = new Regex(@"(\d+)$");

        private static IEnumerable<string> GetLines(string pasteWhat)
        {
            var list = new List<string>();
            Console.WriteLine($"Paste {pasteWhat}. One per line. When finished, type in \"ok\" and press enter.");
            var inputLine = "";
            while (inputLine != "ok")
            {
                Console.Write(">");
                inputLine = Console.ReadLine();
                if (string.IsNullOrEmpty(inputLine))
                    Console.WriteLine("Empty line entered, ignoring...");
                else if (inputLine != "ok")
                    list.Add(inputLine);
            }
            return list;
        }

        private static IEnumerable<int> GetNumbers(string pasteWhat, bool looseRegex = false)
        {
            var list = new List<int>();
            foreach (var numberString in GetLines($"{pasteWhat}{(looseRegex ? ". You can also paste a link, but make sure the number is at the end of the line" : "")}"))
            {
                var idMatch = looseRegex ? loose_number_regex.Match(numberString) : number_regex.Match(numberString);
                if (!idMatch.Success)
                    Console.WriteLine($"Could not parse number from \"{numberString}\", ignoring...");
                else
                    list.Add(int.Parse(idMatch.Groups[1].Value));
            }
            return list;
        }

        public static void Main()
        {
            if (!Directory.Exists(storage))
                Directory.CreateDirectory(storage);
            Console.WriteLine("This program generates a spreadsheet with players' scores" +
                " on maps. You'll have to provide:");
            Console.WriteLine("- your osu!api key (from https://osu.ppy.sh/p/api/)");
            Console.WriteLine("- players' IDs (or links to their profiles)");
            Console.WriteLine("- match IDs (or mp lobby links)");
            Console.WriteLine("- map IDs");
            while (string.IsNullOrEmpty(api_key) || api_key.Length < 8)
            {
                Console.WriteLine("Paste your api key:");
                Console.Write(">");
                api_key = Console.ReadLine();
            }

            var roomIds = GetNumbers("room IDs", true);
            var participantIds = GetNumbers("player IDs", true);
            var mapIds = GetNumbers("map IDs");

            Console.WriteLine("Upon pressing anything all the needed data will begin to download.");
            Console.ReadKey(true);
            Console.WriteLine("Beginning data download.");
            CompileMapStatistics(roomIds, participantIds, mapIds);
        }

        public static void CompileMapScores(IEnumerable<int> roomIds, IEnumerable<int> participantIds)
        {
            var s = new StringBuilder();
            var mapScores = MapScores(roomIds, participantIds);
            var averagesFilePath = $"{storage}/averages.txt";

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
            var previewString = response.Content.Length > 42 ? response.Content.Substring(0, 40) : response.Content;
            Console.WriteLine($"Received match {matchId} response: {previewString}");

            return JsonConvert.DeserializeObject<BanchoMatchAPI>(response.Content);
        }

        private static async Task<OsuPlayer> DownloadPlayerAsync(int playerId)
        {
            var request = new RestRequest("get_user", Method.GET, DataFormat.None);
            request.AddParameter("k", api_key);
            request.AddParameter("u", playerId);

            var response = await client.ExecuteGetTaskAsync(request);
            var previewString = response.Content.Length > 42 ? response.Content.Substring(0, 40) : response.Content;
            Console.WriteLine($"Received player {playerId} response: {previewString}");

            try
            {
                return JsonConvert.DeserializeObject<OsuPlayer[]>(response.Content)[0];
            }
            catch
            {
                return new OsuPlayer { Id = playerId, Username = $"Unavailable {playerId}", Country = "??" };
            }
        }

        private static async Task<OsuMap> DownloadMapAsync(int mapId)
        {
            var request = new RestRequest("get_beatmaps", Method.GET, DataFormat.None);
            request.AddParameter("k", api_key);
            request.AddParameter("b", mapId);

            var response = await client.ExecuteGetTaskAsync(request);
            Console.WriteLine($"Received map {mapId} response: {response.Content.Substring(0, Math.Min(40, response.Content.Length))}");

            var mapsDeserialized = JsonConvert.DeserializeObject<OsuMap[]>(response.Content);
            if (mapsDeserialized.Length == 0)
                return new OsuMap { Id = mapId, Title = "Deleted map" };

            return mapsDeserialized[0];
        }

        private static void CompileMapStatistics(IEnumerable<int> roomIds, IEnumerable<int> participantIds, IEnumerable<int> mapIds)
        {
            var players = new List<OsuPlayer>();
            var matches = new List<BanchoMatchAPI>();
            var maps = new List<OsuMap>();
            var playerDownloadTasks = new List<Task<OsuPlayer>>();
            var matchDownloadTasks = new List<Task<BanchoMatchAPI>>();
            var mapDownloadTasks = new List<Task<OsuMap>>();

            playerDownloadTasks.AddRange(participantIds.Select(playerId => DownloadPlayerAsync(playerId)));
            matchDownloadTasks.AddRange(roomIds.Select(roomId => DownloadMatchAsync(roomId)));
            mapDownloadTasks.AddRange(mapIds.Select(mapId => DownloadMapAsync(mapId)));

            Console.WriteLine("Downloading players, matches and maps");

            Task.WaitAll(playerDownloadTasks.ToArray());
            Task.WaitAll(matchDownloadTasks.ToArray());
            Task.WaitAll(mapDownloadTasks.ToArray());

            Console.WriteLine("Players, matches and maps downloaded");

            players.AddRange(playerDownloadTasks.Select(pt => pt.Result));
            matches.AddRange(matchDownloadTasks.Select(mt => mt.Result));
            maps.AddRange(mapDownloadTasks.Select(mt => mt.Result));

            CreateMappoolStatisticsSpreadsheet(players, matches, maps);
        }

        private static void CreateMappoolStatisticsSpreadsheet(List<OsuPlayer> players, List<BanchoMatchAPI> matches, List<OsuMap> maps)
        {
            Console.WriteLine("Compiling spreadsheet");

            using var p = new ExcelPackage();
            var spreadsheetFilePath = new FileInfo($"{storage}/mappoolStatistics.xlsx");

            { // the player summary spreadsheet
                var ws = p.Workbook.Worksheets.Add("Players summary");

                var placeNumberCell = ws.Cells["A1"];
                placeNumberCell.Value = "#";

                var playerNameCell = ws.Cells["B1"];
                playerNameCell.Value = "Player name";

                var totalScoreCell = ws.Cells["C1"];
                totalScoreCell.Value = "Total score";

                var mapColumn = 4;
                foreach (var map in maps)
                {
                    var mapTitleCell = ws.Cells[1, mapColumn++];
                    mapTitleCell.Value = map.Title;
                }

                var currentRow = 1;
                var playerPlace = 1;
                foreach (var player in players.OrderByDescending(p => matches.SelectMany(m => m.Games.SelectMany(g => g.Scores).Where(s => s.PlayerId == p.Id)).Sum(score => score.Score)))
                {
                    currentRow++;
                    ws.Cells[currentRow, 1].Value = playerPlace++;

                    ws.Cells[currentRow, 2].Value = player.Username;

                    ws.Cells[currentRow, 3].Value = matches.SelectMany(m => m.Games.SelectMany(g => g.Scores).Where(s => s.PlayerId == player.Id)).Sum(score => score.Score);

                    var currentColumn = 3;
                    foreach (var map in maps)
                    {
                        currentColumn++;
                        var scores = matches.SelectMany(m => m.Games.Where(g => g.BeatmapId == map.Id).SelectMany(g => g.Scores).Where(score => score.PlayerId == player.Id)).Select(s => s.Score);
                        if (scores.Count() == 1)
                            ws.Cells[currentRow, currentColumn].Value = scores.First();
                        else
                            ws.Cells[currentRow, currentColumn].Value = string.Join(", ", scores);
                    }
                }
            }

            foreach (var map in maps)
            {
                var ws = p.Workbook.Worksheets.Add(map.Title);

                var titleLabelCell = ws.Cells["A1:B2"];
                titleLabelCell.Merge = true;
                titleLabelCell.Value = "Title";

                var titleValueCell = ws.Cells["C1:E2"];
                titleValueCell.Merge = true;
                titleValueCell.Value = map.Title;
                titleValueCell.Style.WrapText = true;

                var artistLabelCell = ws.Cells["F1:F2"];
                artistLabelCell.Merge = true;
                artistLabelCell.Value = "Artist";

                var artistValueCell = ws.Cells["G1:H2"];
                artistValueCell.Merge = true;
                artistValueCell.Value = map.Artist;

                var mapsetHostLabelCell = ws.Cells["A3:B4"];
                mapsetHostLabelCell.Merge = true;
                mapsetHostLabelCell.Value = "Mapset Host";

                var mapsetHostValueCell = ws.Cells["C3:E4"];
                mapsetHostValueCell.Merge = true;
                mapsetHostValueCell.Value = map.Creator;

                var difficultyNameLabelCell = ws.Cells["F3:F4"];
                difficultyNameLabelCell.Merge = true;
                difficultyNameLabelCell.Value = "Difficulty";

                var difficultyNameValueCell = ws.Cells["G3:H4"];
                difficultyNameValueCell.Merge = true;
                difficultyNameValueCell.Value = map.DifficultyName;

                ws.Cells["I1"].Value = "SD";
                ws.Cells["I2"].Value = "AR";
                ws.Cells["I3"].Value = "HP";
                ws.Cells["I4"].Value = "Combo";
                ws.Cells["J1"].Value = map.StarRating;
                ws.Cells["J1"].Style.Numberformat.Format = "#.##";
                ws.Cells["J2"].Value = map.ApproachRate;
                ws.Cells["J3"].Value = map.HealthDrain;
                ws.Cells["J4"].Value = map.MaxCombo;

                ws.Cells["K1"].Value = "CS";
                ws.Cells["K2"].Value = "OD";
                ws.Cells["K3"].Value = "Length";
                ws.Cells["K4"].Value = "BPM";
                ws.Cells["L1"].Value = map.CircleSize;
                ws.Cells["L2"].Value = map.OverallDifficulty;
                ws.Cells["L3"].Value = map.Length;
                ws.Cells["L3"].Style.Numberformat.Format = "00:00";
                ws.Cells["L4"].Value = map.Bpm;

                ws.Cells["A6"].Value = "Pos";
                ws.Column(1).Width = 4;
                ws.Cells["B6"].Value = "Cn.";
                ws.Column(2).Width = 4;
                ws.Cells["C6"].Value = "Player";
                ws.Column(3).Width = 18;
                ws.Cells["D6"].Value = "Rank";
                ws.Column(4).Width = 5.2;
                ws.Cells["E6"].Value = "Score";
                ws.Column(5).Width = 9;
                ws.Cells["F6"].Value = "Combo";
                ws.Column(6).Width = 14;
                ws.Cells["G6"].Value = "Accuracy";
                ws.Column(7).Width = 14;
                ws.Cells["H6"].Value = "Mods";
                ws.Column(8).Width = 9;
                ws.Column(9).Width = 9;
                ws.Column(10).Width = 9;
                ws.Column(11).Width = 9;
                ws.Column(12).Width = 9;
                ws.Column(13).Width = 15;
                ws.Column(14).Width = 12.5;
                ws.Row(6).Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thick;
                ws.Row(6).Style.Border.Bottom.Color.SetColor(Color.Gray);

                ws.Cells["A1:A500,A6:I6,K1:K4,I1:I4,F1:F4,M1:M4"].Style.Font.Bold = true;
                ws.Cells["A1:N500"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                ws.Cells["A1:N500"].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                var scores = matches.SelectMany(m => m.Games).Where(g => g.BeatmapId == map.Id).SelectMany(g => g.Scores).Where(s => players.Any(player => player.Id == s.PlayerId)).OrderByDescending(s => s.Score).ToList();

                ws.Cells["M1"].Value = "Picks";
                ws.Cells["N1"].Value = matches.Count(m => m.Games.Any(g => g.BeatmapId == map.Id));
                ws.Cells["M2"].Value = "Average score";
                ws.Cells["N2"].Formula = $"AVERAGE(E7:E500)";
                ws.Cells["N2"].Style.Numberformat.Format = "#";
                ws.Cells["M3"].Value = "Median score";
                ws.Cells["N3"].Formula = $"MEDIAN(E7:E500)";
                ws.Cells["N3"].Style.Numberformat.Format = "#";

                var lastScore = int.MinValue;
                var currentRow = 6;
                foreach (var score in scores)
                {
                    currentRow++;
                    var currentColumn = 1;
                    ws.Cells[currentRow, currentColumn++].Value = score.Score == lastScore ? ws.Cells[currentRow - 1, 1].Value : currentRow - 6;
                    lastScore = score.Score;
                    var player = players.Find(_p => _p.Id == score.PlayerId);
                    ws.Cells[currentRow, currentColumn++].Value = player.Country;
                    ws.Cells[currentRow, currentColumn++].Value = player.Username;
                    ws.Cells[currentRow, currentColumn++].Value = score.Grade;
                    ws.Cells[currentRow, currentColumn++].Value = score.Score;
                    ws.Cells[currentRow, currentColumn++].Value = score.Combo;
                    ws.Cells[currentRow, currentColumn].Style.Numberformat.Format = "0.00%";
                    ws.Cells[currentRow, currentColumn++].Value = score.Accuracy;
                    var game = matches.SelectMany(m => m.Games).Single(g => g.Scores.Contains(score));
                    var match = matches.Find(m => m.Games.Contains(game));
                    ws.Cells[currentRow, currentColumn++].Value = ((Mods)((int)(game.GlobalMods ?? Mods.None) + (int)(score.Mods ?? Mods.None))).ToString().Replace("None", "");
                    ws.Cells[currentRow, currentColumn].Hyperlink = new Uri($@"https://osu.ppy.sh/community/matches/{match.MatchInfo.Id}");
                    ws.Cells[currentRow, currentColumn].Value = "match link";
                }

                ws.Cells["B7:B500"].Style.Font.Size = 10;
                ws.Cells["E7:E500"].Style.Font.Bold = true;
                ws.Cells["H7:H500"].Style.Numberformat.Format = "#0.00%";
                var condRank = ws.Cells["E7:E500"].ConditionalFormatting;
                var condRankSS = condRank.AddContainsText();
                condRankSS.Text = "SS";
                condRankSS.Style.Fill.BackgroundColor.Theme = condRankSS.Style.Font.Color.Theme = 1;
                condRankSS.Style.Fill.BackgroundColor.Tint = 0.25;
                condRankSS.Style.Font.Color.Tint = 0.75;
                var condRankS = condRank.AddContainsText();
                condRankS.Text = "S";
                condRankS.Style.Fill.BackgroundColor.Theme = condRankS.Style.Font.Color.Theme = 5;
                condRankS.Style.Fill.BackgroundColor.Tint = 0.25;
                condRankS.Style.Font.Color.Tint = 0.75;
                var condRankA = condRank.AddContainsText();
                condRankA.Text = "A";
                condRankA.Style.Fill.BackgroundColor.Theme = condRankA.Style.Font.Color.Theme = 9;
                condRankA.Style.Fill.BackgroundColor.Tint = 0.25;
                condRankA.Style.Font.Color.Tint = 0.75;
                var condRankB = condRank.AddContainsText();
                condRankB.Text = "B";
                condRankB.Style.Fill.BackgroundColor.Theme = condRankB.Style.Font.Color.Theme = 8;
                condRankB.Style.Fill.BackgroundColor.Tint = 0.25;
                condRankB.Style.Font.Color.Tint = 0.75;
                var condRankC = condRank.AddContainsText();
                condRankC.Text = "C";
                condRankC.Style.Fill.BackgroundColor.Color = Color.Purple;
                condRankC.Style.Font.Color.Color = Color.White;
                var condRankD = condRank.AddContainsText();
                condRankD.Text = "D";
                condRankD.Style.Fill.BackgroundColor.Color = Color.Purple;
            }

            while (true)
            {
                try
                {
                    p.SaveAs(spreadsheetFilePath);
                    Console.WriteLine("\n------------------------------------------------\n");
                    Console.WriteLine($"Successfully written a spreadsheet to {spreadsheetFilePath}");
                    Console.WriteLine("You can import the file to Google Sheets by going to docs.google.com,");
                    Console.WriteLine("clicking the folder icon on the right side, \"Upload\" tab,");
                    Console.WriteLine("and selecting the spreadsheet.");
                    Console.WriteLine("Press any key to open the spreadsheet's folder and close this program.");
                    Console.ReadKey(true);
                    Console.WriteLine("Opening folder in 3 seconds");
                    Thread.Sleep(3000);
                    Process.Start("explorer.exe", $@"/select, ""{spreadsheetFilePath.FullName}""");
                    break;
                }
                catch
                {
                    Console.WriteLine($"Couldn't write to {spreadsheetFilePath}, press anything to try again. Make sure the file is not in use.");
                    Console.ReadKey(true);
                }
            }

        }
    }
}

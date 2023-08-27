﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpAdbClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.RightsManagement;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RandomStreamLoader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // Steps:
        // 1 Select a TV (preferably UI selection but can be a dropbox)
        // 2 Select a category on twitch
        // 3 Select between random stream or popular stream (or choose a specific one)
        // 4 Tell the TV to launch Twitch and go to that stream
        // 5 Log the current stream that the tv is running
        // 6 Log recently launched streams and have then as an option to launch back into
        // Have a "refresh" button
        // 
        // IDEAS
        // Ad TV Object that knows everything about a TV
        // Add a no stream option
        // Fix twich data limitaition


        // ABD server
        AdbServer server = new AdbServer(); 
        AdbClient adbClient = new AdbClient();

        string clientID = "yyvs5vmhixmy5b9eexa570tmeggrb1";
        string clientSecret = "zx55ojox0omv462rk5geujywe7kxvm";
        private static readonly HttpClient httpClient = new HttpClient();
        List<TV> tvList = new List<TV>();


        public MainWindow()
        {

            InitializeComponent();

            // Add the inital TVs
            addTV("Front Lobby TV", "172.16.15.94");
            addTV("Middle VIP TV", "172.16.15.205");
            addTV("Right VIP TV", "172.16.15.199");
            addTV("Left VIP TV", "172.16.15.7");
            addTV("Front Desk 1 TV", "172.16.15.194");
            addTV("Lounge TV 1", "172.16.15.202");
            addTV("Lounge TV 2", "172.16.15.207");
            addTV("Game Room 1", "172.16.15.252");
            addTV("Game Room 2", "172.16.15.66");
            addTV("Game Room 3", "172.16.15.64");
            addTV("Game Room 4", "172.16.15.246");
            addTV("Party Room A", "172.16.15.43");
            addTV("Party Room B", "172.16.15.241");
            

            // Start the ABD server
            var result = server.StartServer(@"C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe", restartServerIfNewer: true);
            Console.WriteLine(result);

            //Add the clientID to the HttpClient
            httpClient.DefaultRequestHeaders.Add("Client-ID", clientID);

            //Dispatch Timers
            DispatcherTimer tvRefreshTimer = new DispatcherTimer();
            tvRefreshTimer.Tick += new EventHandler(refreshTVs);
            tvRefreshTimer.Interval = TimeSpan.FromMinutes(15); //15
            tvRefreshTimer.Start();
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            ComboBoxItem selectedBox = tvBox.SelectedItem as ComboBoxItem;
            TV selectedTV = (TV)selectedBox.Tag;
            ComboBoxItem selectedCategory = categoryBox.SelectedItem as ComboBoxItem;
            string selectedGame = categoryBox.Text;
            if (selectedGame.Equals("Amazon Photos"))
            {
                launchPhotosOnTV(selectedTV.tvIP);
                selectedTV.lastTimeTVUpdated = System.DateTime.Now;
                selectedTV.currGame = selectedGame;
            } 
            else if (selectedGame.Equals("No Stream"))
            {
                selectedTV.currGame = "";
                selectedTV.mostRecentStream = "";
                selectedTV.lastTimeTVUpdated = System.DateTime.Now;
            }
            {
                string mostPopularStreamer = Task.Run(() => getMostPopularStreamerAsync(selectedGame)).GetAwaiter().GetResult();
                launchTwitchOnTV(selectedTV.tvIP, $"twitch://stream/{mostPopularStreamer}");
                selectedTV.mostRecentStream = mostPopularStreamer;
                selectedTV.lastTimeTVUpdated = System.DateTime.Now;
                selectedTV.currGame = selectedGame;
            }
        }

        public class TokenResponse
        {
            [JsonProperty("access_token")]
            public string token { get; set; }
        }

        // Gets a twitch app token based on the client ID and secret provided above
        private async Task<string> GetTwtichToken()
        {
            //Set the POST Parameters
            var values = new Dictionary<string, string>
            {
                {"client_id", clientID},
                {"client_secret", clientSecret},
                {"grant_type", "client_credentials"}
            };

            //Add the parameters
            var content = new FormUrlEncodedContent(values);
            Console.WriteLine("Attempting to get Twitch token");

            //Get the response
            var response = await httpClient.PostAsync("https://id.twitch.tv/oauth2/token", content);
            string responseString = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Token Aqcuired");

            //Deserialize the JSON and pull the token from the JSON
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseString);
            return tokenResponse.token;
        }

        // Gets a game ID for a specified game
        private async Task<string> GetGameID(string gameName)
        {
            //Setup the uri for getting a game ID from Name
            Console.WriteLine($"Getting Game ID for {gameName}");
            string twitchToken = await GetTwtichToken();
            string uri = $"https://api.twitch.tv/helix/games?name={gameName}";

            //Add Client request Headers
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {twitchToken}");

            //Get the response from twitch
            var response = await httpClient.GetAsync(uri);
            var responseString = await response.Content.ReadAsStringAsync();
            //Remove the Twitch Token
            httpClient.DefaultRequestHeaders.Remove("Authorization");
            JObject gameResponse = JObject.Parse(responseString);

            //Check if we got valid data, and if we do return the game ID
            if (!gameResponse["data"].ToString().Equals("[]"))
            {
                Console.WriteLine($"The Game ID for {gameName} is " + gameResponse["data"][0]["id"]);
                return (string)gameResponse["data"][0]["id"];
            }
            else
            {
                Console.WriteLine("The game " + gameName + " does not exist. Returning empty string.");
                return "";
            }
        }

        // Gets the most popular streamer streaming in the given category
        private async Task<string> getMostPopularStreamerAsync(string desiredCategory)
        {
            //Create the uri 
            Console.WriteLine($"\n----- Trying to get the most popular streamer in {desiredCategory} -----");
            string gameID = await GetGameID(desiredCategory);
            string uri = $"https://api.twitch.tv/helix/streams?game_id={gameID}&type=live";

            //Add Client request Headers
            string twitchToken = await GetTwtichToken();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {twitchToken}");

            //Send out the request
            var response = await httpClient.GetAsync(uri);
            string responseString = await response.Content.ReadAsStringAsync();

            //Remove the Twtich Token
            httpClient.DefaultRequestHeaders.Remove("Authorization");

            //Check if the response was successful
            if (response.IsSuccessStatusCode)
            {
                string jsonContent = await response.Content.ReadAsStringAsync();
                var twitchStreamsResponse = JsonConvert.DeserializeObject<TwitchStreamsResponse>(jsonContent);

                //Check if there were any valid streams in the response
                if (twitchStreamsResponse != null && twitchStreamsResponse.Streams.Count > 0)
                {
                    foreach (var stream in twitchStreamsResponse.Streams)
                    {
                        Console.WriteLine($"Most popular streamer for {desiredCategory} is: {stream.UserName}");
                        // You can access other stream properties here if needed
                        return stream.UserName;
                    }
                }
                else
                {
                    Console.WriteLine($"No live streams found for {desiredCategory}.");
                    return "";
                }
            }
            else
            {
                Console.WriteLine($"Failed to fetch streams: {response.ReasonPhrase}");
                return "";
            }
            return "";
        }

        public class GameID
        {
            [JsonProperty("id")]
            public string gameID { get; set; }
        }

        public class TwitchStreamsResponse
        {
            [JsonProperty("data")]
            public List<Stream> Streams { get; set; }
        }

        public class Stream
        {
            [JsonProperty("user_name")]
            public string UserName { get; set; }

            [JsonProperty("viewer_count")]
            public int ViewerCount { get; set; }
        }


        private void launchTwitchOnTV(string tvIP, string twitchUrl)
        {
            try
            {
                adbClient.Connect(tvIP);
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 500) { }
                DispatcherTimer dispatcherTimer = new DispatcherTimer();
                var reciever = new ConsoleOutputReceiver();
                Console.WriteLine("Trying to launch twitch on IP " + tvIP);
                adbClient.ExecuteRemoteCommand("input keyevent KEYCODE_WAKEUP", adbClient.GetDevices().First(), reciever);
                // Format twitch://stream/NickEh30
                adbClient.ExecuteRemoteCommand($"am start -a android.intent.action.VIEW -d {twitchUrl} tv.twitch.android.viewer", adbClient.GetDevices().First(), reciever);
                Console.WriteLine("The TV responded:");
                Console.WriteLine(reciever.ToString());
                adbClient.Disconnect(new DnsEndPoint(tvIP, 5555));
                Console.WriteLine("Disconnected from " + tvIP + "\n");
            }
            catch
            {
                MessageBox.Show("Failed to connect to TV");
                try
                {
                    adbClient.Disconnect(new DnsEndPoint(tvIP, 5555));
                }
                catch { }
            }
        }

        private void launchPhotosOnTV(string tvIP)
        {
            try
            {
                adbClient.Connect(tvIP);
                var reciever = new ConsoleOutputReceiver();
                Console.WriteLine("Trying to launch photos on IP " + tvIP);
                adbClient.ExecuteRemoteCommand("input keyevent KEYCODE_WAKEUP", adbClient.GetDevices().First(), reciever);
                // Format twitch://stream/NickEh30
                adbClient.ExecuteRemoteCommand($"am start -n com.amazon.photos/.activity.MainActivity", adbClient.GetDevices().First(), reciever);
                Console.WriteLine("The TV responded:");
                Console.WriteLine(reciever.ToString());
                adbClient.Disconnect(new DnsEndPoint(tvIP, 5555));
                Console.WriteLine("Disconnected from " + tvIP + "\n");
            }
            catch
            {
                MessageBox.Show("Failed to connect to TV");

            }
        }

        private void addTV(String tvName, String tvIP)
        {
            ComboBoxItem newTVItem = new ComboBoxItem();
            TV newTV = new TV(tvName, tvIP);
            newTVItem.Content = newTV.tvName;
            newTVItem.Tag = newTV;
            tvList.Add(newTV);
            tvBox.Items.Add(newTVItem);
        }

        private void refreshTVs(object sender, EventArgs e)
        {
            Console.WriteLine("----- Refreshing TVs -----");
            foreach (TV tv in tvList)
            {
                Console.WriteLine("\nRefreshing " + tv.tvName);
                if (!tv.currGame.Equals(""))
                {
                    string mostPopularStreamer = Task.Run(() => getMostPopularStreamerAsync(tv.currGame)).GetAwaiter().GetResult();
                    if (!mostPopularStreamer.Equals(tv.mostRecentStream))
                    {
                        launchTwitchOnTV(tv.tvIP, $"twitch://stream/{mostPopularStreamer}");
                        tv.mostRecentStream = mostPopularStreamer;
                    }
                    else
                    {
                        Console.WriteLine("Current stream is still live and the most popular");
                    }
                } 
                else
                {
                    Console.WriteLine("No stream set");
                }
                tv.lastTimeTVUpdated = System.DateTime.Now;
            }
            Console.WriteLine("Time Stamp: " + System.DateTime.Now.ToString());
        }

        // Custom wrapper class to help keep track of TVs
        public class TV
        {
            public string tvName { get; set; }
            public string tvIP { get; set; }
            public string mostRecentStream { get; set; }
            public string currGame { get; set; }
            public DateTime lastTimeTVUpdated { get; set; }

            public TV(string tvName, string tvIP)
            {
                this.tvName = tvName;
                this.tvIP = tvIP;
                this.currGame = "";
            }
        }

        private void TestBtn_Click(object sender, RoutedEventArgs e)
        {
            //string mostPopularStreamer = Task.Run(() => getMostPopularStreamerAsync("overwatch 2")).GetAwaiter().GetResult();
            //Console.WriteLine("The most popular Streamer is: " + mostPopularStreamer);
        }
    }
}
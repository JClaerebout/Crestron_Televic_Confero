using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Crestron.SimplSharp;

namespace TelevicConfero
{
    public class MicFeedbackEventArgs : EventArgs
    {
        public ushort Connected { get; set; }
        public ushort FirstActiveMic { get; set; }
        public ushort LastActiveMic { get; set; }
    }

    public class ConferoController
    {
        private readonly object sync = new object();
        private CancellationTokenSource session;
        public event EventHandler<MicFeedbackEventArgs> OnFeedback;

        // Each pulse replaces the previous subscription; old callbacks are suppressed.
        public void Initialize(string ip, string apiKey)
        {
            lock (sync)
            {
                if (session != null) session.Cancel();
                session = new CancellationTokenSource();
                var token = session.Token;
                Publish(token, false, null);
                Task.Run(() => RunAsync(ip, apiKey, token));
            }
        }

        private void Publish(CancellationToken token, bool connected, JArray speakers)
        {
            ushort first = 0, last = 0;
            if (speakers != null && speakers.Count > 0)
            {
                foreach (var seat in speakers)
                    if (seat.Type != JTokenType.Integer || (long)seat < 1 || (long)seat > ushort.MaxValue)
                        throw new FormatException("Invalid speaker seat number");
                first = (ushort)speakers[0];
                last = (ushort)speakers[speakers.Count - 1];
            }
            lock (sync)
            {
                if (token.IsCancellationRequested) return;
                var handler = OnFeedback;
                if (handler != null) handler(this, new MicFeedbackEventArgs
                {
                    Connected = (ushort)(connected ? 1 : 0),
                    FirstActiveMic = first, LastActiveMic = last
                });
            }
        }

        private static async Task<HttpResponseMessage> GetResponse(HttpClient client, string path, CancellationToken token)
        {
            try
            {
                return await client.GetAsync(path, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested) throw;
                throw new TimeoutException("HTTP request timed out after 40 seconds: " + new Uri(client.BaseAddress, path));
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException("GET " + new Uri(client.BaseAddress, path) + ": " + ex.Message, ex);
            }
        }

        private static async Task<JArray> ReadSpeakers(HttpClient client, CancellationToken token)
        {
            using (var response = await GetResponse(client, "api/discussion/speakers", token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return JArray.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }

        private async Task RunAsync(string ip, string apiKey, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(apiKey))
                    throw new ArgumentException("IP and API_Key must be configured");
                var address = new UriBuilder("http://" + ip.Trim());
                if (address.Port == 80) address.Port = 9080;
                address.Path = "/";
                CrestronConsole.PrintLine("Confero: Connecting to {0} (HTTP speaker events)", address.Uri);
                using (var client = new HttpClient())
                {
                    client.BaseAddress = address.Uri;
                    client.Timeout = TimeSpan.FromSeconds(40);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            long? lastId = null;
                            bool firstPoll = true;
                            JArray speakers = null;
                            while (!token.IsCancellationRequested)
                            {
                                var path = "api/notification/events?include-filter=Discussion";
                                if (lastId.HasValue) path += "&minimum-id=" + (lastId.Value + 1);
                                // Open the long poll before the startup snapshot to avoid a subscription gap.
                                var pending = GetResponse(client, path, token);
                                try
                                {
                                    if (firstPoll)
                                    {
                                        speakers = await ReadSpeakers(client, token).ConfigureAwait(false);
                                        // An idle room may not produce an event response for some time.
                                        // A successful authenticated snapshot already confirms connectivity.
                                        Publish(token, true, speakers);
                                    }
                                }
                                catch
                                {
                                    try { using (await pending.ConfigureAwait(false)) { } } catch { }
                                    throw;
                                }
                                HttpResponseMessage eventResponse;
                                try
                                {
                                    eventResponse = await pending.ConfigureAwait(false);
                                }
                                catch (TimeoutException)
                                {
                                    // An idle long poll can outlive the client timeout. Check the
                                    // ordinary API before declaring a failed connection, and retain
                                    // the event cursor so renewal does not discard queued changes.
                                    speakers = await ReadSpeakers(client, token).ConfigureAwait(false);
                                    Publish(token, true, speakers);
                                    firstPoll = false;
                                    continue;
                                }
                                using (var response = eventResponse)
                                {
                                    response.EnsureSuccessStatusCode();
                                    bool refresh = firstPoll;
                                    if (response.StatusCode != HttpStatusCode.NoContent)
                                    {
                                        var notification = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                                        lastId = (long)notification["id"];
                                        refresh |= (bool?)notification["discontinuity"] == true;
                                        if ((string)notification["module"] == "Discussion" &&
                                            (string)notification["name"] == "SpeakersChanged")
                                        {
                                            var list = notification["data"] as JArray;
                                            if (list == null) refresh = true;
                                            else speakers = list;
                                        }
                                    }
                                    // Reconcile startup overlap and lost event history against current state.
                                    if (refresh) speakers = await ReadSpeakers(client, token).ConfigureAwait(false);
                                    Publish(token, true, speakers);
                                }
                                firstPoll = false;
                                await Task.Delay(50, token).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (token.IsCancellationRequested) return;
                            Publish(token, false, null);
                            CrestronConsole.PrintLine("Confero: {0}", ex.Message);
                            await Task.Delay(2000, token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Publish(token, false, null);
                CrestronConsole.PrintLine("Confero: {0}", ex.Message);
            }
        }
    }
}

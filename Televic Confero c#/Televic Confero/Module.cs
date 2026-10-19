using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TelevicConfero
{
    public class ModuleFeedbackEventArgs : EventArgs
    {
        public ushort SeatCount { get; set; }
        public ushort SpeakerCount { get; set; }
        public ushort RequestCount { get; set; }
        public ushort MeetingCount { get; set; }
        public ushort MeetingRunning { get; set; }
        public ushort RecordingActive { get; set; }
        public ushort DiscussionMode { get; set; }
        public ushort OpenMics { get; set; }
        public string MeetingId { get; set; }
        public string MeetingTitle { get; set; }
        public string RecordingState { get; set; }
        public string Status { get; set; }
    }

    public partial class ConferoController
    {
        // All metadata reads, notification applications and commands share this gate.
        // Thus an older snapshot cannot overwrite a newer applied notification.
        private sealed class ModuleSession
        {
            public CancellationToken Token;
            public string Address, Key;
            public readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
            public Task CommandTail = Task.FromResult(0);
            public JArray Seats = new JArray(), Speakers = new JArray(), Requests = new JArray(), Meetings = new JArray();
            public JObject Meeting = new JObject(), Settings = new JObject();
            public readonly Dictionary<int, string> Names = new Dictionary<int, string>();
            public readonly Dictionary<string, JToken> Options = new Dictionary<string, JToken>();
            public string Recording = "Unknown", Status = "Connecting";
            public string CommandStatus = "";
            public bool Connected;
            public DateTime Refreshed = DateTime.MinValue;
        }
        private ModuleSession module = new ModuleSession();
        private bool moduleEnabled;
        private bool defaultHttps;
        private ushort debugPrint;
        private int seatRows, speakerRows, requestRows, meetingRows, refreshSeconds = 30;
        private static readonly string[] Modes = { "directSpeak", "request", "group", "operator", "handsFree" };
        public event EventHandler<ModuleFeedbackEventArgs> OnModuleFeedback;

        public void ConfigureTransport(ushort https, ushort debug)
        {
            lock (sync) { defaultHttps = https != 0; debugPrint = debug; }
        }
        private void Trace(string message)
        {
            if (debugPrint != 0) Crestron.SimplSharp.CrestronConsole.PrintLine("Confero: {0}", message);
        }

        public void Configure(ushort microphones, ushort speakers, ushort requests, ushort meetings, ushort refresh)
        {
            if (microphones < 1 || speakers < 1 || requests < 1 || meetings < 1 || refresh < 10)
                throw new ArgumentException("Row capacities must be positive; refresh must be at least 10 seconds");
            lock (sync)
            {
                seatRows = microphones; speakerRows = speakers; requestRows = requests; meetingRows = meetings;
                refreshSeconds = refresh; moduleEnabled = true;
            }
        }

        private void ResetModule(string address, string key, CancellationToken token)
        {
            module = new ModuleSession { Address = address, Key = key, Token = token };
        }
        private ModuleSession Current(CancellationToken token)
        {
            lock (sync) return moduleEnabled && module != null && module.Token == token && !token.IsCancellationRequested ? module : null;
        }
        private void Emit(ModuleSession s)
        {
            lock (sync)
            {
                if (s != module || s.Token.IsCancellationRequested) return;
                var handler = OnModuleFeedback;
                if (handler == null) return;
                if (debugPrint > 1) Trace("Feedback seats=" + s.Seats.Count + " speakers=" + s.Speakers.Count + " requests=" + s.Requests.Count + "; " + s.Status);
                handler(this, new ModuleFeedbackEventArgs {
                    SeatCount = Count(s.Seats), SpeakerCount = Count(s.Speakers), RequestCount = Count(s.Requests), MeetingCount = Count(s.Meetings),
                    MeetingId = (string)s.Meeting["meetingId"] ?? "", MeetingTitle = (string)s.Meeting["title"] ?? "",
                    MeetingRunning = Bit(!string.IsNullOrEmpty((string)s.Meeting["meetingId"])),
                    RecordingState = s.Recording, RecordingActive = Bit(s.Recording == "recording"),
                    DiscussionMode = (ushort)(Array.IndexOf(Modes, (string)s.Settings["microphoneMode"]) + 1),
                    OpenMics = (ushort?)s.Settings["maximumNumberOfSpeakers"] ?? 0,
                    Status = s.Status + (s.CommandStatus.Length == 0 ? "" : "; " + s.CommandStatus)
                });
            }
        }
        private static ushort Bit(bool value) { return (ushort)(value ? 1 : 0); }
        private static ushort Count(JArray list) { return (ushort)Math.Min(65535, list.Count); }
        private static JToken Row(JArray list, ushort index, int capacity) { return index > 0 && index <= capacity && index <= list.Count ? list[index - 1] : null; }
        private string Name(ModuleSession s, int number) { string name; return s.Names.TryGetValue(number, out name) ? name : "Seat " + number; }
        public ushort GetSeatNumber(ushort row) { lock (sync) return (ushort?)Row(module.Seats, row, seatRows)?["seatNumber"] ?? 0; }
        public ushort GetSeatMic(ushort row) { lock (sync) return Bit((bool?)Row(module.Seats, row, seatRows)?["microphoneOn"] == true); }
        public ushort GetSeatRequest(ushort row) { lock (sync) return Bit((bool?)Row(module.Seats, row, seatRows)?["requestingToSpeak"] == true); }
        public string GetSeatRole(ushort row) { lock (sync) return (string)Row(module.Seats, row, seatRows)?["role"] ?? ""; }
        public string GetSeatName(ushort row) { lock (sync) { var n = GetSeatNumber(row); return n == 0 ? "" : Name(module, n); } }
        public ushort GetSpeakerNumber(ushort row) { lock (sync) return (ushort?)Row(module.Speakers, row, speakerRows) ?? 0; }
        public ushort GetRequestNumber(ushort row) { lock (sync) return (ushort?)Row(module.Requests, row, requestRows) ?? 0; }
        public string GetSpeakerName(ushort row) { lock (sync) { var n = GetSpeakerNumber(row); return n == 0 ? "" : Name(module, n); } }
        public string GetRequestName(ushort row) { lock (sync) { var n = GetRequestNumber(row); return n == 0 ? "" : Name(module, n); } }
        public string GetMeetingId(ushort row) { lock (sync) return (string)Row(module.Meetings, row, meetingRows)?["scheduledMeetingId"] ?? ""; }
        public string GetMeetingName(ushort row) { lock (sync) return (string)Row(module.Meetings, row, meetingRows)?["name"] ?? ""; }

        private void PublishModuleSpeakers(CancellationToken token, bool connected, JArray speakers)
        {
            var s = Current(token);
            if (s == null) return;
            s.Connected = connected;
            if (!connected)
            {
                // Clear all displayed data and invalidate row mappings on connection loss.
                s.Seats = new JArray(); s.Requests = new JArray(); s.Meetings = new JArray();
                s.Meeting = new JObject(); s.Settings = new JObject(); s.Names.Clear();
                s.Recording = "Unknown"; s.Status = "Disconnected"; s.CommandStatus = ""; s.Refreshed = DateTime.MinValue;
            }
            s.Speakers = speakers == null ? new JArray() : (JArray)speakers.DeepClone();
            Emit(s);
        }
        private HttpClient Client(ModuleSession s)
        {
            var address = new UriBuilder(s.Address.Contains("://") ? s.Address.Trim() : "http://" + s.Address.Trim());
            if (address.Port == 80 || address.Port == 443) address.Port = address.Scheme == "https" ? 9443 : 9080;
            address.Path = "/";
            var client = new HttpClient { BaseAddress = address.Uri, Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Key.Trim());
            return client;
        }
        private async Task<JToken> Read(HttpClient client, string path, ModuleSession s)
        {
            using (var response = await client.GetAsync("api/" + path, s.Token).ConfigureAwait(false))
            {
                Trace("GET api/" + path + " HTTP " + (int)response.StatusCode);
                if ((path == "meeting" && ((int)response.StatusCode == 412 || (int)response.StatusCode == 405))) return new JObject();
                response.EnsureSuccessStatusCode();
                return JToken.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }
        private void Settings(ModuleSession s, JObject value)
        {
            s.Settings = value;
            var mode = (string)value["microphoneMode"];
            if (mode != null && value["options"] is JObject) s.Options[mode] = value["options"].DeepClone();
        }
        private void Meeting(ModuleSession s, JObject value)
        {
            s.Meeting = value; s.Names.Clear();
            foreach (var p in (value["participants"] as JArray ?? new JArray()))
            {
                int? n = (int?)p["presence"]?["seatNumber"];
                string name = ((string)p["firstName"] + " " + (string)p["lastName"]).Trim();
                if (n.HasValue && name.Length > 0) s.Names[n.Value] = name;
            }
        }
        private async Task Snapshot(ModuleSession s, HttpClient client, bool lists)
        {
            // Optional/unlicensed features report their own errors without disabling microphones.
            var paths = new[] { "discussion/seats", "discussion/requests", "meeting", "meeting/scheduled-meetings", "discussion/settings", "recording/state" };
            var errors = new List<string>();
            foreach (var path in paths)
            {
                if (!lists && path == "discussion/requests") continue;
                try
                {
                    var value = await Read(client, path, s).ConfigureAwait(false);
                    lock (sync)
                    {
                        if (s != module || s.Token.IsCancellationRequested) return;
                        switch (path)
                        {
                            case "discussion/seats": s.Seats = new JArray(((JArray)value).OrderBy(x => (int)x["seatNumber"])); break;
                            case "discussion/requests": s.Requests = (JArray)value; break;
                            case "meeting": Meeting(s, (JObject)value); break;
                            case "meeting/scheduled-meetings": s.Meetings = (JArray)value; break;
                            case "discussion/settings": Settings(s, (JObject)value); break;
                            case "recording/state": s.Recording = (string)value["state"] ?? "Unknown"; break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (s.Token.IsCancellationRequested) return;
                    errors.Add(path + ": " + ex.Message);
                    lock (sync)
                    {
                        if (s != module) return;
                        if (path == "discussion/seats") s.Seats = new JArray();
                        if (path == "discussion/requests") s.Requests = new JArray();
                        if (path == "meeting") Meeting(s, new JObject());
                        if (path == "meeting/scheduled-meetings") s.Meetings = new JArray();
                        if (path == "discussion/settings") s.Settings = new JObject();
                        if (path == "recording/state") s.Recording = "Unknown";
                    }
                }
            }
            lock (sync)
            {
                if (s != module || s.Token.IsCancellationRequested) return;
                s.Refreshed = DateTime.UtcNow;
                s.Status = errors.Count == 0 ? "Connected" : string.Join("; ", errors);
                Emit(s);
            }
        }
        private async Task RefreshModule(CancellationToken token, bool force)
        {
            var s = Current(token); if (s == null) return;
            await s.Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (force || (DateTime.UtcNow - s.Refreshed).TotalSeconds >= refreshSeconds)
                    using (var client = Client(s)) await Snapshot(s, client, force).ConfigureAwait(false);
            }
            finally { s.Gate.Release(); }
        }
        private async Task ModuleEvent(CancellationToken token, JObject notification)
        {
            var s = Current(token); if (s == null) return;
            var name = (string)notification["name"];
            if ((bool?)notification["discontinuity"] == true || (string)notification["module"] == "Meeting" || (string)notification["module"] == "Room")
            { await RefreshModule(token, true).ConfigureAwait(false); return; }
            await s.Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                lock (sync)
                {
                    if (s != module || token.IsCancellationRequested) return;
                    var data = notification["data"];
                    if ((string)notification["module"] == "Discussion")
                    {
                        if (name == "RequestsChanged" && data is JArray) s.Requests = (JArray)data;
                        if (name == "SettingsChanged" && data is JObject) Settings(s, (JObject)data);
                        if (name == "SeatChanged" && data is JObject)
                        {
                            var number = (int)data["seatNumber"];
                            var rows = s.Seats.Where(x => (int)x["seatNumber"] != number).ToList(); rows.Add(data);
                            s.Seats = new JArray(rows.OrderBy(x => (int)x["seatNumber"]));
                        }
                    }
                    if ((string)notification["module"] == "Recording" && name == "StateChanged") s.Recording = (string)data?["state"] ?? "Unknown";
                    Emit(s);
                }
            }
            finally { s.Gate.Release(); }
        }
        private void Command(Func<ModuleSession, HttpClient, Task> action)
        {
            lock (sync)
            {
                ModuleSession s = moduleEnabled ? module : null;
                if (s == null || !s.Connected || s.Token.IsCancellationRequested) return;
                s.CommandTail = s.CommandTail.ContinueWith(async previous => {
                try
                {
                    await s.Gate.WaitAsync(s.Token).ConfigureAwait(false);
                    try
                    {
                        using (var client = Client(s))
                        {
                            lock (sync) { if (s != module || !s.Connected || s.Token.IsCancellationRequested) return; }
                            await action(s, client).ConfigureAwait(false);
                            await Snapshot(s, client, true).ConfigureAwait(false);
                        }
                    }
                    finally { s.Gate.Release(); }
                }
                catch (Exception ex)
                {
                    lock (sync) { if (s != module || s.Token.IsCancellationRequested) return; s.CommandStatus = "Command failed: " + ex.Message; Emit(s); }
                }
                }, TaskScheduler.Default).Unwrap();
            }
        }
        private async Task Send(ModuleSession s, HttpClient client, string method, string path, JObject body)
        {
            using (var request = new HttpRequestMessage(new HttpMethod(method), "api/" + path))
            {
                if (body != null) request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(request, s.Token).ConfigureAwait(false))
                {
                    Trace(method + " api/" + path + " HTTP " + (int)response.StatusCode);
                    if (!response.IsSuccessStatusCode && path == "recording/state")
                        lock (sync) { if (s == module && !s.Token.IsCancellationRequested) s.Recording = "Unknown"; }
                    if (!response.IsSuccessStatusCode)
                    {
                        string detail = response.ReasonPhrase;
                        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        try
                        {
                            var error = JObject.Parse(content);
                            if (error["message"] != null && error["message"].Type == JTokenType.String)
                                detail = (string)error["message"];
                        }
                        catch (Exception) { /* Non-JSON errors retain the HTTP reason. */ }
                        throw new HttpRequestException(method + " api/" + path + " HTTP " + (int)response.StatusCode + ": " + detail);
                    }
                    lock (sync) { if (s == module && !s.Token.IsCancellationRequested) s.CommandStatus = method + " " + path + " succeeded"; }
                }
            }
        }
        private void Mic(ushort number, bool on)
        {
            if (number == 0) return;
            Command((s,c) => Send(s,c,"PUT","discussion/seats/" + number, new JObject { ["microphoneOn"] = on, ["requestingToSpeak"] = false }));
        }
        public void SetSeatMic(ushort row, ushort on) { lock (sync) Mic(GetSeatNumber(row), on != 0); }
        public void TurnOffSpeaker(ushort row) { lock (sync) Mic(GetSpeakerNumber(row), false); }
        public void EnableRequest(ushort row) { lock (sync) Mic(GetRequestNumber(row), true); }
        public void Refresh()
        {
            lock (sync) { if (session != null && moduleEnabled) Initialize(module.Address, module.Key); }
        }
        public void ClearSpeakers() { Command((s,c) => Send(s,c,"DELETE","discussion/speakers",null)); }
        public void ClearRequests() { Command((s,c) => Send(s,c,"DELETE","discussion/requests",null)); }
        public void StartMeeting() { Command((s,c) => Send(s,c,"POST","meeting",new JObject { ["kind"] = "NewMeetingFromLocalTemplate" })); }
        public void StopMeeting() { Command((s,c) => Send(s,c,"DELETE","meeting",null)); }
        public void StartScheduledMeeting(ushort row)
        {
            lock (sync)
            {
                string id = GetMeetingId(row), title = GetMeetingName(row);
                if (id.Length == 0) return;
                Command((s,c) => Send(s,c,"POST","meeting",new JObject { ["kind"] = "NewMeetingFromScheduledMeeting", ["scheduledMeetingId"] = id, ["title"] = title }));
            }
        }
        public void SetRecording(ushort on) { Command((s,c) => Send(s,c,"PUT","recording/state",new JObject { ["state"] = on == 0 ? "idle" : "recording" })); }
        public void WirelessTurnOff() { Command((s,c) => Send(s,c,"POST","device/devices/actions",new JObject { ["action"] = "turnOff" })); }
        private static JObject DefaultOptions(string mode)
        {
            var options = new JObject { ["ledColorOn"] = "red", ["ledColorOff"] = "off" };
            if (mode == "directSpeak" || mode == "group") options["microphoneActivationType"] = "toggle";
            if (mode == "directSpeak") { options["speakerOverrideAllowed"] = false; options["switchOffAllowed"] = true; }
            if (mode == "request" || mode == "group") options["ledColorRequest"] = "green";
            if (mode == "request") { options["switchOffAllowed"] = true; options["cancelRequestAllowed"] = true; options["nextInLineIndication"] = true; }
            if (mode == "handsFree") options["pushToMute"] = true;
            return options;
        }
        public void ApplySettings(ushort mode, ushort openMics)
        {
            if (mode < 1 || mode > 5 || openMics < 1 || openMics > 100) return;
            Command((s,c) => {
                string id = Modes[mode - 1]; JToken options;
                if (!s.Options.TryGetValue(id, out options)) options = DefaultOptions(id);
                return Send(s,c,"PUT","discussion/settings",new JObject { ["microphoneMode"] = id, ["maximumNumberOfSpeakers"] = openMics, ["options"] = options.DeepClone() });
            });
        }
    }
}

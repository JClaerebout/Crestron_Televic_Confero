using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TelevicConfero;

// A real HTTP fixture, independent of the optional shared simulator junction.
class ModuleIntegration
{
    static readonly object sync = new object();
    static readonly List<JObject> events = new List<JObject>();
    static readonly List<string> commands = new List<string>();
    static readonly List<string> recordingWrites = new List<string>();
    static JArray seats = JArray.Parse("[{seatNumber:20,microphoneOn:true,requestingToSpeak:false,role:'delegate'},{seatNumber:3,microphoneOn:true,requestingToSpeak:false,role:'chairperson'},{seatNumber:11,microphoneOn:false,requestingToSpeak:true,role:'delegate'}]");
    static JArray speakers = new JArray(20,3), requests = new JArray(11);
    static JObject settings = JObject.Parse("{microphoneMode:'request',maximumNumberOfSpeakers:3,options:{switchOffAllowed:false,cancelRequestAllowed:true,ledColorOn:'red',ledColorRequest:'green',ledColorOff:'off',nextInLineIndication:false}}");
    static JObject meeting = new JObject();
    static string recording = "idle";
    static bool unlicensed, rejectRecording;
    static int nextId;
    static volatile ModuleFeedbackEventArgs feedback;
    static volatile MicFeedbackEventArgs micFeedback;
    static ConferoController controller;

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Wait(Func<bool> ready, string message)
    {
        for (int i=0; i<240; i++) { if (ready()) return; Thread.Sleep(50); }
        throw new Exception("Timeout: " + message + "; status=" + (feedback == null ? "none" : feedback.Status));
    }
    static void Event(string module, string name, JToken data)
    {
        events.Add(new JObject { {"id",++nextId}, {"module",module}, {"name",name}, {"data",data.DeepClone()} });
    }
    static void Serve(TcpClient socket)
    {
        try
        {
            using (socket)
            {
                var stream = socket.GetStream(); var reader = new StreamReader(stream, Encoding.UTF8);
                string first = reader.ReadLine(), line; if (first == null) return;
                string[] parts = first.Split(' '); string method = parts[0], path = parts[1].Split('?')[0];
                bool authorized = false; int length=0;
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                    if (line == "Authorization: Bearer test-key") authorized = true;
                    if (line.StartsWith("Content-Length:")) length = int.Parse(line.Substring(15).Trim());
                }
                var chars = new char[length]; int read=0;
                while (read < length) { int n = reader.Read(chars,read,length-read); if(n == 0) break; read += n; }
                var body = length == 0 ? new JObject() : JObject.Parse(new string(chars));
                if (path.Contains("notification/events")) Thread.Sleep(75);
                int status = authorized ? 200 : 401; JToken result = new JObject();
                lock(sync)
                {
                    if (authorized)
                    {
                        if (method == "GET")
                        {
                            switch(path)
                            {
                                case "/api/discussion/seats": result = seats; break;
                                case "/api/discussion/speakers": result = speakers; break;
                                case "/api/discussion/requests": result = requests; break;
                                case "/api/discussion/settings": result = settings; break;
                                case "/api/recording/state": result = new JObject { {"state",recording} }; break;
                                case "/api/meeting": result = meeting; if (!meeting.HasValues) status=412; break;
                                case "/api/meeting/scheduled-meetings":
                                    result = JArray.Parse("[{kind:'ScheduledMeeting',scheduledMeetingId:'stable-1',name:'Council'},{kind:'ScheduledMeeting',scheduledMeetingId:'stable-2',name:'Council'}]");
                                    if(unlicensed) status=405; break;
                                case "/api/notification/events":
                                    Check(parts[1].Contains("Discussion,Meeting,Room,Recording"), "Missing notification filters");
                                    long minimum = 0; string query = parts[1]; int at=query.IndexOf("minimum-id=");
                                    if (at >= 0) minimum=long.Parse(query.Substring(at+11));
                                    var next = events.FirstOrDefault(x => (long)x["id"] >= minimum);
                                    if(next == null) status=204; else result=next;
                                    break;
                                default: status=404; break;
                            }
                        }
                        else
                        {
                            commands.Add(method + " " + path);
                            status=204;
                            if(path.StartsWith("/api/discussion/seats/"))
                            {
                                Check(body["microphoneOn"] != null && body["requestingToSpeak"] != null, "Seat PUT requires both booleans");
                                int number=int.Parse(path.Substring(path.LastIndexOf('/')+1));
                                var seat=seats.First(x => (int)x["seatNumber"] == number);
                                seat["microphoneOn"]=(bool)body["microphoneOn"]; seat["requestingToSpeak"]=false;
                                speakers = new JArray(speakers.Where(x => (int)x != number));
                                requests = new JArray(requests.Where(x => (int)x != number));
                                if((bool)body["microphoneOn"]) speakers.Add(number);
                                Event("Discussion","SeatChanged",seat);
                                Event("Discussion","SpeakersChanged",speakers); Event("Discussion","RequestsChanged",requests);
                            }
                            else if(path == "/api/discussion/speakers") { speakers=new JArray(); Event("Discussion","SpeakersChanged",speakers); }
                            else if(path == "/api/discussion/requests") { requests=new JArray(); Event("Discussion","RequestsChanged",requests); }
                            else if(path == "/api/meeting")
                            {
                                if(method == "DELETE") meeting = new JObject();
                                else if(meeting.HasValues) { status=412; result=new JObject { {"message","Meeting already open"} }; }
                                else
                                {
                                    Check((string)body["kind"] == "NewMeetingFromLocalTemplate" || (string)body["scheduledMeetingId"] == "stable-1", "Incorrect meeting payload");
                                    meeting=JObject.Parse("{meetingId:'running-id',title:'Running council',participants:[{firstName:'Ada',lastName:'Lovelace',presence:{seatNumber:3}}]}");
                                }
                                if(status < 300) Event("Meeting","MeetingStateChanged",new JObject { {"meetingState",method=="DELETE" ? "noMeetingRunning":"meetingRunning"} });
                            }
                            else if(path == "/api/recording/state")
                            {
                                if(rejectRecording) status=405;
                                else { recording=(string)body["state"]; recordingWrites.Add(recording); Event("Recording","StateChanged",new JObject { {"state",recording} }); }
                            }
                            else if(path == "/api/discussion/settings") { settings=body; Event("Discussion","SettingsChanged",settings); }
                            else if(path == "/api/device/devices/actions") Check((string)body["action"] == "turnOff", "Incorrect wireless payload");
                            else status=404;
                        }
                    }
                    var bytes = Encoding.UTF8.GetBytes(status == 204 ? "" : result.ToString());
                    var header=Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Fixture\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                    stream.Write(header,0,header.Length); stream.Write(bytes,0,bytes.Length);
                }
            }
        }
        catch(IOException) { }
        catch(Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); }
    }
    static void Main()
    {
        var server=new TcpListener(IPAddress.Loopback,0); server.Start();
        Task.Run(() => { try { while(true) { var socket=server.AcceptTcpClient(); Task.Run(() => Serve(socket)); } } catch(SocketException) { } });
        string host="127.0.0.1:"+((IPEndPoint)server.LocalEndpoint).Port;
        controller=new ConferoController(); controller.Configure(2,1,1,1,10);
        controller.OnModuleFeedback += (s,e) => feedback=e;
        controller.OnFeedback += (s,e) => micFeedback=e;
        controller.Initialize(host,"test-key");
        Wait(() => feedback != null && feedback.MeetingCount==2 && feedback.SeatCount==3,"initial metadata");
        Check(controller.GetSeatNumber(1)==3 && controller.GetSeatNumber(2)==11 && controller.GetSeatNumber(3)==0,"Sorted noncontiguous seats and mic capacity");
        Check(controller.GetSpeakerNumber(1)==20 && controller.GetSpeakerNumber(2)==0 && feedback.SpeakerCount==2,"Speaker order, total count and capacity");
        Check(controller.GetRequestNumber(1)==11 && controller.GetRequestNumber(2)==0,"Queue rows");
        Check(controller.GetMeetingId(1)=="stable-1" && controller.GetMeetingId(2)=="","Meeting capacity and stable ID");
        controller.SetSeatMic(3,1); controller.StartScheduledMeeting(2);
        Thread.Sleep(200); lock(sync) Check(commands.Count==0,"Invalid rows sent commands");
        controller.TurnOffSpeaker(1); Wait(() => controller.GetSpeakerNumber(1)==3,"Turn off API seat 20 from list row 1");
        controller.EnableRequest(1); Wait(() => feedback.RequestCount==0 && controller.GetSeatMic(2)==1,"Admit queue API seat 11");
        controller.SetSeatMic(2,0); Wait(() => controller.GetSeatMic(2)==0,"Mic row 2 maps to API seat 11");
        controller.SetSeatMic(2,1); Wait(() => controller.GetSeatMic(2)==1,"Mic on confirmed feedback");
        controller.ClearSpeakers(); Wait(() => micFeedback.FirstActiveMic==0 && feedback.SpeakerCount==0,"Clear speakers");
        controller.ClearRequests();
        controller.StartScheduledMeeting(1); Wait(() => feedback.MeetingRunning==1 && controller.GetSeatName(1)=="Ada Lovelace","Scheduled start and participant names");
        controller.StopMeeting(); Wait(() => feedback.MeetingRunning==0 && controller.GetSeatName(1)=="Seat 3","Stop clears participant names");
        controller.StartMeeting(); Wait(() => feedback.MeetingRunning==1,"Open meeting start");
        controller.StartMeeting();
        Wait(() => feedback.Status.Contains("POST api/meeting HTTP 412: Meeting already open"),"Command error includes endpoint and API explanation");
        Check(micFeedback.Connected==1 && feedback.MeetingRunning==1,"Rejected duplicate start changed connection or meeting feedback");
        controller.ApplySettings(2,5); Wait(() => feedback.OpenMics==5,"Settings update");
        lock(sync) Check((bool)settings["options"]["switchOffAllowed"]==false && (bool)settings["options"]["nextInLineIndication"]==false,"Read settings options were not preserved");
        controller.ApplySettings(5,4); Wait(() => feedback.DiscussionMode==5 && feedback.OpenMics==4,"Mode change");
        lock(sync) Check((bool)settings["options"]["pushToMute"],"Hands-free default options");
        controller.SetDiscussionMode(2); Wait(() => feedback.DiscussionMode==2,"Direct mode change");
        Check(feedback.OpenMics==4,"Mode change altered open-mic limit");
        controller.SetOpenMics(6); Wait(() => feedback.OpenMics==6,"Direct open-mic change");
        Check(feedback.DiscussionMode==2,"Open-mic change altered mode");
        controller.SetRecording(1); Wait(() => feedback.RecordingActive==1,"Recording start");
        controller.SetRecording(0); Wait(() => feedback.RecordingState=="idle","Recording stop");
        lock(sync) recordingWrites.Clear();
        controller.SetRecording(1); controller.SetRecording(0);
        Wait(() => { lock(sync) return recordingWrites.Count==2 && feedback.RecordingState=="idle"; },"Rapid command ordering");
        lock(sync) Check(recordingWrites[0]=="recording" && recordingWrites[1]=="idle","Rapid commands executed out of order");
        controller.WirelessTurnOff(); Wait(() => feedback.Status.Contains("device/devices/actions succeeded"),"Wireless shutdown result");
        Thread.Sleep(300);
        Check(feedback.Status.Contains("device/devices/actions succeeded"),"Metadata refresh erased command status");
        lock(sync) rejectRecording=true;
        controller.SetRecording(1); Wait(() => feedback.Status.Contains("Command failed"),"Rejected optional command");
        Check(micFeedback.Connected==1,"Rejected command disabled mic connectivity");
        lock(sync) unlicensed=true;
        controller.Refresh(); Wait(() => feedback.MeetingCount==0 && feedback.SeatCount==3,"Unavailable meeting list");
        Check(micFeedback.Connected==1,"Unlicensed meeting feature disabled mic connectivity");
        lock(sync)
        {
            requests=new JArray(20,11); Event("Discussion","RequestsChanged",requests);
            recording="recording"; Event("Recording","StateChanged",new JObject { {"state",recording} });
        }
        Wait(() => feedback.RequestCount==2 && feedback.RecordingActive==1,"External queue and recording notifications");
        Check(controller.GetRequestNumber(2)==0,"Queue capacity after notification");
        controller.Initialize(host,"bad-key"); Wait(() => micFeedback.Connected==0 && feedback.SeatCount==0 && feedback.RequestCount==0,"Authentication clears feedback");
        controller.Initialize(host,"test-key"); Wait(() => feedback.SeatCount==3 && micFeedback.Connected==1,"Reinitialize full module");
        Console.WriteLine("PASS: capacities, noncontiguous seat mapping, ordered lists, all commands, names, settings options, notifications, feature errors, authentication and reinitialization");
        server.Stop();
    }
}

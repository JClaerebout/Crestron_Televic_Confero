using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TelevicConfero;
class Integration
{
    static volatile string speakers = "[]";
    static volatile bool fail;
    static volatile MicFeedbackEventArgs feedback;
    static int eventId;
    static int resumed;
    static int idlePoll;
    static int idleStarted;
    static int unexpectedDisconnect;
    static volatile bool watchConnection;
    static int startupPoll = 1;
    static int startupCompleted;
    static void WaitFor(ushort connected, ushort first, ushort last)
    {
        for (int i = 0; i < 160; i++)
        {
            var f = feedback;
            if (f != null && f.Connected == connected && f.FirstActiveMic == first && f.LastActiveMic == last) return;
            Thread.Sleep(50);
        }
        throw new Exception("Feedback timeout: " + connected + "/" + first + "/" + last);
    }
    static void Serve(TcpClient socket)
    {
        using (socket)
        {
            var stream = socket.GetStream();
            var reader = new StreamReader(stream);
            string request = reader.ReadLine(), line;
            bool authorized = false;
            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                if (line == "Authorization: Bearer test-key") authorized = true;
            bool events = request.Contains("notification/events");
            if (events && Interlocked.Exchange(ref startupPoll, 0) == 1)
            {
                Thread.Sleep(3000);
                var idle = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                Interlocked.Exchange(ref startupCompleted, 1);
                stream.Write(idle, 0, idle.Length);
                return;
            }
            if (events && Interlocked.Exchange(ref idlePoll, 0) == 1)
            {
                Interlocked.Exchange(ref idleStarted, 1);
                // Exercise the production 40-second HTTP timeout, without shortening it.
                Thread.Sleep(42000);
                return;
            }
            if (events) Thread.Sleep(100);
            string status = !authorized ? "401 Unauthorized" : fail ? "503 Unavailable" : "200 OK";
            string body = speakers;
            if (events)
            {
                if (request.Contains("minimum-id=")) Interlocked.Increment(ref resumed);
                body = "{\"id\":" + Interlocked.Increment(ref eventId) + ",\"module\":\"Discussion\",\"name\":\"SpeakersChanged\",\"data\":" + speakers + "}";
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
    static void Main()
    {
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        Task.Run(() => { while (true) { var s = server.AcceptTcpClient(); Task.Run(() => Serve(s)); } });
        var controller = new ConferoController();
        controller.OnFeedback += (s, e) => {
            feedback = e;
            if (watchConnection && e.Connected == 0) Interlocked.Increment(ref unexpectedDisconnect);
        };
        var host = "127.0.0.1:" + ((IPEndPoint)server.LocalEndpoint).Port;
        controller.Initialize(host, "test-key");
        WaitFor(1, 0, 0);
        if (startupCompleted != 0) throw new Exception("Initialization waited for idle event response");
        Console.WriteLine("PASS: connected feedback goes high before any event response in an idle room");
        speakers = "[7]"; WaitFor(1, 7, 7);
        speakers = "[7,2,9]"; WaitFor(1, 7, 9);
        speakers = "[2,9]"; WaitFor(1, 2, 9);
        speakers = "[9,7]"; WaitFor(1, 9, 7);
        speakers = "[]"; WaitFor(1, 0, 0);
        fail = true; WaitFor(0, 0, 0);
        speakers = "[4,8]"; fail = false; WaitFor(1, 4, 8);
        controller.Initialize(host, "bad-key"); WaitFor(0, 0, 0);
        Thread.Sleep(400);
        if (feedback.Connected != 0) throw new Exception("Old subscription leaked feedback");
        controller.Initialize(host, "test-key"); WaitFor(1, 4, 8);
        if (resumed == 0) throw new Exception("No minimum-id sent");
        watchConnection = true;
        Interlocked.Exchange(ref idlePoll, 1);
        for (int i = 0; i < 100 && idleStarted == 0; i++) Thread.Sleep(50);
        if (idleStarted == 0) throw new Exception("Idle poll was not started");
        speakers = "[11,3]";
        Thread.Sleep(41000);
        WaitFor(1, 11, 3);
        if (unexpectedDisconnect != 0) throw new Exception("Idle poll timeout disconnected healthy API");
        watchConnection = false;
        speakers = "[3]"; WaitFor(1, 3, 3);
        Console.WriteLine("PASS: idle event timeout keeps connection, refreshes snapshot, and resumes events");
        Console.WriteLine("PASS: empty, single, ordered speakers, removals, reactivation, disconnect, reconnect, authentication, reinitialize, event cursor");
        server.Stop();
    }
}

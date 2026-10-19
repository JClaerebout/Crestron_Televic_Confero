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
        controller.OnFeedback += (s, e) => feedback = e;
        var host = "127.0.0.1:" + ((IPEndPoint)server.LocalEndpoint).Port;
        controller.Initialize(host, "test-key");
        WaitFor(1, 0, 0);
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
        Console.WriteLine("PASS: empty, single, ordered speakers, removals, reactivation, disconnect, reconnect, authentication, reinitialize, event cursor");
        server.Stop();
    }
}

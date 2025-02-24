using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Server
{
    TcpClient[] clients = new TcpClient[20];
    int n = 0;

    public Server(int port)
    {
        StartServer(port);
    }

    void StartServer(int port)
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        Log("Server started on port: " + port.ToString());
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = tokenSource.Token;

        while (!cancellationToken.IsCancellationRequested)
        {

            TcpClient client = listener.AcceptTcpClient();
            Log("Client connected!");

            clients[n] = client;
            ReceivePackets(client, n);
            n++;
        }
    }

    Task ReceivePackets(TcpClient client, int clientId)
    {
        return Task.Run(() =>
        {
            Byte[] bytes = new Byte[256];
            string? data = null;

            data = null;
            NetworkStream stream = client.GetStream();

            int i;
            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                data = Encoding.ASCII.GetString(bytes, 0, i);
                Log("Received: " + clientId.ToString() + " " + data);
            }
        });
    }

    void Log(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("[{0}] ", DateTime.Now);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(msg);
    }
}

class Program 
{
    static void Main(string[] args)
    {
        Server server = new Server(13000);
    }
}
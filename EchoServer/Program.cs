using System.Net;
using System.Net.Sockets;
using System.Text;

enum PacketType
{
    Text
}

class Server
{
    const int MAX_CLIENTS = 3;
    TcpClient[] clients = new TcpClient[MAX_CLIENTS + 1];
    bool[] used = new bool[MAX_CLIENTS + 1];

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

            int id = GetClientId();
            if(id != -1)
            {
                clients[id] = client;
                ReceivePackets(client, id);
            }
            else
            {
                client.Close();
            }
        }
    }

    Task ReceivePackets(TcpClient client, int id)
    {
        return Task.Run(() =>
        {
            Byte[] bytes = new Byte[256];
            string? data = null;

            NetworkStream stream = client.GetStream();

            int i;
            try
            {
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    data = Encoding.ASCII.GetString(bytes, 0, i);
                    Log("Received: " + id.ToString() + " " + data);
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
                DisconnectClient(id);
            }
        });
    }

    int GetClientId()
    {
        for (int i = 0; i < MAX_CLIENTS; i++)
            if (!used[i])
            {
                used[i] = true;
                return i;
            }
        return -1;
    }

    void DisconnectClient(int id)
    {
        Log("Client " + id + " disconnected");
        clients[id].Close();
        used[id] = false;
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
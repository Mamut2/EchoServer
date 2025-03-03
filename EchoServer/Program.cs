using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

public enum PacketType
{
    Empty = 0,
    Text = 1,
    Disconnect = 2,
    Connect = 3,
    AssignId = 4
}

class Client
{
    public Client() 
    {
        tcp = new TcpClient();
        username = "";
    }

    public TcpClient tcp;
    public string username;
    public bool Connected { get { return tcp.Connected; } }
}

class Server
{
    const int MAX_CLIENTS = 5;
    Client[] clients = new Client[MAX_CLIENTS];
    int port;
    CancellationTokenSource tokenSource;

    public Server(int port, CancellationTokenSource tokenSource)
    {
        for(int i = 0; i < MAX_CLIENTS; i++)
            clients[i] = new Client();

        this.port = port;
        this.tokenSource = tokenSource;
    }

    public void StartServer()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        Log("Server started on port: " + port.ToString());
        CancellationToken cancellationToken = tokenSource.Token;

        while (true)
        {

            TcpClient client = listener.AcceptTcpClient();

            int id = GetClientId();
            if (id != -1)
            {
                clients[id].tcp = client;
                _ = Task.Run(() => ReceivePackets(client, id));
            }
            else
            {
                client.Close();
            }
        }
    }

    
    void ReceivePackets(TcpClient client, int id)
    {

        Byte[] bytes = new Byte[256];
        string? data = null;

        NetworkStream stream = client.GetStream();

        int i;
        try
        {
            while (client.Connected && (i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                PacketType packetType = PacketType.Empty;
                data = Encoding.ASCII.GetString(bytes, 0, i);

                int fromId;
                string[] p = data.Split('|');
                packetType = (PacketType)int.Parse(p[0]);
                fromId = int.Parse(p[1]);
                data = "";
                for (int k = 2; k < p.Length; k++) data += p[k];

                switch (packetType)
                {
                    case PacketType.Text:
                        Log(clients[id].username + ": " + data);
                        for(int j = 0; j < MAX_CLIENTS; j++)
                            if (clients[j].Connected)
                                SendPacket(j, id, data, PacketType.Text).Wait();
                        break;
                    case PacketType.Disconnect:
                        DisconnectClient(id);
                        break;
                    case PacketType.Connect:
                        SendPacket(id, id, null, PacketType.AssignId).Wait();
                        clients[id].username = data;
                        Log(data + " connected!");
                        for (int j = 0; j < MAX_CLIENTS; j++)
                            if (clients[j].Connected)
                                SendPacket(j, id, data, PacketType.Connect).Wait();
                        for (int j = 0; j < MAX_CLIENTS; j++)
                            if (clients[j].Connected && j != id)
                                SendPacket(id, j, clients[j].username, PacketType.Connect).Wait();
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Log(e.Message);
            DisconnectClient(id);
        }
    }

    Task SendPacket(int clientIdTo, int clientIdFrom, string? data, PacketType packetType)
    {
        return Task.Run(() =>
        {
            if (clients[clientIdTo].Connected)
            {
                NetworkStream stream = clients[clientIdTo].tcp.GetStream();
                string pt = ((int)packetType).ToString();

                data = pt + '|' + clientIdFrom + '|' + data;

                Byte[] bytes = Encoding.ASCII.GetBytes(data);

                stream.Write(bytes, 0, bytes.Length);
            }
        });
    }

    int GetClientId()
    {
        for (int i = 0; i < MAX_CLIENTS; i++)
        {
            if (clients[i] == null) clients[i] = new Client();
            if (clients[i].tcp == null || !clients[i].tcp.Connected)
                return i;
        }
        return -1;
    }

    void DisconnectClient(int id)
    {
        if (clients[id].Connected)
        {
            Log(clients[id].username + " disconnected");
            SendPacket(id, -1, null, PacketType.Disconnect).Wait();
            clients[id].tcp.Close();
        }
    }

    public void CloseServer()
    {
        for (int i = 0; i < MAX_CLIENTS; i++) DisconnectClient(i);
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
    static void Main()
    {
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        Server server = new Server(13000, tokenSource);
        Console.ForegroundColor = ConsoleColor.White;
        ConsoleClosingHandler.Initialize();
        ConsoleClosingHandler.OnClosing += (sender, e) =>
        {
            server.CloseServer();
        };

        Task.Run(() =>
        {
            server.StartServer();
        });

        while (true)
        {
            string? input = Console.ReadLine();

            if (input == "exit")
            {
                server.CloseServer();
                break;
            }
        }
    }
}
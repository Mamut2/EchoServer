using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Server
{
    public enum PacketType { UserInfo = 0, Message = 1, Disconnect = 2 }

    class ClientInfo
    {
        public string? username;
        public TcpClient? client;
        public NetworkStream? stream;
        public byte[]? avatar;
    }

    static ConcurrentDictionary<string, ClientInfo> clients = new ConcurrentDictionary<string, ClientInfo>();

    static void Main(string[] args)
    {
        const int port = 13000;
        TcpListener listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Log($"Server started on port: {port}");

        while(true)
        {
            TcpClient client = listener.AcceptTcpClient();
            ThreadPool.QueueUserWorkItem(HandleClient, client);
        }
    }

    static void HandleClient(object? obj)
    {
        TcpClient client = (TcpClient)obj!;
        string clientId = Guid.NewGuid().ToString();

        try
        {
            // Get user info
            NetworkStream stream = client.GetStream();

            var (type, data) = ReadPacket(stream);
            if (type != PacketType.UserInfo) return;

            ClientInfo info;
            using (MemoryStream ms = new MemoryStream(data!))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                string username = reader.ReadString();
                byte[] avatar = reader.ReadBytes(reader.ReadInt32());

                info = new ClientInfo
                {
                    client = client,
                    stream = stream,
                    username = username,
                    avatar = avatar
                };

                clients.TryAdd(clientId, info);
                Log($"{username} connected!");

                Thread monitorThread = new Thread(() => MonitorClientConnection(clientId));
                monitorThread.Start();

                BroadcastUserInfo(clientId, username, avatar);
                SendUsersInfo(clientId);
            }

            // Receive packets
            while(true)
            {
                (type, data) = ReadPacket(stream);
                switch(type)
                {
                    case PacketType.Message:
                        string msg = Encoding.UTF8.GetString(data!);
                        Log($"[{clients[clientId].username}] {msg}");
                        BroadcastMessage(clientId, msg);
                        break;
                }
            }
        }
        catch
        {

        }
        finally
        {
            Disconnect(clientId);
        }
    }

    static void BroadcastUserInfo(string senderId, string username, byte[] avatar)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write(senderId);
            writer.Write(username);
            writer.Write(avatar.Length);
            writer.Write(avatar);

            SendToAll(senderId, PacketType.UserInfo, ms.ToArray());
        }
    }

    static void BroadcastMessage(string senderId, string msg)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write(senderId);
            writer.Write(msg);

            SendToAll(senderId, PacketType.Message, ms.ToArray());
        }
    }

    static void BroadcastDisconnect(string senderId)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write(senderId);

            SendToAll(senderId, PacketType.Disconnect, ms.ToArray());
        }
    }

    static void SendUsersInfo(string receiverId)
    {
        foreach (var client in clients)
        {
            if (client.Key == receiverId) continue;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(client.Key);
                writer.Write(client.Value.username!);
                writer.Write(client.Value.avatar!.Length);
                writer.Write(client.Value.avatar!);

                SendTo(receiverId, PacketType.UserInfo, ms.ToArray());
            }
        }
    }

    static void SendTo(string receiverId, PacketType type, byte[] data)
    {
        byte[] header = new byte[8];
        BitConverter.GetBytes((int)type).CopyTo(header, 0);
        BitConverter.GetBytes(data.Length).CopyTo(header, 4);

        try
        {
            clients[receiverId].stream!.Write(header, 0, 8);
            clients[receiverId].stream!.Write(data, 0, data.Length);
        }
        catch { Disconnect(receiverId); }
    }

    static void SendToAll(string senderId, PacketType type, byte[] data)
    {
        byte[] header = new byte[8];
        BitConverter.GetBytes((int)type).CopyTo(header, 0);
        BitConverter.GetBytes(data.Length).CopyTo(header, 4);

        foreach(var client in clients)
        {
            try
            {
                client.Value.stream!.Write(header, 0, 8);
                client.Value.stream!.Write(data, 0, data.Length);
            }
            catch { Disconnect(client.Key); }
        }
    }

    static (PacketType? type, byte[]? data) ReadPacket(NetworkStream stream)
    {
        try
        {
            byte[] header = new byte[8];
            int bytesRead = stream.Read(header, 0, 8);
            if (bytesRead != 8) return (0, null);

            int type = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);

            byte[] data = new byte[length];
            int totalRead = 0;
            while(totalRead < length)
            {
                int read = stream.Read(data, totalRead, length - totalRead);
                if (read == 0) return (0, null);
                totalRead += read;
            }

            return ((PacketType)type, data);
        }
        catch
        {
            return (0, null);
        }
    }

    static void MonitorClientConnection(string clientId)
    {
        while (true)
        {
            try
            {
                if (clients[clientId].client!.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[1];
                    if (clients[clientId].client!.Client.Receive(buffer, SocketFlags.Peek) == 0)
                    {
                        Disconnect(clientId);
                        break;
                    }
                }

                Thread.Sleep(1000);
            }
            catch
            {
                break;
            }
        }
    }

    static void Disconnect(string clientId)
    {
        if (!clients.ContainsKey(clientId)) return;

        Log($"{clients[clientId].username} disconnected!");
        clients[clientId].client.Close();
        clients.TryRemove(clientId, out _);
        BroadcastDisconnect(clientId);
    }

    static void Log(string msg)
    {
        Task.Run(() =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"[{DateTime.Now}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(msg);
        });
    }
}

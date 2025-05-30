using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Room
{
    public string Name { get; set; }
    public List<TcpClient> Clients { get; set; } = new List<TcpClient>();
}

class Server
{
    private TcpListener listener;
    private List<TcpClient> allClients = new List<TcpClient>();
    private List<Room> rooms = new List<Room>();
    private bool running = false;

    public void Start(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        running = true;

        Console.WriteLine("서버 시작됨. 포트: " + port);

        Thread acceptThread = new Thread(AcceptClients);
        acceptThread.Start();
    }

    private void AcceptClients()
    {
        while (running)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine("클라이언트 연결됨: " + client.Client.RemoteEndPoint);
                lock (allClients)
                {
                    allClients.Add(client);
                }

                Thread thread = new Thread(() => HandleClient(client));
                thread.Start();
            }
            catch (SocketException)
            {
                break;
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        Room joinedRoom = null;

        while (running)
        {
            try
            {
                byte[] buffer = new byte[1024];
                int length = stream.Read(buffer, 0, buffer.Length);
                if (length == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, length);
                Console.WriteLine("수신: " + message);

                if (message.StartsWith("CREATE_ROOM:"))
                {
                    string roomName = message.Substring("CREATE_ROOM:".Length).Trim();
                    lock (rooms)
                    {
                        if (!rooms.Exists(r => r.Name == roomName))
                        {
                            Room newRoom = new Room { Name = roomName };
                            newRoom.Clients.Add(client);
                            rooms.Add(newRoom);
                            joinedRoom = newRoom;
                            Console.WriteLine($"방 생성: {roomName}");
                            BroadcastRoomList();
                        }
                    }
                }
                else if (message.StartsWith("JOIN_ROOM:"))
                {
                    string roomName = message.Substring("JOIN_ROOM:".Length).Trim();
                    lock (rooms)
                    {
                        Room room = rooms.Find(r => r.Name == roomName);
                        if (room != null && !room.Clients.Contains(client))
                        {
                            room.Clients.Add(client);
                            joinedRoom = room;
                            Console.WriteLine($"클라이언트가 {roomName} 방에 참가함.");
                            BroadcastToRoom(room, "USER_JOINED:" + roomName);
                        }
                    }
                }
                else if (message.StartsWith("REQUEST_ROOM_LIST"))
                {
                    SendRoomListToClient(client);
                }
                else if (message.StartsWith("READY"))
                {
                    if (joinedRoom != null)
                    {
                        BroadcastToRoom(joinedRoom, "READY_RECEIVED");
                        // 필요 시 조건 체크 후 게임 시작 신호 전송 가능
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("에러: " + e.Message);
                break;
            }
        }

        // 클라이언트 연결 해제 처리
        if (joinedRoom != null)
        {
            lock (rooms)
            {
                joinedRoom.Clients.Remove(client);
                if (joinedRoom.Clients.Count == 0)
                {
                    rooms.Remove(joinedRoom);
                    BroadcastRoomList();
                    Console.WriteLine($"방 삭제됨: {joinedRoom.Name}");
                }
            }
        }

        lock (allClients)
        {
            allClients.Remove(client);
        }

        client.Close();
        Console.WriteLine("클라이언트 연결 종료");
    }

    //요청한 특정 클라이언트에게만 방 목록 전송
    private void SendRoomListToClient(TcpClient client)
    {
        string roomListMessage;
        lock (rooms)
        {
            List<string> roomNames = new List<string>();
            foreach (var room in rooms)
            {
                roomNames.Add(room.Name);
            }
            roomListMessage = "ROOM_LIST:" + string.Join(",", roomNames);
        }

        byte[] data = Encoding.UTF8.GetBytes(roomListMessage);

        try
        {
            NetworkStream stream = client.GetStream();
            stream.Write(data, 0, data.Length);
        }
        catch
        {
            // 전송 실패
        }
    }

    //모든 클라이언트에게 현재 방 목록을 "ROOM_LIST:<방1>,<방2>,..." 형식으로 전송
    private void BroadcastRoomList()
    {
        string roomListMessage;
        lock (rooms)
        {
            List<string> roomNames = new List<string>();
            foreach (var room in rooms)
            {
                roomNames.Add(room.Name);
            }
            roomListMessage = "ROOM_LIST:" + string.Join(",", roomNames);
        }

        byte[] data = Encoding.UTF8.GetBytes(roomListMessage);

        lock (allClients)
        {
            foreach (TcpClient c in allClients)
            {
                try
                {
                    NetworkStream stream = c.GetStream();
                    stream.Write(data, 0, data.Length);
                }
                catch
                {
                    // 전송 실패
                }
            }
        }
    }

    //특정 방의 모든 참가자에게 메시지 전송
    private void BroadcastToRoom(Room room, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        foreach (TcpClient c in room.Clients)
        {
            try
            {
                NetworkStream stream = c.GetStream();
                stream.Write(data, 0, data.Length);
            }
            catch
            {
                // 전송 실패
            }
        }
    }

    public void Stop()
    {
        running = false;
        listener.Stop();

        lock (allClients)
        {
            foreach (var client in allClients)
            {
                client.Close();
            }
            allClients.Clear();
        }

        Console.WriteLine("서버 중지됨");
    }
}

// Server.cs (C# 7.3 호환 버전)
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace InRang
{
    public class Server
    {
        private TcpListener listener;
        private Dictionary<int, TcpClient> clients = new Dictionary<int, TcpClient>();
        private Dictionary<int, string> playerNames = new Dictionary<int, string>();
        private Dictionary<int, StreamWriter> writers = new Dictionary<int, StreamWriter>();
        private Dictionary<int, bool> readyStatus = new Dictionary<int, bool>();
        private int clientIdCounter = 0;
        private List<string> roles = new List<string> { "시민", "점쟁이", "사냥꾼", "영매", "네코마타", "인랑", "광인", "요호" };

        public void Start()
        {
            listener = new TcpListener(IPAddress.Any, 9000);
            listener.Start();
            Console.WriteLine("서버 시작됨");

            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.Start();
        }

        private void AcceptClients()
        {
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                int clientId = clientIdCounter++;
                clients[clientId] = client;
                readyStatus[clientId] = false;

                NetworkStream ns = client.GetStream();
                StreamReader reader = new StreamReader(ns, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };
                writers[clientId] = writer;

                writer.WriteLine("ID:" + clientId);
                Console.WriteLine("클라이언트 " + clientId + " 연결됨");

                Thread receiveThread = new Thread(() => Receive(clientId, reader));
                receiveThread.Start();
            }
        }

        private void Receive(int id, StreamReader reader)
        {
            try
            {
                while (true)
                {
                    string msg = reader.ReadLine();
                    if (msg == null) break;

                    Console.WriteLine("[수신:" + id + "] " + msg);

                    if (msg.StartsWith("NAME:"))
                    {
                        string name = msg.Substring(5);
                        playerNames[id] = name;
                    }
                    else if (msg == "READY")
                    {
                        readyStatus[id] = true;
                        CheckAllReady();
                    }
                    else if (msg.StartsWith("CHAT:"))
                    {
                        string content = msg.Substring(5);
                        string senderName = playerNames.ContainsKey(id) ? playerNames[id] : "Player" + id;
                        Broadcast("CHAT:" + senderName + ": " + content);
                    }
                    else if (msg.StartsWith("ACTION:"))
                    {
                        string target = msg.Substring(7);
                        Broadcast("VOTE_RESULT:" + target + "이(가) 선택되었습니다.");
                    }
                    else if (msg == "TIME_UP")
                    {
                        Broadcast("CHAT:[시스템] 시간 종료됨");
                    }
                }
            }
            catch
            {
                Console.WriteLine("클라이언트 " + id + " 연결 종료됨");
            }
        }

        private void CheckAllReady()
        {
            foreach (bool ready in readyStatus.Values)
            {
                if (!ready) return;
            }

            Console.WriteLine("모든 인원 준비 완료. 게임 시작!");

            string playerList = string.Join(",", playerNames.Values);
            Broadcast("PLAYER_LIST:" + playerList);

            Random rnd = new Random();
            List<string> shuffledRoles = new List<string>(roles);

            while (shuffledRoles.Count < playerNames.Count)
            {
                shuffledRoles.Add("시민");
            }

            for (int i = 0; i < shuffledRoles.Count; i++)
            {
                int j = rnd.Next(i, shuffledRoles.Count);
                string temp = shuffledRoles[i];
                shuffledRoles[i] = shuffledRoles[j];
                shuffledRoles[j] = temp;
            }

            int index = 0;
            foreach (KeyValuePair<int, string> kvp in playerNames)
            {
                int id = kvp.Key;
                if (writers.ContainsKey(id))
                {
                    writers[id].WriteLine("ROLE:" + shuffledRoles[index++]);
                }
            }

            Broadcast("START_PHASE:Day");
        }

        private void Broadcast(string msg)
        {
            foreach (StreamWriter writer in writers.Values)
            {
                try { writer.WriteLine(msg); } catch { }
            }
        }
    }
}

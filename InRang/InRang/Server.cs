using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;

namespace InRang
{
    public class Server
    {
        private Dictionary<int, string> clientRooms = new Dictionary<int, string>();

        private Dictionary<string, System.Timers.Timer> roomTimers = new Dictionary<string, System.Timers.Timer>();

        private List<string> assignedRoles = new List<string>();    // 직업 배정시 이용

        private Dictionary<string, List<string>> chatLogs = new Dictionary<string, List<string>>();



        // HandleReady
        private void HandleReady(int playerId)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                var room = rooms[roomName];
                var player = room.GetPlayer(playerId);
                if (player == null) return;

                player.IsReady = true;

                Console.WriteLine($"플레이어 {playerId} 준비 완료.");

                CheckAllGameReady(roomName);
            }
        }

        private void CheckAllGameReady(string roomName)
        {
            var room = rooms[roomName];
            foreach (var player in room.Players)
            {
                if (!player.IsAI && !player.IsReady)
                {
                    return;
                }
            }
            Console.WriteLine("모든 플레이어 준비 완료.");

        }

        //private void StartGame(string roomName)
        //{
        //    var room = rooms[roomName];
        //    // 역할 배정
        //    List<string> roleList = new List<string>(roles);
        //    var rand = new Random();

        //    foreach (var player in room.Players)
        //    {
        //        int idx = rand.Next(roleList.Count);
        //        player.Role = roleList[idx];
        //        roleList.RemoveAt(idx);
        //    }

        //    //게임 시작
        //    BroadcastToRoom(roomName, "START_GAME");

        //    //클라이언트를 MultiPlayGameForm에서 game start로 넘어가도록 할 수 있음
        //    Console.WriteLine("게임이 " + roomName + "에서 시작되었습니다.");

        //    //추가로 이 후의 game flow, vote, action, death, game over 등을 이 내부에서 처리해야합니다.
        //}


        private TcpListener listener;

        private Dictionary<int, TcpClient> clients = new Dictionary<int, TcpClient>();
        private Dictionary<int, StreamWriter> writers = new Dictionary<int, StreamWriter>();

        private int clientIdCounter = 0;

        // 방 관리
        private Dictionary<string, GameRoom> rooms = new Dictionary<string, GameRoom>();

        // IP 연결 정보
        private HashSet<string> connectedIPs = new HashSet<string>();

        private List<string> roles = new List<string> { "시민", "점쟁이", "사냥꾼", "영매", "네코마타", "인랑", "광인", "요호" };

        private int port = 9000;

        public void Start(int port = 9000)
        {
            this.port = port;

            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"서버 시작됨 (port {port})");

            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        private void AcceptClients()
        {
            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();

                    // IP 확인
                    var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();


                    int clientId = clientIdCounter++;
                    clients[clientId] = client;

                    NetworkStream ns = client.GetStream();
                    StreamReader reader = new StreamReader(ns, Encoding.UTF8);
                    StreamWriter writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };
                    writers[clientId] = writer;

                    writer.WriteLine("ID:" + clientId);
                    Console.WriteLine("클라이언트를 연결되었습니다.");

                    Thread receiveThread = new Thread(() => Receive(clientId, reader, ip)); // ip 전달
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("클라이언트를 연결 오류: " + ex.Message);
                }
            }
        }

        private void Receive(int id, StreamReader reader, string ip)
        {
            try
            {
                while (true)
                {
                    string msg = reader.ReadLine();

                    if (msg == null) break;

                    Console.WriteLine("[수신 " + id + "] " + msg);

                    if (msg.StartsWith("CREATE_ROOM:"))
                    {
                        string roomName = msg.Substring(12).Trim();
                        CreateRoom(roomName, id);
                    }
                    else if (msg.StartsWith("JOIN_ROOM:"))
                    {
                        string roomName = msg.Substring(10).Trim();
                        JoinRoom(roomName, id);
                    }
                    else if (msg == "REQUEST_ROOM_LIST")
                    {
                        SendRoomList(id);
                    }
                    else if (msg.StartsWith("NAME:"))
                    {
                        string name = msg.Substring(6);
                        SetPlayerName(id, name);
                    }
                    else if (msg == "REQUEST_PLAYER_LIST")
                    {
                        if (clientRooms.ContainsKey(id))
                        {
                            string roomName = clientRooms[id];
                            SendPlayerList(roomName);
                        }
                    }

                    else if (msg == "READY")
                    {
                        SetPlayerReady(id); // 게임 시작까지 이어지는 올바른 흐름
                    }
                    else if (msg == "LEAVE_ROOM")
                    {
                        LeaveRoom(id);
                    }
                    else if (msg.StartsWith("CHAT:"))
                    {
                        HandleChat(id, msg.Substring(5));

                    }
                    else if (msg.StartsWith("ACTION:"))
                    {
                        HandleAction(id, msg.Substring(7));

                    }
                    else if (msg == "TIME_UP")
                    {
                        HandleTimeUp(id);
                    }
                    else if (msg.StartsWith("GAME_READY"))
                    {
                        if (clientRooms.ContainsKey(id))
                        {
                            string roomName = clientRooms[id];
                            SendPlayerList(roomName);

                            Thread.Sleep(2000);

                          
                        }
                        else
                        {
                            MessageBox.Show("게임 시작 오류");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("클라이언트를 연결 중 오류: " + ex.Message);
            }
            finally
            {
                DisconnectClient(id);
                connectedIPs.Remove(ip);
            }
        }
        //private void HandleGameStartReady(int playerId)
        //{
        //    if (clientRooms.ContainsKey(playerId))
        //    {
        //        string roomName = clientRooms[playerId];
        //        var room = rooms[roomName];
        //        var player = room.GetPlayer(playerId);
        //        if (player == null) return;

        //        player.GameReady = true; // Players 클래스에 GameReady 속성 추가 필요

        //        Console.WriteLine($"플레이어 {playerId}({player.Name}) 게임 시작 준비 완료");

        //        CheckAllGameStartReady(roomName);
        //    }
        //}

        //private void CheckAllGameStartReady(string roomName)
        //{
        //    if (!rooms.ContainsKey(roomName))
        //        return;

        //    GameRoom room = rooms[roomName];

        //    // 인간 플레이어들이 모두 게임 시작 준비 완료했는지 확인
        //    foreach (Players player in room.Players)
        //    {
        //        if (!player.IsAI && !player.GameReady)
        //        {
        //            Console.WriteLine($"플레이어 {player.Name}이(가) 아직 게임 시작 준비 중");
        //            return;
        //        }
        //    }

        //    Console.WriteLine("모든 플레이어가 게임 시작 준비 완료! 첫 번째 게임 페이즈 시작!");

        //    // 짧은 지연 후 첫 번째 게임 페이즈 시작
        //    Thread.Sleep(1000);
        //    StartDayPhase(roomName);
        //}
        private void HandleGameReady(int playerId)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);

                if (player != null)
                {
                    player.IsReady = true;
                    Console.WriteLine($"플레이어 {playerId}({player.Name}) 게임 준비 완료");

                    // 모든 플레이어가 준비되었는지 확인
                    CheckAllGameReady(roomName);
                }
            }
        }

        //private void CheckAllGameReady(string roomName)
        //{
        //    if (!rooms.ContainsKey(roomName))
        //        return;

        //    GameRoom room = rooms[roomName];

        //    // 인간 플레이어들이 모두 게임 준비 완료했는지 확인
        //    foreach (Players player in room.Players)
        //    {
        //        if (!player.IsAI && !player.IsReady)
        //        {
        //            Console.WriteLine($"플레이어 {player.Name}이(가) 아직 게임 준비 중");
        //            return;
        //        }
        //    }

        //    Console.WriteLine("모든 플레이어가 게임 준비 완료! 게임 시작!");

        //    // 짧은 지연 후 게임 시작
        //    Thread.Sleep(1000);
        //    StartGame(roomName);
        //}

        private void CreateRoom(string roomName, int hostId)
        {
            roomName = roomName.Replace(":", "").Trim();

            if (rooms.ContainsKey(roomName))
            {
                SendToClient(hostId, "ERROR:방 이름이 이미 존재합니다.");
                return;
            }

            GameRoom room = new GameRoom(roomName, hostId);
            rooms[roomName] = room;
            clientRooms[hostId] = roomName;

            // AI 플레이어들 추가 (기본적으로 준비 상태)
            for (int i = 0; i < GameSettings.AICount; i++)
            {
                Players aiPlayer = new Players
                {
                    Id = -1 - i, // AI는 음수 ID 사용
                    Name = "AI_" + (i + 1),
                    IsAI = true,
                    IsReady = true
                };
                room.AddPlayer(aiPlayer);
            }

            // 호스트 추가
            Players hostPlayer = new Players
            {
                Id = hostId,
                Name = "Host",
                IsAI = false,
                IsReady = false
            };
            room.AddPlayer(hostPlayer);

            Console.WriteLine("방 생성됨: " + roomName + " (호스트: " + hostId + ")");

            Console.WriteLine("방 생성됨. 현재 방 목록: " + string.Join(", ", rooms.Keys));

            SendToClient(hostId, "ROOM_CREATED:" + roomName);
            SendToClient(hostId, "ROOM_JOINED:" + roomName);
            SendPlayerList(roomName);

            BroadcastRoomListToAll();
        }

        private void JoinRoom(string roomName, int playerId)
        {
            roomName = roomName.Replace(":", "").Trim();

            Console.WriteLine($"방 참가 시도: {roomName}, 플레이어: {playerId}");
            Console.WriteLine($"현재 존재하는 방들: {string.Join(", ", rooms.Keys)}");

            if (!rooms.ContainsKey(roomName))
            {
                SendToClient(playerId, "ERROR:존재하지 않는 방입니다.");

                return;
            }

            GameRoom room = rooms[roomName];
            if (room.GetHumanPlayerCount() >= GameSettings.PlayerCount - GameSettings.AICount)
            {
                SendToClient(playerId, "ROOM_FULL");
                return;
            }

            Players player = new Players
            {
                Id = playerId,
                Name = "Player_" + playerId,
                IsAI = false,
                IsReady = false
            };

            room.AddPlayer(player);
            clientRooms[playerId] = roomName;

            Console.WriteLine("플레이어 " + playerId + "가 방 " + roomName + "에 참가함");
            SendToClient(playerId, "ROOM_JOINED:" + roomName);

            // 다른 플레이어들에게 새 플레이어 입장 알림
            BroadcastToRoom(roomName, "PLAYER_JOINED:" + player.Name);
            SendPlayerList(roomName);
        }

        private void LeaveRoom(int playerId)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);

                if (player != null)
                {
                    string playerName = player.Name;
                    room.RemovePlayer(playerId);
                    clientRooms.Remove(playerId);

                    // 다른 플레이어들에게 플레이어 퇴장 알림
                    BroadcastToRoom(roomName, "PLAYER_LEFT:" + playerName);
                    SendPlayerList(roomName);

                    // 방이 비었으면 삭제
                    if (room.GetHumanPlayerCount() == 0)
                    {
                        // 타이머 정리
                        if (roomTimers.ContainsKey(roomName))
                        {
                            roomTimers[roomName].Stop();
                            roomTimers[roomName].Dispose();
                            roomTimers.Remove(roomName);
                        }

                        rooms.Remove(roomName);
                        Console.WriteLine("방 " + roomName + " 삭제됨");
                        BroadcastRoomListToAll();
                    }
                }
            }
        }

        private void BroadcastRoomListToAll()
        {
            List<string> roomNames = new List<string>();
            foreach (string roomName in rooms.Keys)
            {
                roomNames.Add(roomName);
            }

            string roomList = string.Join(",", roomNames.ToArray());
            string message = "ROOM_LIST:" + roomList;

            // 모든 연결된 클라이언트에게 방 목록 전송
            foreach (int clientId in writers.Keys)
            {
                try
                {
                    writers[clientId].WriteLine(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("방 목록 브로드캐스트 실패 (" + clientId + "): " + ex.Message);
                }
            }
        }

        private void SetPlayerName(int playerId, string name)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);
                if (player != null)
                {
                    player.Name = name;
                    SendPlayerList(roomName);
                }
            }
        }

        private void SetPlayerReady(int playerId)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);
                if (player != null && !player.IsAI)
                {
                    player.IsReady = true;
                    Console.WriteLine("플레이어 " + playerId + "(" + player.Name + ") 준비 완료");

                    BroadcastToRoom(roomName, "PLAYER_READY:" + player.Name);
                    SendPlayerList(roomName);

                    CheckAllReady(roomName);
                }
            }
        }

        private void CheckAllReady(string roomName)
        {
            if (!rooms.ContainsKey(roomName))
            {
                Console.WriteLine("방을 찾을 수 없습니다: " + roomName);
                return;
            }

            GameRoom room = rooms[roomName];
            Console.WriteLine("=== 준비 상태 확인 ===");
            Console.WriteLine($"현재 플레이어 수: {room.Players.Count}, 필요한 플레이어 수: {GameSettings.PlayerCount}");

            // 플레이어 수 확인
            if (room.Players.Count < GameSettings.PlayerCount)
            {
                Console.WriteLine("플레이어 수 부족: " + room.Players.Count + "/" + GameSettings.PlayerCount);
                return;
            }

            // 준비 상태 확인
            foreach (Players player in room.Players)
            {
                Console.WriteLine($"플레이어 {player.Name} (ID: {player.Id}, 준비 상태: {player.IsReady})");
                if (!player.IsReady)
                {
                    Console.WriteLine("플레이어 " + player.Name + "이(가) 준비되지 않았습니다.");
                    return;
                }
            }

            Console.WriteLine("모든 플레이어가 준비 완료. 게임 시작 준비!");

            // 게임 시작 알림 전송
            BroadcastToRoom(roomName, "GAME_STARTING");

            // 약간의 지연 후 게임 시작
            Thread.Sleep(2000);

            // 플레이어들의 ready 상태를 초기화 (게임 준비용)
            foreach (Players player in room.Players)
            {
                if (!player.IsAI)
                {
                    player.IsReady = false; // 게임 준비 상태로 초기화
                }
            }
            StartGame(roomName);

        }

        //Server.cs의 StartGame 메소드 수정
        private void StartGame(string roomName)
        {
            GameRoom room = rooms[roomName];
            Console.WriteLine("방 " + roomName + " 게임 시작!");

            // 게임 상태 초기화
            room.GameStarted = true;
            room.CurrentPhase = "Day";
            room.DayCount = 1;
            room.VoteResults.Clear();
            room.NightActions.Clear();

            // 역할 배정
            AssignRoles(roomName);
            InitializeAITrustAndSuspicions(room); // 역할 배정 이후에 호출

            // 플레이어 목록 전송 (역할 배정 후)
            List<string> playerNames = new List<string>();
            foreach (Players player in room.Players)
            {
                playerNames.Add(player.Name);
            }
            string playerList = string.Join(",", playerNames.ToArray());

            //Thread.Sleep(1000);
            // 게임 폼으로 전환 신호 전송
            BroadcastToRoom(roomName, "START_PHASE:Day");

            // 역할 전송 전 잠시 대기
            //Thread.Sleep(2000);

            // 각 플레이어에게 개별적으로 역할 전송 (AI가 아닌 플레이어만)
            foreach (Players player in room.Players)
            {
                if (!player.IsAI && writers.ContainsKey(player.Id))
                {
                    // 개별 플레이어에게만 자신의 역할 전송
                    writers[player.Id].WriteLine("ROLE:" + player.Role);
                    Console.WriteLine($"플레이어 {player.Name}({player.Id})에게 역할 '{player.Role}' 전송");
                }
            }

            // 실제 게임 시작 신호 전송 (역할 배정 완료 후)
            Thread.Sleep(2000);
            StartDayPhase(roomName);
        }

        private void InitializeAITrustAndSuspicions(GameRoom room)
        {
            Random rnd = new Random();

            foreach (var ai in room.Players.Where(p => p.IsAI))
            {
                var trust = new Dictionary<string, int>();
                foreach (var target in room.Players)
                {
                    trust[target.Name] = (ai.Name == target.Name) ? 100 : 45 + rnd.Next(11); // 자기자신은 100, 나머진 45~55
                }
                room.AiTrustScores[ai.Name] = trust;

                var sus = new List<string>();
                int suspicionCount = rnd.Next(1, 3);
                while (sus.Count < suspicionCount)
                {
                    var candidate = room.Players[rnd.Next(room.Players.Count)].Name;
                    if (candidate != ai.Name && !sus.Contains(candidate))
                        sus.Add(candidate);
                }
                room.AiSuspicions[ai.Name] = sus;
            }
        }


        private void StartDayPhase(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            room.CurrentPhase = "Day";
            room.VoteResults.Clear();

            BroadcastToRoom(roomName, "GAME_PHASE_START:Day");

            // AI 플레이어들의 자동 행동 시뮬레이션
            Thread aiThread = new Thread(() => SimulateAIActions(roomName, "Day"));
            aiThread.IsBackground = true;
            aiThread.Start();

            // 낮 페이즈 타이머 (45초)
            StartPhaseTimer(roomName, 45000, () => EndDayPhase(roomName));
        }

        private void StartNightPhase(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            room.CurrentPhase = "Night";
            room.NightActions.Clear();

            BroadcastToRoom(roomName, "GAME_PHASE_START:Night");
            BroadcastToRoom(roomName, "CHAT:[시스템] 밤이 되었습니다. 특수 능력을 가진 플레이어는 행동을 선택하세요.");

            // AI 플레이어들의 자동 행동 시뮬레이션
            Thread aiThread = new Thread(() => SimulateAIActions(roomName, "Night"));
            aiThread.IsBackground = true;
            aiThread.Start();

            // 밤 페이즈 타이머 (25초)
            StartPhaseTimer(roomName, 25000, () => EndNightPhase(roomName));
        }

        private void StartPhaseTimer(string roomName, int milliseconds, Action onTimerEnd)
        {
            if (roomTimers.ContainsKey(roomName))
            {
                roomTimers[roomName].Stop();
                roomTimers[roomName].Dispose();
            }

            System.Timers.Timer timer = new System.Timers.Timer(milliseconds);
            timer.Elapsed += (sender, e) =>
            {
                timer.Stop();
                timer.Dispose();
                roomTimers.Remove(roomName);
                onTimerEnd?.Invoke();
            };
            timer.AutoReset = false;
            timer.Start();

            roomTimers[roomName] = timer;
        }

        private void EndDayPhase(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            Console.WriteLine($"방 {roomName} 낮 페이즈 종료");

            // 투표 결과 처리
            string eliminatedPlayer = ProcessVotes(roomName);

            if (!string.IsNullOrEmpty(eliminatedPlayer))
            {
                EliminatePlayer(roomName, eliminatedPlayer, "투표로 처형당했습니다.");
            }
            else
            {
                BroadcastToRoom(roomName, "CHAT:[시스템] 투표 결과가 동점이거나 투표가 없어 아무도 처형되지 않았습니다.");
            }

            // 게임 종료 조건 확인
            if (CheckGameEnd(roomName)) return;

            room.VoteResults.Clear();

            // 밤 페이즈 시작
            Thread.Sleep(3000);
            StartNightPhase(roomName);
        }

        private void EndNightPhase(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            Console.WriteLine($"방 {roomName} 밤 페이즈 종료");

            // 밤 행동 결과 처리
            ProcessNightActions(roomName);

            // 게임 종료 조건 확인
            if (CheckGameEnd(roomName)) return;

            // 다음 날 시작
            room.DayCount++;
            Thread.Sleep(3000);
            StartDayPhase(roomName);
        }

        private string ProcessVotes(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return null;

            GameRoom room = rooms[roomName];
            Dictionary<string, int> voteCount = new Dictionary<string, int>();

            // 투표 집계
            foreach (var vote in room.VoteResults)
            {
                string target = vote.Value;
                if (!voteCount.ContainsKey(target))
                    voteCount[target] = 0;
                voteCount[target]++;
            }

            if (voteCount.Count == 0) return null;

            // 최다 득표자 찾기
            int maxVotes = voteCount.Values.Max();
            var candidates = voteCount.Where(kvp => kvp.Value == maxVotes).ToList();

            if (candidates.Count == 1)
            {
                string eliminated = candidates[0].Key;
                BroadcastToRoom(roomName, $"VOTE_RESULT:{eliminated}이(가) {maxVotes}표로 처형됩니다.");
                return eliminated;
            }
            else
            {
                // 동점인 경우 랜덤 선택
                Random rnd = new Random();
                string eliminated = candidates[rnd.Next(candidates.Count)].Key;
                BroadcastToRoom(roomName, $"VOTE_RESULT:동점 결과 {eliminated}이(가) 처형됩니다.");
                return eliminated;
            }
        }


        // 개별 플레이어에게 메시지 전송 메소드 (누락되어 있던 부분)
        private void BroadcastToPlayer(string playerName, string message)
        {
            foreach (var room in rooms.Values)
            {
                var player = room.GetPlayer(playerName);
                if (player != null && !player.IsAI && writers.ContainsKey(player.Id))
                {
                    try
                    {
                        writers[player.Id].WriteLine(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"플레이어 {playerName}에게 메시지 전송 실패: {ex.Message}");
                    }
                    break;
                }
            }
        }

        private void HandleDeadChat(int playerId, string content)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);

                if (player != null && !player.IsAlive)
                {
                    string senderName = player.Name;

                    // 죽은 플레이어들에게만 채팅 전송
                    foreach (Players p in room.Players)
                    {
                        if (!p.IsAlive && !p.IsAI && writers.ContainsKey(p.Id))
                        {
                            try
                            {
                                writers[p.Id].WriteLine("DEAD_CHAT:" + senderName + ": " + content);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("죽은 플레이어 채팅 전송 실패 (" + p.Id + "): " + ex.Message);
                            }
                        }
                    }
                }
            }
        }

        private void HandleAction(int playerId, string actionData)
        {
            if (!clientRooms.ContainsKey(playerId)) return;

            string roomName = clientRooms[playerId];
            GameRoom room = rooms[roomName];
            Players player = room.GetPlayer(playerId);

            if (player == null || !player.IsAlive) return;

            if (actionData.StartsWith("VOTE:"))
            {
                string target = actionData.Substring(5);
                room.VoteResults[player.Name] = target;
                BroadcastToRoom(roomName, $"VOTE:{player.Name}이(가) {target}에게 투표했습니다.");
                Console.WriteLine($"{player.Name}이(가) {target}에게 투표");
            }
            else if (isNightAction(actionData))
            {
                string[] parts = actionData.Split(':');
                if (parts.Length == 2)
                {
                    string action = parts[0];       // ex: "ATTACK"
                    string target = parts[1];       // ex: "홍길동"
                    room.NightActions[player.Name] = $"{action}:{target}";
                    Console.WriteLine($"{player.Name}의 밤 행동: {action}:{target}");

                    SendToClient(playerId, "ACTION_CONFIRMED:밤 행동이 접수되었습니다.");
                }
                else
                {
                    Console.WriteLine($"[오류] 잘못된 actionData 형식: {actionData}");
                }
            }
        }

        private bool isNightAction(string actionData)
        {
            return (actionData.StartsWith("ATTACK") || actionData.StartsWith("FORTUNE") || actionData.StartsWith("MEDIUM") || actionData.StartsWith("PROTECT"));
        }


        private void ProcessNightActions(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            List<string> nightResults = new List<string>();
            List<string> playersToKill = new List<string>();
            List<string> protectedPlayers = new List<string>();
            List<string> disturbedPlayers = new List<string>();
            Dictionary<string, string> fortuneResults = new Dictionary<string, string>();

            // 보호 행동 먼저 처리
            foreach (var action in room.NightActions)
            {
                if (action.Value.StartsWith("PROTECT:"))
                {
                    string target = action.Value.Substring(8);
                    protectedPlayers.Add(target);
                    Console.WriteLine($"{action.Key}가 {target}을(를) 보호");
                }
            }

            // 다른 행동들 처리
            foreach (var action in room.NightActions)
            {
                string playerName = action.Key;
                string actionData = action.Value;
                if (disturbedPlayers.Contains(playerName)) continue; // 요호에게 교란당한 플레이어는 행동 스킵

                if (actionData.StartsWith("ATTACK:"))
                {
                    string target = actionData.Substring(7);

                    if (GameSettings.QuantumMode)
                    {
                        // 양자인랑 모드에서는 인랑 공격 무효화
                        Console.WriteLine($"[양자인랑] {playerName}의 공격은 봉인되어 무시됨");
                        BroadcastToPlayer(playerName, "CHAT:[시스템] 양자인랑 모드에서는 공격 능력이 봉인되어 사용되지 않습니다.");
                    }
                    else
                    {
                        if (!protectedPlayers.Contains(target))
                        {
                            playersToKill.Add(target);
                            nightResults.Add($"{target}이(가) 인랑에게 공격당했습니다.");
                        }
                        else
                        {
                            nightResults.Add($"{target}이(가) 공격을 받았지만 보호받아 살아남았습니다.");
                            BroadcastToPlayer(target, $"PROTECTION_INFO:당신은 오늘 밤 보호받았습니다.");
                        }
                    }
                }
                else if (actionData.StartsWith("FORTUNE:"))
                {

                    string target = actionData.Substring(8);
                    Players targetPlayer = room.GetPlayer(target);
                    if (targetPlayer != null)
                    {
                        string result = (targetPlayer.Role == "인랑" || targetPlayer.Role == "광인") ? "인랑" : "시민";
                        fortuneResults[target] = result;
                        BroadcastToPlayer(playerName, $"FORTUNE_RESULT:{target}:{result}");
                    }
                    // 역할 확정 (양자인랑 모드)
                    if (GameSettings.QuantumMode && !room.GetPlayer(playerName).IsRoleConfirmed)
                    {
                        room.GetPlayer(playerName).IsRoleConfirmed = true;
                        BroadcastToPlayer(playerName, "ROLE_REVEALED:" + room.GetPlayer(playerName).Role);
                        Console.WriteLine($"[양자인랑] {playerName} → 역할 확정됨: {room.GetPlayer(playerName).Role}");
                    }
                }
                else if (actionData.StartsWith("MEDIUM:"))
                {
                    string target = actionData.Substring(7);
                    // 영매 능력 - 죽은 자와의 대화 (구현은 클라이언트에서)
                    BroadcastToPlayer(playerName, $"MEDIUM_RESULT:{target}와의 영혼 대화가 가능합니다.");
                    // 역할 확정 (양자인랑 모드)
                    if (GameSettings.QuantumMode && !room.GetPlayer(playerName).IsRoleConfirmed)
                    {
                        room.GetPlayer(playerName).IsRoleConfirmed = true;
                        BroadcastToPlayer(playerName, "ROLE_REVEALED:" + room.GetPlayer(playerName).Role);
                        Console.WriteLine($"[양자인랑] {playerName} → 역할 확정됨: {room.GetPlayer(playerName).Role}");
                    }
                }
                else if (actionData.StartsWith("NEKOMATA:"))
                {

                    string target = actionData.Substring(9);
                    // 네코마타 능력 - 특수 효과
                    nightResults.Add($"네코마타의 신비한 능력이 발동했습니다.");
                    // 역할 확정 (양자인랑 모드)
                    if (GameSettings.QuantumMode && !room.GetPlayer(playerName).IsRoleConfirmed)
                    {
                        room.GetPlayer(playerName).IsRoleConfirmed = true;
                        BroadcastToPlayer(playerName, "ROLE_REVEALED:" + room.GetPlayer(playerName).Role);
                        Console.WriteLine($"[양자인랑] {playerName} → 역할 확정됨: {room.GetPlayer(playerName).Role}");
                    }
                }
                else if (actionData.StartsWith("DISTURB:"))
                {
                    string target = actionData.Substring(8);
                    disturbedPlayers.Add(target);
                    nightResults.Add($"{target}이(가) 요호에게 교란당해 행동을 취할 수 없었습니다.");
                    BroadcastToPlayer(target, $"CHAT:[시스템] 당신은 요호의 교란에 당해 아무 행동도 하지 못했습니다.");
                    // 역할 확정 (양자인랑 모드)
                    if (GameSettings.QuantumMode && !room.GetPlayer(playerName).IsRoleConfirmed)
                    {
                        room.GetPlayer(playerName).IsRoleConfirmed = true;
                        BroadcastToPlayer(playerName, "ROLE_REVEALED:" + room.GetPlayer(playerName).Role);
                        Console.WriteLine($"[양자인랑] {playerName} → 역할 확정됨: {room.GetPlayer(playerName).Role}");
                    }
                }
            }

            // 밤 결과 발표
            BroadcastToRoom(roomName, "CHAT:[시스템] === 밤이 지나갔습니다 ===");

            foreach (string result in nightResults)
            {
                BroadcastToRoom(roomName, $"NIGHT_RESULT:{result}");
                Thread.Sleep(1000);
            }

            // 플레이어 제거
            foreach (string playerToKill in playersToKill)
            {
                EliminatePlayer(roomName, playerToKill, "밤에 사망했습니다.");
                Thread.Sleep(1000);
            }
        }

        private void EliminatePlayer(string roomName, string playerName, string reason)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            Players player = room.GetPlayer(playerName);


            if (player != null && player.IsAlive)
            {
                player.IsAlive = false;

                BroadcastToRoom(roomName, $"PLAYER_DIED:{playerName}");
                Thread.Sleep(500);

                BroadcastToRoom(roomName, $"CHAT:[시스템] {playerName}님이 {reason}");

                // 특수 능력 처리 (영매의 점괘)
                if (player.Role == "영매")
                {
                    BroadcastToRoom(roomName, $"CHAT:[시스템] 영매의 능력으로 마지막으로 죽은 자의 직업은 '{player.Role}'입니다.");
                }

                // 특수 능력 처리 (사냥꾼의 반격 등)
                if (player.Role == "사냥꾼")
                {
                    BroadcastToRoom(roomName, "CHAT:[시스템] 사냥꾼이 죽으면서 한 명을 도련할 수 있습니다!");
                    // 실제 구현시 사냥꾼 플레이어에게 선택권 부여
                }

                // 특수 능력 처리 (네코마타의 동귀어진)
                if (player.Role == "네코마타")
                {
                    var candidates = room.Players.Where(p => p.IsAlive && p.Name != playerName).ToList();
                    if (candidates.Count > 0)
                    {
                        Random rnd = new Random();
                        var victim = candidates[rnd.Next(candidates.Count)];
                        victim.IsAlive = false;
                        BroadcastToRoom(roomName, $"CHAT:[시스템] 네코마타의 저주로 {victim.Name}도 함께 사망했습니다!");
                        BroadcastToRoom(roomName, $"PLAYER_DIED:{victim.Name}");
                    }
                }

                SendPlayerList(roomName);

            }
        }
        private string lastGameResult = "";
        private bool CheckGameEnd(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return false;

            GameRoom room = rooms[roomName];
           
            if (room.GameEnded)
            {
                Console.WriteLine($"[게임 종료] {roomName} - 이미 게임 종료 상태");
                return true;
            }
            List<Players> alivePlayers = room.Players.Where(p => p.IsAlive).ToList();
            List<Players> aliveWolves = alivePlayers.Where(p => p.Role == "인랑").ToList();
            List<Players> aliveCitizens = alivePlayers.Where(p => p.Role != "인랑" && p.Role != "광인").ToList();

            // 인랑이 모두 죽었으면 시민 승리
            if (aliveWolves.Count == 0)
            {
                lastGameResult = "🎉 시민팀 승리! 모든 인랑을 제거했습니다.";
                BroadcastToRoom(roomName, "GAME_END:시민팀 승리! 모든 인랑을 제거했습니다.");
                Thread.Sleep(100);
                EndGame(roomName);
                return true;
            }

            // 인랑 수가 시민 수 이상이면 인랑 승리
            if (aliveWolves.Count >= aliveCitizens.Count)
            {
                lastGameResult = "🐺 인랑팀 승리! 인랑이 마을을 장악했습니다.";
                BroadcastToRoom(roomName, "GAME_END:인랑팀 승리! 인랑이 마을을 장악했습니다.");
                Thread.Sleep(100);

                EndGame(roomName);
                return true;
            }

            return false;
        }

        private void EndGame(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            room.GameEnded = true;

            room.GameEnded = true;
            room.GameStarted = false;

            // 타이머 정리
            if (roomTimers.ContainsKey(roomName))
            {
                roomTimers[roomName].Stop();
                roomTimers[roomName].Dispose();
                roomTimers.Remove(roomName);
            }

            // 모든 플레이어 역할 공개
            StringBuilder roleReveal = new StringBuilder();
            
            roleReveal.AppendLine("=== 게임 종료 ===");
     
            roleReveal.AppendLine("");
            roleReveal.AppendLine(lastGameResult); // 게임 결과 먼저 표시

            foreach (Players player in room.Players)
            {
                string status = player.IsAlive ? "생존" : "사망";
                roleReveal.AppendLine($"{player.Name}: {player.Role} ({status})");
            }

            BroadcastToRoom(roomName, "GAME_ROLES:" + roleReveal.ToString());

          
        }




        //private void AssignRoles(string roomName)
        //{
        //    GameRoom room = rooms[roomName];
        //    Random rnd = new Random();
        //    List<string> assignedRoles = new List<string>();    // 직업 배정시 이용


        //    if (GameSettings.YaminabeMode)
        //    {
        //        assignedRoles = AssignYaminabeRoles(rnd, room);
        //    }
        //    else
        //    {
        //        assignedRoles = AssignStandardRoles(rnd, room);
        //    }


        //    // 역할 배정
        //    for (int i = 0; i < room.Players.Count; i++)
        //    {
        //        Players player = room.Players[i];
        //        player.Role = assignedRoles[i];

        //        if (!player.IsAI && writers.ContainsKey(player.Id))
        //        {
        //            writers[player.Id].WriteLine("ROLE:" + player.Role);
        //        }
        //    }
        //}

        private void AssignRoles(string roomName)
        {
            GameRoom room = rooms[roomName];
            Random rnd = new Random();

            if (GameSettings.YaminabeMode)
            {
                Console.WriteLine("야미나베 모드 활성화됨");
                AssignYaminabeRoles(room); // ← 변경: 더 이상 리스트를 반환하지 않음
            }
            else if (GameSettings.QuantumMode)
            {
                Console.WriteLine("양자인랑 모드 활성화됨");
                AssignQuantumRoles(room);
            }
            else
            {
                List<string> assignedRoles = AssignStandardRoles(rnd, room);

                for (int i = 0; i < room.Players.Count; i++)
                {
                    Players player = room.Players[i];
                    player.Role = assignedRoles[i];
                }
            }


        }


        /// <summary>
        /// 표준 모드 직업 배정 - 인원 조정 요구사항 반영
        /// </summary>
        private List<string> AssignStandardRoles(Random random, GameRoom room)
        {
            List<string> availableRoles = new List<string>();

            // 플레이어 수에 따른 인랑측 인원 결정 (인랑 + 광인)
            int wolfTeamCount;
            int wolfCount;
            int madmanCount;

            if (room.Players.Count < 6)
            {
                // 6명 미만: 인랑 1명, 광인 0명
                wolfTeamCount = 1;
                wolfCount = 1;
                madmanCount = 0;
            }
            else if (room.Players.Count <= 9)
            {
                // 6~9명: 인랑+광인 합쳐서 2명
                wolfTeamCount = 2;
                // 인랑은 최소 1명 보장
                wolfCount = 1;
                madmanCount = 1;
            }
            else
            {
                // 10명 이상: 인랑측 총 3명
                wolfTeamCount = 3;
                // 인랑은 최소 2명 보장
                wolfCount = 2;
                madmanCount = 1;
            }

            // 특수 직업 배정
            int fortuneTellerCount = 1;  // 점쟁이 1명 (항상 존재)
            int mediumCount = room.Players.Count >= 7 ? 1 : 0;  // 영매 (7명 이상일 때)
            int hunterCount = room.Players.Count >= 6 ? 1 : 0;  // 사냥꾼 (6명 이상일 때)
            int foxCount = room.Players.Count >= 8 ? 1 : 0;  // 여우 (8명 이상일 때)
            int immoralCount = foxCount > 0 && room.Players.Count >= 9 ? 1 : 0;  // 배덕자 (9명 이상이고 여우가 있을 때)
            int nekomataCount = room.Players.Count >= 10 ? 1 : 0;  // 네코마타 (10명 이상일 때)

            // 나머지는 시민으로 채움
            int civilianCount = room.Players.Count - (wolfCount + fortuneTellerCount + mediumCount +
                                             hunterCount + nekomataCount + madmanCount +
                                             foxCount + immoralCount);

            // 직업 리스트에 추가
            for (int i = 0; i < civilianCount; i++) availableRoles.Add("시민");
            for (int i = 0; i < wolfCount; i++) availableRoles.Add("인랑");
            for (int i = 0; i < fortuneTellerCount; i++) availableRoles.Add("점쟁이");
            for (int i = 0; i < mediumCount; i++) availableRoles.Add("영매");
            for (int i = 0; i < hunterCount; i++) availableRoles.Add("사냥꾼");
            for (int i = 0; i < nekomataCount; i++) availableRoles.Add("네코마타");
            for (int i = 0; i < madmanCount; i++) availableRoles.Add("광인");
            for (int i = 0; i < foxCount; i++) availableRoles.Add("여우");
            for (int i = 0; i < immoralCount; i++) availableRoles.Add("배덕자");

            // 셔플 후 배정
            assignedRoles = availableRoles.OrderBy(x => random.Next()).ToList();

            return assignedRoles;
        }

        /// <summary>
        /// 야미나베 모드 직업 배정
        /// </summary>
        //private List<string> AssignYaminabeRoles(Random random, GameRoom room)
        //{
        //    // 모든 가능한 직업 목록
        //    List<string> allRoles = roles;

        //    // 최소 1명의 인랑은 보장
        //    assignedRoles.Add("인랑");

        //    // 나머지 인원 랜덤 배정
        //    for (int i = 1; i < room.Players.Count; i++)
        //    {
        //        int randomIndex = random.Next(allRoles.Count);
        //        assignedRoles.Add(allRoles[randomIndex]);
        //    }

        //    // 결과 셔플
        //    assignedRoles = assignedRoles.OrderBy(x => random.Next()).ToList();

        //    return assignedRoles;
        //}
        private void AssignYaminabeRoles(GameRoom room)
        {
            List<string> rolePool = new List<string>
    {
        "시민", "시민", "시민",
        "점쟁이", "사냥꾼", "영매", "네코마타",
        "광인", "요호", "배덕자",
        "인랑", "인랑"
    };

            Random rand = new Random();
            List<Players> shuffledPlayers = room.Players.OrderBy(p => rand.Next()).ToList();
            List<string> shuffledRoles = rolePool.OrderBy(x => rand.Next()).ToList();

            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                string role = shuffledRoles[i % shuffledRoles.Count]; // 부족하면 반복됨
                shuffledPlayers[i].Role = role;
                Console.WriteLine($"[야미나베] {shuffledPlayers[i].Name} → {role}");
            }
        }

        /// <summary>
        /// 양자인랑 모드 역할 배정 - 플레이어는 진짜 직업과 후보 직업 리스트를 따로 가짐
        /// </summary>
        private void AssignQuantumRoles(GameRoom room)
        {
            List<string> allRoles = new List<string>
    {
        "시민", "시민", "시민",
        "점쟁이", "사냥꾼", "영매", "네코마타",
        "광인", "요호", "배덕자",
        "인랑", "인랑"
    };

            Random rand = new Random();
            List<Players> shuffledPlayers = room.Players.OrderBy(p => rand.Next()).ToList();
            List<string> shuffledRoles = allRoles.OrderBy(x => rand.Next()).ToList();

            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                Players player = shuffledPlayers[i];
                string trueRole = shuffledRoles[i % shuffledRoles.Count];
                player.Role = trueRole;
                player.IsRoleConfirmed = false;

                // 후보군 만들기 (자신의 진짜 직업 포함, 랜덤한 다른 직업들 추가)
                List<string> candidatePool = new List<string>(allRoles);
                candidatePool.Remove(trueRole);
                candidatePool = candidatePool.OrderBy(x => rand.Next()).ToList();

                player.RoleCandidates = new List<string> { trueRole, candidatePool[0] };
                if (rand.NextDouble() < 0.5)
                {
                    player.RoleCandidates.Add(candidatePool[1]);
                }

                player.RoleCandidates = player.RoleCandidates.OrderBy(x => rand.Next()).ToList(); // 셔플

                if (!player.IsAI && writers.ContainsKey(player.Id))
                {
                    string choices = string.Join(",", player.RoleCandidates);
                    writers[player.Id].WriteLine("ROLE_CHOICES:" + choices);
                    Console.WriteLine($"[양자인랑] {player.Name} 후보군 전송 → {choices} (진짜는 {trueRole})");
                }
            }
        }


        private void SendPlayerList(string roomName)
        {
            if (rooms.ContainsKey(roomName))
            {
                GameRoom room = rooms[roomName];
                List<string> playerNames = new List<string>();

                foreach (Players player in room.Players)
                {
                    string playerInfo = player.Name;
                    if (!player.IsAlive)
                        playerInfo += " [죽음]";
                    if (player.IsReady)
                        playerInfo += " [준비]";

                    playerNames.Add(playerInfo);
                }

                string playerList = string.Join(",", playerNames.ToArray());

                BroadcastToRoom(roomName, "PLAYER_LIST:" + playerList);

            }
            else
            {
                MessageBox.Show("SendPlayerList error");
            }
        }

        private void HandleChat(int playerId, string content)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);

                if (player != null)
                {
                    string senderName = player.Name;
                    BroadcastToRoom(roomName, "CHAT:" + senderName + ": " + content);
                }
            }
        }

        // AI 행동 시뮬레이션 메소드 (누락되어 있던 부분)
        private void SimulateAIActions(string roomName, string phase)
        {
            Thread.Sleep(1000); // AI 행동 1초 대기 후 시작

            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            Random rnd = new Random();

            var aiPlayers = room.Players.Where(p => p.IsAI && p.IsAlive).ToList();
            foreach (var ai in aiPlayers)
            {
                Thread.Sleep(100);

                if (!room.Players.Contains(ai) || !ai.IsAlive)
                    continue;

                // 공통: 신뢰도 테이블 존재 여부 확인
                if (!room.AiTrustScores.ContainsKey(ai.Name))
                    continue;

                var trustMap = room.AiTrustScores[ai.Name];
                var aliveOthers = room.Players.Where(p => p.IsAlive && p.Name != ai.Name).ToList();
                if (aliveOthers.Count == 0) continue;

                if (phase == "Day" && room.CurrentPhase == "Day")
                {


                    // 2. AI 투표
                    var voteTarget = aliveOthers.OrderBy(p => trustMap.ContainsKey(p.Name) ? trustMap[p.Name] : 50).FirstOrDefault();
                    if (voteTarget != null)
                    {
                        lock (room.VoteResults)
                        {
                            room.VoteResults[ai.Name] = voteTarget.Name;
                        }

                    }
                }
                else if (phase == "Night" && room.CurrentPhase == "Night")
                {
                    string action = "";
                    Players target = null;

                    switch (ai.Role)
                    {
                        case "인랑":
                            target = aliveOthers.OrderBy(p => trustMap.ContainsKey(p.Name) ? trustMap[p.Name] : 50).FirstOrDefault();
                            if (target != null)
                                action = "ATTACK:" + target.Name;
                            break;

                        case "점쟁이":
                            target = aliveOthers.OrderBy(p => trustMap.ContainsKey(p.Name) ? trustMap[p.Name] : 50).FirstOrDefault();
                            if (target != null)
                                action = "FORTUNE:" + target.Name;
                            break;

                        case "사냥꾼":
                            target = aliveOthers.OrderByDescending(p => trustMap.ContainsKey(p.Name) ? trustMap[p.Name] : 50).FirstOrDefault();
                            if (target != null)
                                action = "PROTECT:" + target.Name;
                            break;
                    }

                    if (!string.IsNullOrEmpty(action))
                    {
                        lock (room.NightActions)
                        {
                            room.NightActions[ai.Name] = action;
                        }

                        BroadcastToRoom(roomName, $"CHAT:[시스템] {ai.Name}이(가) 밤에 행동을 선택했습니다.");
                    }
                }

            }

            foreach (var ai in aiPlayers)
            {
                Thread.Sleep(rnd.Next(4000, 10000)); // AI마다 8~20초 랜덤 딜레이

                if (!room.Players.Contains(ai) || !ai.IsAlive)
                    continue;

                // 공통: 신뢰도 테이블 존재 여부 확인
                if (!room.AiTrustScores.ContainsKey(ai.Name))
                    continue;

                var trustMap = room.AiTrustScores[ai.Name];
                var aliveOthers = room.Players.Where(p => p.IsAlive && p.Name != ai.Name).ToList();
                if (aliveOthers.Count == 0) continue;

                if (phase == "Day" && room.CurrentPhase == "Day")
                {
                    // AI 채팅 (의심 발언)
                    //var leastTrusted = trustMap.OrderBy(kv => kv.Value).FirstOrDefault().Key;
                    //string chat = $"{leastTrusted}이(가) 수상해 보여요. 이상한 행동을 해요.";
                    //BroadcastToRoom(roomName, $"CHAT:[{ai.Name}] {chat}");
                    string lastChat = GetLatestChatMessage(roomName);
                    string chat = GenerateAIChat(ai, trustMap, lastChat);
                    BroadcastToRoom(roomName, $"CHAT:[{ai.Name}] {chat}");



                }


            }
        }

        private string GetLatestChatMessage(string roomName)
        {
            if (!chatLogs.ContainsKey(roomName)) return "";
            List<string> logs = chatLogs[roomName];
            if (logs.Count == 0) return "";
            string last = logs[logs.Count - 1];
            Console.WriteLine("lastChat: " + last); // 여기는 'last' 변수로 출력해야 함
            return logs[logs.Count - 1];
        }

        private string GenerateAIChat(Players ai, Dictionary<string, int> trust, string lastChat)
        {
            if (trust == null || trust.Count == 0)
                return "";

            string role = ai.Role;
            string leastTrusted = trust
                .Where(kvp => kvp.Key != ai.Name)
                .OrderBy(kvp => kvp.Value)
                .First()
                .Key;

            // 채팅 내용만 추출
            string cleanChat = lastChat;

            // CASE 1: "CHAT:[Player] 내용"
            if (lastChat.StartsWith("CHAT:["))
            {
                int endIndex = lastChat.IndexOf("]");
                if (endIndex != -1 && endIndex + 2 < lastChat.Length)
                    cleanChat = lastChat.Substring(endIndex + 2);
            }
            // CASE 2: "CHAT:Player: 내용"
            else if (lastChat.StartsWith("CHAT:"))
            {
                int secondColon = lastChat.IndexOf(":", 5);
                if (secondColon != -1 && secondColon + 1 < lastChat.Length)
                    cleanChat = lastChat.Substring(secondColon + 1).Trim();
            }

            if (role == "시민")
            {
                if (cleanChat.Contains("누구"))
                    return $"{leastTrusted}이(가) 의심되네요.";
                else if (cleanChat.Contains("인랑"))
                    return $"하...누구지";
                else if (cleanChat.Contains("요호"))
                    return $"가장 과묵한 사람이 요호일 겁니다";
                else if (cleanChat.Contains("안녕"))
                    return $"수고가 많으십니다";
                else if (cleanChat.Contains("신중"))
                    return $"맞아요 한 번 더 생각해 봅시다. 그래도 저는...";
                else if (cleanChat.Contains("ㅋㅋㅋ"))
                    return $"껄껄껄";
            }
            else if (role == "인랑")
            {
                if (cleanChat.Contains("인랑"))
                    return $"그보다는 우선 요호를 잡는 게 우선 아닐까요?";
                else if (cleanChat.Contains("누구"))
                    return $"누굴까... 일단 {leastTrusted}는 아닙니다.";
                else if (cleanChat.Contains("의심"))
                    return $"일단 투표를 해보죠 그러면";
                else if (cleanChat.Contains("요호"))
                    return $"...진짜 조용하게 있는 사람이 범인입니다";
                else if (cleanChat.Contains("시민"))
                    return $"시민이 문제가 아니라 지금 여우요괴를 먼저 찾아야 합니다...";
                else if (cleanChat.Contains("안녕"))
                    return $"안녕하세요!!";
                else if (cleanChat.Contains("흐음"))
                    return $"뭘 고민하는거져...? 전 누군지 알 것 같은데";
                else if (cleanChat.Contains("믿어"))
                    return $"당신 점쟁인가요?";
            }
            else if (role == "점쟁이")
            {
                if (cleanChat.Contains("인랑"))
                    return $"제 말을 믿어주실 수 있으신가요?";
                else if (cleanChat.Contains("점쟁이"))
                    return $"저는 그 직업은 아니지만...누군지는 알 것 같아요";
                else if (cleanChat.Contains("의심"))
                    return $"그 사람을....";
                else if (cleanChat.Contains("요호"))
                    return $"확실히 늑대인간만큼 무섭죠";
                else if (cleanChat.Contains("시민"))
                    return $"시민이 문제가 아니라 지금 인랑을 먼저 찾아야 합니다...";
            }
            else if (role == "영매")
            {
                if (cleanChat.Contains("인랑"))
                    return $"일단 저는 진짜 아닙니다.";
                else if (cleanChat.Contains("영매"))
                    return $".....";
                else if (cleanChat.Contains("의심"))
                    return $"{leastTrusted} 그 사람을....";
                else if (cleanChat.Contains("요호"))
                    return $"여우도 그만큼 무섭죠. 끝까지 살아남으려고 할 겁니다.";
                else if (cleanChat.Contains("시민"))
                    return $"....";
            }
            else if (role == "사냥꾼")
            {
                if (cleanChat.Contains("인랑"))
                    return $"누구야! 빨리 나와!! 껄껄껄...";
                else if (cleanChat.Contains("사냥꾼"))
                    return $"저 네코마타이니까 건들지 마세요";
                else if (cleanChat.Contains("의심"))
                    return $"{leastTrusted}인가....";
                else if (cleanChat.Contains("큰일"))
                    return $"제가 있으니까 큰일은 없을겁니다.";
                else if (cleanChat.Contains("시민"))
                    return $"확실하게 누가 시민인지 알려주세요";
                else if (cleanChat.Contains("직감"))
                    return $"당신의 촉을 믿지 마세요";
            }
            else if (role == "네코마타")
            {
                if (cleanChat.Contains("인랑"))
                    return $"제가 생각했을 때는 {leastTrusted}이... ";
                else if (cleanChat.Contains("....."))
                    return $"너구나";
                else if (cleanChat.Contains("의심"))
                    return $"{leastTrusted}인가....";
                else if (cleanChat.Contains("시민"))
                    return $"그보다 누가 확실히 늑대인간인지 알려주세요...ㅋㅋ";
                else if (cleanChat.Contains("조용히"))
                    return $"채팅 게임에서 아무 말도 안하는게...";
                else if (cleanChat.Contains("차분히"))
                    return $"ㅋㅋㅋㅋㅋㅋㅋㅋㅋ";
                else if (cleanChat.Contains("차분히"))
                    return $"차분...히....?";
                else if (cleanChat.Contains("빨리"))
                    return $"급하신가 보네요";
                else if (cleanChat.Contains("네코마타"))
                    return $"당신 뭐야";
                else if (cleanChat.Contains("걸렸"))
                    return $"???";
            }
            else if (role == "광인")
            {
                if (cleanChat.Contains("인랑"))
                    return $"에휴...";
                else if (cleanChat.Contains("조용히"))
                    return $"조용히라는 말은 여우가 가장 좋아하는 말인데";
                else if (cleanChat.Contains("의심"))
                    return $"{leastTrusted}은 일단 너무 몰아가는게 좀 이상한 것 같아요";
            }
            else if (role == "여우")
            {
                if (cleanChat.Contains("요호") || cleanChat.Contains("여우"))
                    return ".....";
            }
            else if (role == "배덕자")
            {
                if (cleanChat.Contains("요호") || cleanChat.Contains("여우"))
                    return ".....";
            }

            return $"모두 조심하세요. {leastTrusted}이(가) 뭔가 수상해요.";
        }

        private Players GetLeastTrustedAlivePlayer(string aiName, GameRoom room)
        {
            if (!room.AiTrustScores.ContainsKey(aiName)) return null;
            var trust = room.AiTrustScores[aiName];
            var alive = room.Players.Where(p => p.IsAlive && p.Name != aiName).ToList();
            return alive.OrderBy(p => trust.ContainsKey(p.Name) ? trust[p.Name] : 50).FirstOrDefault();
        }

        private void HandleTimeUp(int playerId)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                BroadcastToRoom(roomName, "CHAT:[시스템] 시간 종료됨");
            }
        }

        private void SendRoomList(int clientId)
        {
            List<string> roomNames = new List<string>();
            foreach (string roomName in rooms.Keys)
            {
                roomNames.Add(roomName);
            }

            string roomList = string.Join(",", roomNames.ToArray());
            SendToClient(clientId, "ROOM_LIST:" + roomList);
        }

        private void BroadcastToRoom(string roomName, string message)
        {
            // 채팅 로그에 저장
            if (!chatLogs.ContainsKey(roomName))
                chatLogs[roomName] = new List<string>();

            chatLogs[roomName].Add(message);

            // 너무 오래되면 잘라냄 (예: 100개 초과 시)
            if (chatLogs[roomName].Count > 100)
                chatLogs[roomName].RemoveAt(0);
            // 클라이언트에게 메시지 전송
            if (rooms.ContainsKey(roomName))
            {

                GameRoom room = rooms[roomName];
                foreach (Players player in room.Players)
                {
                    if (!player.IsAI && writers.ContainsKey(player.Id))
                    {

                        try
                        {
                            writers[player.Id].WriteLine(message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("메시지 전송 실패 (" + player.Id + "): " + ex.Message);
                        }
                    }
                }
            }
        }

        private void SendToClient(int clientId, string message)
        {
            if (writers.ContainsKey(clientId))
            {
                try
                {
                    writers[clientId].WriteLine(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("클라이언트 " + clientId + "에게 메시지 전송 실패: " + ex.Message);
                }
            }
        }

        private void DisconnectClient(int clientId)
        {
            LeaveRoom(clientId);

            if (clients.ContainsKey(clientId))
            {
                try { clients[clientId].Close(); } catch { }
                clients.Remove(clientId);
            }

            if (writers.ContainsKey(clientId))
            {
                writers.Remove(clientId);
            }
        }

        public void Stop()
        {
            try
            {
                listener?.Stop();
                foreach (TcpClient client in clients.Values)
                {
                    client?.Close();
                }
                clients.Clear();
                writers.Clear();
                rooms.Clear();
                clientRooms.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine("서버 종료 오류: " + ex.Message);
            }
        }
    }

    // 게임 방 클래스
    public class GameRoom
    {
        public string Name { get; set; }
        public int HostId { get; set; }
        public List<Players> Players { get; set; }
        public DateTime CreatedTime { get; set; }

        public bool GameStarted { get; set; } = false;
        public bool GameEnded { get; set; } = false;
        public string CurrentPhase { get; set; } = "Lobby";
        public int DayCount { get; set; } = 1;
        public Dictionary<string, string> VoteResults { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> NightActions { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, Dictionary<string, int>> AiTrustScores = new Dictionary<string, Dictionary<string, int>>();
        public Dictionary<string, List<string>> AiSuspicions = new Dictionary<string, List<string>>();


        public GameRoom(string name, int hostId)
        {
            Name = name;
            HostId = hostId;
            Players = new List<Players>();
            CreatedTime = DateTime.Now;
        }

        public void AddPlayer(Players player)
        {
            Players.Add(player);
        }

        public void RemovePlayer(int playerId)
        {
            Players.RemoveAll(p => p.Id == playerId);
        }

        public Players GetPlayer(int playerId)
        {
            return Players.Find(p => p.Id == playerId);
        }

        public Players GetPlayer(string playerName)
        {
            return Players.Find(p => p.Name == playerName);
        }

        public int GetHumanPlayerCount()
        {
            int count = 0;
            foreach (Players player in Players)
            {
                if (!player.IsAI)
                    count++;
            }
            return count;
        }

        public bool AreAllPlayersReady()
        {
            Console.WriteLine($"=== 준비 상태 확인 ===");
            Console.WriteLine($"현재 플레이어 수: {Players.Count}");
            Console.WriteLine($"필요한 플레이어 수: {GameSettings.PlayerCount}");

            if (Players.Count < GameSettings.PlayerCount)
            {
                Console.WriteLine("플레이어 수 부족: " + Players.Count + "/" + GameSettings.PlayerCount);
                return false;
            }

            foreach (Players player in Players)
            {
                Console.WriteLine($"플레이어 {player.Name} (ID: {player.Id}, AI: {player.IsAI}, 준비: {player.IsReady})");
                if (!player.IsReady)
                {
                    Console.WriteLine("플레이어 " + player.Name + "이(가) 준비되지 않음");
                    return false;
                }
            }
            Console.WriteLine("모든 플레이어가 준비 완료!");
            return true;
        }
    }

    // 플레이어 클래스
    public class Players
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public bool IsAI { get; set; }
        public bool IsReady { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool GameReady { get; set; } = false;

        // 양자인랑 모드 전용
        public List<string> RoleCandidates { get; set; } = new List<string>(); // 후보 직업
        public bool IsRoleConfirmed { get; set; } = false; // 능력 발동 후 역할 확정 여부

    }
}
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
        private TcpListener listener;
        private Dictionary<int, TcpClient> clients = new Dictionary<int, TcpClient>();
        private Dictionary<int, StreamWriter> writers = new Dictionary<int, StreamWriter>();
        private int clientIdCounter = 0;

        // 방 관리
        private Dictionary<string, GameRoom> rooms = new Dictionary<string, GameRoom>();
        private Dictionary<int, string> clientRooms = new Dictionary<int, string>();

        // IP 연결 정보
        private HashSet<string> connectedIPs = new HashSet<string>();

        // 게임 타이머
        private Dictionary<string, System.Timers.Timer> roomTimers = new Dictionary<string, System.Timers.Timer>();

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
                    var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    if (connectedIPs.Contains(ip))
                    {
                        Console.WriteLine($"동일 IP 연결 차단: {ip}");
                        client.Close();
                        continue;
                    }
                    connectedIPs.Add(ip);

                    int clientId = clientIdCounter++;
                    clients[clientId] = client;

                    NetworkStream ns = client.GetStream();
                    StreamReader reader = new StreamReader(ns, Encoding.UTF8);
                    StreamWriter writer = new StreamWriter(ns, Encoding.UTF8) { AutoFlush = true };
                    writers[clientId] = writer;

                    writer.WriteLine("ID:" + clientId);
                    Console.WriteLine($"클라이언트 {clientId} 연결됨");

                    Thread receiveThread = new Thread(() => Receive(clientId, reader, ip));
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("클라이언트 연결 오류: " + ex.Message);
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

                    Console.WriteLine($"[수신 {id}] {msg}");

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
                        string name = msg.Substring(5);
                        SetPlayerName(id, name);

                        // 이름 설정 후 추가로 게임 시작 조건 체크
                        if (clientRooms.ContainsKey(id))
                        {
                            string roomName = clientRooms[id];
                            CheckGameStartConditions(roomName);
                        }
                    }
                    else if (msg == "READY")
                    {
                        HandleReady(id);
                    }
                    else if (msg == "LEAVE_ROOM")
                    {
                        LeaveRoom(id);
                    }
                    else if (msg.StartsWith("CHAT:"))
                    {
                        HandleChat(id, msg.Substring(5));
                    }
                    else if (msg.StartsWith("DEAD_CHAT:"))
                    {
                        HandleDeadChat(id, msg.Substring(10));
                    }
                    else if (msg.StartsWith("ACTION:"))
                    {
                        HandleAction(id, msg.Substring(7));
                    }
                    else if (msg == "TIME_UP")
                    {
                        HandleTimeUp(id);
                    }
                    else if (msg == "REQUEST_PLAYER_LIST")
                    {
                        if (clientRooms.ContainsKey(id))
                        {
                            string roomName = clientRooms[id];
                            SendPlayerList(roomName);
                        }
                    }
                    else if (msg == "GAME_READY")
                    {
                        if (clientRooms.ContainsKey(id))
                        {
                            string roomName = clientRooms[id];
                            SendPlayerList(roomName);
                            Thread.Sleep(1000);
                            StartGame(roomName);
                        }
                    }
                    else if (msg == "FORCE_GAME_START")
                    {
                        if (clientRooms.ContainsKey(id))
                        {
                            string roomName = clientRooms[id];
                            Console.WriteLine($"강제 게임 시작 요청: {roomName}");
                            EnsureAIPlayersReady(roomName);
                            CheckGameStartConditions(roomName);
                        }
                    }
                    else if (msg == "START_GAME_NOW")
                    {
                        if (clientRooms.ContainsKey(id))
                        {
                            string roomName = clientRooms[id];
                            Console.WriteLine($"클라이언트 {id}가 게임 즉시 시작 요청");
                            StartGameImmediately(roomName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 {id} 연결 오류: {ex.Message}");
            }
            finally
            {
                DisconnectClient(id);
                connectedIPs.Remove(ip);
            }
        }

        private void CreateRoom(string roomName, int hostId)
        {
            roomName = roomName.Trim();

            if (rooms.ContainsKey(roomName))
            {
                SendToClient(hostId, "ERROR:방 이름이 이미 존재합니다.");
                return;
            }

            GameRoom room = new GameRoom(roomName, hostId);
            rooms[roomName] = room;
            clientRooms[hostId] = roomName;

            // AI 플레이어들 추가 (준비 상태로 설정)
            for (int i = 0; i < GameSettings.AICount; i++)
            {
                Players aiPlayer = new Players
                {
                    Id = -1 - i,
                    Name = "AI_" + (i + 1),
                    IsAI = true,
                    IsReady = true,  // 확실히 준비 상태로 설정
                    IsAlive = true
                };
                room.AddPlayer(aiPlayer);
                Console.WriteLine($"AI 플레이어 {aiPlayer.Name} 추가됨 (준비: {aiPlayer.IsReady})");
            }

            // 호스트 추가
            Players hostPlayer = new Players
            {
                Id = hostId,
                Name = GameSettings.UserName, // 즉시 사용자 이름 설정
                IsAI = false,
                IsReady = false,
                IsAlive = true
            };
            room.AddPlayer(hostPlayer);

            Console.WriteLine($"방 생성됨: {roomName} (호스트: {hostId})");
            SendToClient(hostId, "ROOM_CREATED:" + roomName);
            SendToClient(hostId, "ROOM_JOINED:" + roomName);
            SendPlayerList(roomName);
            BroadcastRoomListToAll();

            // 방 생성 후 게임 시작 조건 체크
            CheckGameStartConditions(roomName);
        }

        private void JoinRoom(string roomName, int playerId)
        {
            roomName = roomName.Trim();

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
                Name = GameSettings.UserName, // 즉시 사용자 이름 설정
                IsAI = false,
                IsReady = false,
                IsAlive = true
            };

            room.AddPlayer(player);
            clientRooms[playerId] = roomName;

            Console.WriteLine($"플레이어 {playerId}가 방 {roomName}에 참가함");
            SendToClient(playerId, "ROOM_JOINED:" + roomName);
            BroadcastToRoom(roomName, "PLAYER_JOINED:" + player.Name);
            SendPlayerList(roomName);

            // 플레이어 참가 후 게임 시작 조건 체크
            CheckGameStartConditions(roomName);
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
                    string oldName = player.Name;
                    player.Name = name;
                    Console.WriteLine($"플레이어 {playerId}의 이름이 {oldName}에서 {name}으로 변경됨");

                    SendPlayerList(roomName);

                    // 이름 설정 후 게임 시작 조건 자동 체크
                    CheckGameStartConditions(roomName);
                }
            }
        }

        private void HandleReady(int playerId)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);

                if (player != null && !player.IsAI)
                {
                    player.IsReady = true;
                    Console.WriteLine($"플레이어 {playerId}({player.Name}) 준비 완료");

                    BroadcastToRoom(roomName, "PLAYER_READY:" + player.Name);
                    SendPlayerList(roomName);

                    // 준비 상태 변경 후 게임 시작 조건 자동 체크
                    CheckGameStartConditions(roomName);
                }
            }
        }

        private void CheckGameStartConditions(string roomName)
        {
            if (!rooms.ContainsKey(roomName))
            {
                Console.WriteLine($"방 {roomName}을 찾을 수 없습니다.");
                return;
            }

            GameRoom room = rooms[roomName];

            // 이미 게임이 시작되었다면 체크하지 않음
            if (room.GameStarted)
            {
                Console.WriteLine($"방 {roomName}은 이미 게임이 시작됨");
                return;
            }

            Console.WriteLine($"=== 방 {roomName} 게임 시작 조건 체크 ===");
            Console.WriteLine($"현재 플레이어 수: {room.Players.Count}");
            Console.WriteLine($"필요한 플레이어 수: {GameSettings.PlayerCount}");

            // 1단계: 플레이어 수 확인
            if (room.Players.Count < GameSettings.PlayerCount)
            {
                Console.WriteLine($"플레이어 수 부족: {room.Players.Count}/{GameSettings.PlayerCount}");
                return;
            }

            // 2단계: 모든 플레이어 이름 설정 확인
            foreach (Players player in room.Players)
            {
                if (string.IsNullOrWhiteSpace(player.Name) || player.Name.Contains("Player_"))
                {
                    Console.WriteLine($"플레이어 {player.Id}의 이름이 설정되지 않음: '{player.Name}'");
                    return;
                }
            }

            // 3단계: AI 플레이어 준비 상태 강제 설정
            foreach (Players player in room.Players)
            {
                if (player.IsAI)
                {
                    player.IsReady = true;
                }
            }

            // 4단계: 모든 플레이어 준비 상태 확인
            int readyCount = 0;
            int totalPlayers = room.Players.Count;

            foreach (Players player in room.Players)
            {
                Console.WriteLine($"플레이어: {player.Name}, AI: {player.IsAI}, 준비: {player.IsReady}");

                if (player.IsReady)
                {
                    readyCount++;
                }
            }

            Console.WriteLine($"준비된 플레이어: {readyCount}/{totalPlayers}");

            // 5단계: 모든 조건 만족시 게임 시작
            if (readyCount >= totalPlayers)
            {
                Console.WriteLine("=== 모든 조건 만족! 게임 자동 시작! ===");
                StartGameSequence(roomName);
            }
            else
            {
                Console.WriteLine($"아직 준비되지 않은 플레이어가 {totalPlayers - readyCount}명 있습니다.");

                // 준비되지 않은 플레이어 목록 출력
                foreach (Players player in room.Players)
                {
                    if (!player.IsReady)
                    {
                        Console.WriteLine($"  - {player.Name} (AI: {player.IsAI})");
                    }
                }
            }
        }

        private void StartGameSequence(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];

            // 중복 시작 방지
            if (room.GameStarted)
            {
                Console.WriteLine($"방 {roomName}은 이미 게임이 시작됨");
                return;
            }

            Console.WriteLine($"=== 방 {roomName} 게임 시작 시퀀스 시작 ===");

            // 게임 시작 상태로 설정 (중복 방지)
            room.GameStarted = true;

            // 게임 시작 알림
            BroadcastToRoom(roomName, "GAME_STARTING");
            Console.WriteLine("게임 시작 알림 전송 완료");

            // 2초 대기
            Thread.Sleep(2000);

            // 플레이어들의 ready 상태 초기화 (게임 중에는 사용하지 않음)
            foreach (Players player in room.Players)
            {
                if (!player.IsAI)
                {
                    player.IsReady = false;
                }
            }

            // 실제 게임 시작
            StartGame(roomName);
        }

        private void EnsureAIPlayersReady(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            foreach (Players player in room.Players)
            {
                if (player.IsAI)
                {
                    player.IsReady = true;
                    Console.WriteLine($"AI 플레이어 {player.Name} 준비 상태 설정");
                }
            }
        }

        private void PrintRoomStatus(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            Console.WriteLine($"\n=== 방 {roomName} 상태 ===");
            Console.WriteLine($"게임 시작됨: {room.GameStarted}");
            Console.WriteLine($"플레이어 수: {room.Players.Count}/{GameSettings.PlayerCount}");

            foreach (Players player in room.Players)
            {
                Console.WriteLine($"  {player.Name}: AI={player.IsAI}, Ready={player.IsReady}, Alive={player.IsAlive}");
            }
            Console.WriteLine("========================\n");
        }

        private void StartGameImmediately(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];

            // 이미 게임이 시작되었다면 무시
            if (room.GameStarted)
            {
                Console.WriteLine($"방 {roomName}은 이미 게임이 시작됨");
                return;
            }

            Console.WriteLine($"방 {roomName} 즉시 게임 시작!");

            // 게임 시작 플래그 설정
            room.GameStarted = true;

            // 모든 플레이어를 준비 상태로 설정
            foreach (Players player in room.Players)
            {
                player.IsReady = true;
                player.IsAlive = true;
            }

            // 게임 상태 초기화
            room.CurrentPhase = "Day";
            room.DayCount = 1;
            room.VoteResults.Clear();
            room.NightActions.Clear();

            // 역할 배정
            AssignRoles(roomName);

            Console.WriteLine("역할 배정 완료:");
            foreach (Players player in room.Players)
            {
                Console.WriteLine($"  {player.Name}: {player.Role}");
            }

            // 모든 클라이언트에게 게임 시작 알림
            BroadcastToRoom(roomName, "GAME_STARTING");
            Thread.Sleep(1000);

            // 역할 전송
            foreach (Players player in room.Players)
            {
                if (!player.IsAI && writers.ContainsKey(player.Id))
                {
                    writers[player.Id].WriteLine("ROLE:" + player.Role);
                    Console.WriteLine($"플레이어 {player.Name}({player.Id})에게 역할 '{player.Role}' 전송");
                }
            }

            Thread.Sleep(1000);

            // 첫 번째 낮 페이즈 시작
            StartDayPhase(roomName);
        }

        private void StartGame(string roomName)
        {
            if (!rooms.ContainsKey(roomName))
            {
                Console.WriteLine($"게임 시작 실패: 방 {roomName}을 찾을 수 없습니다.");
                return;
            }

            GameRoom room = rooms[roomName];
            Console.WriteLine($"=== 방 {roomName} 게임 시작! ===");

            // 게임 상태 초기화
            room.CurrentPhase = "Day";
            room.DayCount = 1;
            room.VoteResults.Clear();
            room.NightActions.Clear();

            // 역할 배정
            Console.WriteLine("역할 배정 중...");
            AssignRoles(roomName);

            // 역할 배정 완료 로그
            foreach (Players player in room.Players)
            {
                Console.WriteLine($"{player.Name}: {player.Role}");
            }

            Console.WriteLine("게임 폼 전환 신호 전송...");

            // 게임 폼으로 전환 신호 전송 (중요!)
            BroadcastToRoom(roomName, "START_PHASE:Day");

            // 잠시 대기 후 역할 전송
            Thread.Sleep(2000);

            Console.WriteLine("역할 정보 전송 중...");
            foreach (Players player in room.Players)
            {
                if (!player.IsAI && writers.ContainsKey(player.Id))
                {
                    try
                    {
                        writers[player.Id].WriteLine("ROLE:" + player.Role);
                        Console.WriteLine($"플레이어 {player.Name}({player.Id})에게 역할 '{player.Role}' 전송 완료");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"플레이어 {player.Id}에게 역할 전송 실패: {ex.Message}");
                    }
                }
            }

            // 첫 번째 낮 페이즈 시작
            Console.WriteLine("첫 번째 낮 페이즈 시작...");
            Thread.Sleep(1000);
            StartDayPhase(roomName);
        }

        private void StartDayPhase(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            room.CurrentPhase = "Day";
            room.VoteResults.Clear();

            Console.WriteLine($"방 {roomName} - {room.DayCount}일차 낮 시작");

            BroadcastToRoom(roomName, "GAME_PHASE_START:Day");
            BroadcastToRoom(roomName, "PHASE_TIME:50");
            BroadcastToRoom(roomName, $"CHAT:[시스템] {room.DayCount}일차 낮이 시작되었습니다. 토론하고 의심스러운 사람에게 투표하세요!");

            // AI 플레이어들의 자동 행동
            Thread aiThread = new Thread(() => SimulateAIActions(roomName, "Day"));
            aiThread.IsBackground = true;
            aiThread.Start();

            // 낮 페이즈 타이머 (50초)
            StartPhaseTimer(roomName, 50000, () => EndDayPhase(roomName));
        }

        private void StartNightPhase(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            room.CurrentPhase = "Night";
            room.NightActions.Clear();

            Console.WriteLine($"방 {roomName} - {room.DayCount}일차 밤 시작");

            BroadcastToRoom(roomName, "GAME_PHASE_START:Night");
            BroadcastToRoom(roomName, "PHASE_TIME:40");
            BroadcastToRoom(roomName, "CHAT:[시스템] 밤이 되었습니다. 특수 능력을 가진 플레이어는 행동을 선택하세요.");

            // AI 플레이어들의 자동 행동
            Thread aiThread = new Thread(() => SimulateAIActions(roomName, "Night"));
            aiThread.IsBackground = true;
            aiThread.Start();

            // 밤 페이즈 타이머 (40초)
            StartPhaseTimer(roomName, 40000, () => EndNightPhase(roomName));
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
                EliminatePlayer(roomName, eliminatedPlayer, "투표로 처형당했습니다");
            }
            else
            {
                BroadcastToRoom(roomName, "CHAT:[시스템] 투표 결과가 동점이거나 투표가 없어 아무도 처형되지 않았습니다.");
            }

            // 게임 종료 조건 확인
            if (CheckGameEnd(roomName)) return;

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

        private void HandleAction(int playerId, string actionData)
        {
            if (!clientRooms.ContainsKey(playerId)) return;

            string roomName = clientRooms[playerId];
            GameRoom room = rooms[roomName];
            Players player = room.GetPlayer(playerId);

            if (player == null || !player.IsAlive) return;

            Console.WriteLine($"플레이어 {player.Name} 행동: {actionData}");

            if (actionData.StartsWith("VOTE:"))
            {
                string target = actionData.Substring(5);
                room.VoteResults[player.Name] = target;

                string voteMessage = $"{player.Name}이(가) {target}에게 투표했습니다.";
                BroadcastToRoom(roomName, "VOTE_UPDATE:" + voteMessage);
                SendToClient(playerId, "ACTION_CONFIRMED");

                Console.WriteLine($"{player.Name}이(가) {target}에게 투표");
            }
            else if (actionData.StartsWith("ATTACK:") || actionData.StartsWith("FORTUNE:") ||
                     actionData.StartsWith("MEDIUM:") || actionData.StartsWith("NEKOMATA:") ||
                     actionData.StartsWith("DISTURB:") || actionData.StartsWith("PROTECT:"))
            {
                room.NightActions[player.Name] = actionData;
                SendToClient(playerId, "ACTION_CONFIRMED");
                Console.WriteLine($"{player.Name}의 밤 행동: {actionData}");
            }
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

        private void ProcessNightActions(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            List<string> nightResults = new List<string>();
            List<string> playersToKill = new List<string>();
            List<string> protectedPlayers = new List<string>();
            List<string> disturbedPlayers = new List<string>();

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

            // 교란 행동 처리
            foreach (var action in room.NightActions)
            {
                if (action.Value.StartsWith("DISTURB:"))
                {
                    string target = action.Value.Substring(8);
                    disturbedPlayers.Add(target);
                    Console.WriteLine($"{action.Key}가 {target}을(를) 교란");
                }
            }

            // 다른 행동들 처리
            foreach (var action in room.NightActions)
            {
                string playerName = action.Key;
                string actionData = action.Value;

                if (disturbedPlayers.Contains(playerName)) continue; // 교란당한 플레이어는 행동 스킵

                if (actionData.StartsWith("ATTACK:"))
                {
                    string target = actionData.Substring(7);
                    if (!protectedPlayers.Contains(target))
                    {
                        playersToKill.Add(target);
                        nightResults.Add($"{target}이(가) 인랑에게 공격당했습니다.");
                    }
                    else
                    {
                        nightResults.Add($"{target}이(가) 공격을 받았지만 보호받아 살아남았습니다.");
                        BroadcastToPlayer(target, "PROTECTION_INFO:당신은 오늘 밤 보호받았습니다.");
                    }
                }
                else if (actionData.StartsWith("FORTUNE:"))
                {
                    string target = actionData.Substring(8);
                    Players targetPlayer = room.GetPlayer(target);
                    if (targetPlayer != null)
                    {
                        string result = (targetPlayer.Role == "인랑" || targetPlayer.Role == "광인") ? "인랑" : "시민";
                        BroadcastToPlayer(playerName, $"FORTUNE_RESULT:{target}:{result}");
                    }
                }
                else if (actionData.StartsWith("MEDIUM:"))
                {
                    string target = actionData.Substring(7);
                    // 영매 능력 - 죽은 자와의 대화
                    BroadcastToPlayer(playerName, $"MEDIUM_RESULT:{target}와의 영혼 대화가 가능합니다.");
                }
                else if (actionData.StartsWith("NEKOMATA:"))
                {
                    string target = actionData.Substring(9);
                    // 네코마타 능력 - 특수 효과
                    nightResults.Add($"네코마타의 신비한 능력이 발동했습니다.");
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
                EliminatePlayer(roomName, playerToKill, "밤에 사망했습니다");
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
                BroadcastToRoom(roomName, $"CHAT:[시스템] {playerName}님이 {reason} (역할: {player.Role})");

                // 특수 능력 처리
                if (player.Role == "사냥꾼")
                {
                    BroadcastToRoom(roomName, "CHAT:[시스템] 사냥꾼이 죽으면서 한 명을 도련할 수 있습니다!");
                }

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

        private bool CheckGameEnd(string roomName)
        {
            if (!rooms.ContainsKey(roomName)) return false;

            GameRoom room = rooms[roomName];
            List<Players> alivePlayers = room.Players.Where(p => p.IsAlive).ToList();
            List<Players> aliveWolves = alivePlayers.Where(p => p.Role == "인랑").ToList();
            List<Players> aliveCitizens = alivePlayers.Where(p => p.Role != "인랑" && p.Role != "광인").ToList();

            // 인랑이 모두 죽었으면 시민 승리
            if (aliveWolves.Count == 0)
            {
                BroadcastToRoom(roomName, "GAME_END:시민팀 승리! 모든 인랑을 제거했습니다.");
                EndGame(roomName);
                return true;
            }

            // 인랑 수가 시민 수 이상이면 인랑 승리
            if (aliveWolves.Count >= aliveCitizens.Count)
            {
                BroadcastToRoom(roomName, "GAME_END:인랑팀 승리! 인랑이 마을을 장악했습니다.");
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

            // 타이머 정리
            if (roomTimers.ContainsKey(roomName))
            {
                roomTimers[roomName].Stop();
                roomTimers[roomName].Dispose();
                roomTimers.Remove(roomName);
            }

            // 모든 플레이어 역할 공개
            StringBuilder roleReveal = new StringBuilder();
            roleReveal.AppendLine("=== 최종 역할 공개 ===");

            foreach (Players player in room.Players)
            {
                string status = player.IsAlive ? "생존" : "사망";
                roleReveal.AppendLine($"{player.Name}: {player.Role} ({status})");
            }

            BroadcastToRoom(roomName, "GAME_ROLES:" + roleReveal.ToString());

            // 5초 후 로비로 복귀
            Thread.Sleep(5000);

            room.GameStarted = false;
            room.GameEnded = false;
            room.CurrentPhase = "Lobby";
            room.DayCount = 1;
            room.VoteResults.Clear();
            room.NightActions.Clear();

            // 모든 플레이어 상태 초기화
            foreach (Players player in room.Players)
            {
                player.IsAlive = true;
                player.Role = "";
                if (!player.IsAI)
                {
                    player.IsReady = false;
                }
            }

            BroadcastToRoom(roomName, "RETURN_TO_LOBBY");
            SendPlayerList(roomName);
        }

        private void AssignRoles(string roomName)
        {
            GameRoom room = rooms[roomName];
            Random rnd = new Random();
            List<string> assignedRoles = new List<string>();

            if (GameSettings.YaminabeMode)
            {
                assignedRoles = AssignYaminabeRoles(rnd, room);
            }
            else
            {
                assignedRoles = AssignStandardRoles(rnd, room);
            }

            // 역할 배정
            for (int i = 0; i < room.Players.Count; i++)
            {
                room.Players[i].Role = assignedRoles[i];
            }
        }

        private List<string> AssignStandardRoles(Random random, GameRoom room)
        {
            List<string> availableRoles = new List<string>();

            // 플레이어 수에 따른 인랑측 인원 결정
            int wolfTeamCount;
            int wolfCount;
            int madmanCount;

            if (room.Players.Count < 6)
            {
                wolfTeamCount = 1;
                wolfCount = 1;
                madmanCount = 0;
            }
            else if (room.Players.Count <= 9)
            {
                wolfTeamCount = 2;
                wolfCount = 1;
                madmanCount = 1;
            }
            else
            {
                wolfTeamCount = 3;
                wolfCount = 2;
                madmanCount = 1;
            }

            // 특수 직업 배정
            int fortuneTellerCount = 1;
            int mediumCount = room.Players.Count >= 7 ? 1 : 0;
            int hunterCount = room.Players.Count >= 6 ? 1 : 0;
            int foxCount = room.Players.Count >= 8 ? 1 : 0;
            int nekomataCount = room.Players.Count >= 10 ? 1 : 0;

            // 나머지는 시민으로 채움
            int civilianCount = room.Players.Count - (wolfCount + fortuneTellerCount + mediumCount +
                                             hunterCount + nekomataCount + madmanCount + foxCount);

            // 직업 리스트에 추가
            for (int i = 0; i < civilianCount; i++) availableRoles.Add("시민");
            for (int i = 0; i < wolfCount; i++) availableRoles.Add("인랑");
            for (int i = 0; i < fortuneTellerCount; i++) availableRoles.Add("점쟁이");
            for (int i = 0; i < mediumCount; i++) availableRoles.Add("영매");
            for (int i = 0; i < hunterCount; i++) availableRoles.Add("사냥꾼");
            for (int i = 0; i < nekomataCount; i++) availableRoles.Add("네코마타");
            for (int i = 0; i < madmanCount; i++) availableRoles.Add("광인");
            for (int i = 0; i < foxCount; i++) availableRoles.Add("요호");

            // 셔플 후 배정
            return availableRoles.OrderBy(x => random.Next()).ToList();
        }

        private List<string> AssignYaminabeRoles(Random random, GameRoom room)
        {
            List<string> assignedRoles = new List<string>();
            List<string> allRoles = roles;

            // 최소 1명의 인랑은 보장
            assignedRoles.Add("인랑");

            // 나머지 인원 랜덤 배정
            for (int i = 1; i < room.Players.Count; i++)
            {
                int randomIndex = random.Next(allRoles.Count);
                assignedRoles.Add(allRoles[randomIndex]);
            }

            // 결과 셔플
            return assignedRoles.OrderBy(x => random.Next()).ToList();
        }

        private void SimulateAIActions(string roomName, string phase)
        {
            Thread.Sleep(5000); // 5초 후 AI 행동 시작

            if (!rooms.ContainsKey(roomName)) return;

            GameRoom room = rooms[roomName];
            Random rnd = new Random();

            if (phase == "Day")
            {
                // AI 투표 시뮬레이션
                var aiPlayers = room.Players.Where(p => p.IsAI && p.IsAlive).ToList();

                foreach (var aiPlayer in aiPlayers)
                {
                    Thread.Sleep(rnd.Next(5000, 25000)); // 5-25초 사이 랜덤 대기

                    if (room.CurrentPhase != "Day") break;

                    // 랜덤한 플레이어에게 투표
                    var allPlayers = room.Players.Where(p => p.IsAlive && p.Name != aiPlayer.Name).ToList();
                    if (allPlayers.Count > 0)
                    {
                        var target = allPlayers[rnd.Next(allPlayers.Count)];
                        room.VoteResults[aiPlayer.Name] = target.Name;

                        string voteMessage = $"{aiPlayer.Name}이(가) {target.Name}에게 투표했습니다.";
                        BroadcastToRoom(roomName, "VOTE_UPDATE:" + voteMessage);
                    }
                }
            }
            else if (phase == "Night")
            {
                // AI 밤 행동 시뮬레이션
                var aiPlayers = room.Players.Where(p => p.IsAI && p.IsAlive).ToList();

                foreach (var aiPlayer in aiPlayers)
                {
                    Thread.Sleep(rnd.Next(3000, 15000)); // 3-15초 사이 랜덤 대기

                    if (room.CurrentPhase != "Night") break;

                    // 역할별 행동
                    switch (aiPlayer.Role)
                    {
                        case "인랑":
                            var victims = room.Players.Where(p => p.IsAlive && p.Role != "인랑").ToList();
                            if (victims.Count > 0)
                            {
                                var target = victims[rnd.Next(victims.Count)];
                                room.NightActions[aiPlayer.Name] = "ATTACK:" + target.Name;
                            }
                            break;

                        case "점쟁이":
                            var suspects = room.Players.Where(p => p.IsAlive && p.Name != aiPlayer.Name).ToList();
                            if (suspects.Count > 0)
                            {
                                var target = suspects[rnd.Next(suspects.Count)];
                                room.NightActions[aiPlayer.Name] = "FORTUNE:" + target.Name;
                            }
                            break;

                        case "사냥꾼":
                            var protectees = room.Players.Where(p => p.IsAlive && p.Name != aiPlayer.Name).ToList();
                            if (protectees.Count > 0)
                            {
                                var target = protectees[rnd.Next(protectees.Count)];
                                room.NightActions[aiPlayer.Name] = "PROTECT:" + target.Name;
                            }
                            break;

                        case "영매":
                            var deadPlayers = room.Players.Where(p => !p.IsAlive).ToList();
                            if (deadPlayers.Count > 0)
                            {
                                var target = deadPlayers[rnd.Next(deadPlayers.Count)];
                                room.NightActions[aiPlayer.Name] = "MEDIUM:" + target.Name;
                            }
                            break;

                        case "요호":
                            var disturbTargets = room.Players.Where(p => p.IsAlive && p.Name != aiPlayer.Name).ToList();
                            if (disturbTargets.Count > 0)
                            {
                                var target = disturbTargets[rnd.Next(disturbTargets.Count)];
                                room.NightActions[aiPlayer.Name] = "DISTURB:" + target.Name;
                            }
                            break;
                    }
                }
            }
        }

        private void HandleChat(int playerId, string content)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                GameRoom room = rooms[roomName];
                Players player = room.GetPlayer(playerId);

                if (player != null && player.IsAlive)
                {
                    string senderName = player.Name;
                    string message = $"CHAT:{senderName}: {content}";
                    BroadcastToRoom(roomName, message);
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

                    // 죽은 플레이어들과 영매에게만 채팅 전송
                    foreach (Players p in room.Players)
                    {
                        if ((!p.IsAlive || p.Role == "영매") && !p.IsAI && writers.ContainsKey(p.Id))
                        {
                            try
                            {
                                writers[p.Id].WriteLine("DEAD_CHAT:" + senderName + ": " + content);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"죽은 플레이어 채팅 전송 실패 ({p.Id}): {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        private void HandleTimeUp(int playerId)
        {
            if (clientRooms.ContainsKey(playerId))
            {
                string roomName = clientRooms[playerId];
                Console.WriteLine($"방 {roomName}에서 시간 종료 신호 받음");
            }
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

                    BroadcastToRoom(roomName, "PLAYER_LEFT:" + playerName);
                    SendPlayerList(roomName);

                    // 방이 비었으면 삭제
                    if (room.GetHumanPlayerCount() == 0)
                    {
                        if (roomTimers.ContainsKey(roomName))
                        {
                            roomTimers[roomName].Stop();
                            roomTimers[roomName].Dispose();
                            roomTimers.Remove(roomName);
                        }

                        rooms.Remove(roomName);
                        Console.WriteLine($"방 {roomName} 삭제됨");
                        BroadcastRoomListToAll();
                    }
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
                    if (player.IsReady) playerInfo += " [준비]";
                    if (!player.IsAlive) playerInfo += " [죽음]";
                    playerNames.Add(playerInfo);
                }

                string playerList = string.Join(",", playerNames.ToArray());
                BroadcastToRoom(roomName, "PLAYER_LIST:" + playerList);
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

        private void BroadcastRoomListToAll()
        {
            List<string> roomNames = new List<string>();
            foreach (string roomName in rooms.Keys)
            {
                roomNames.Add(roomName);
            }

            string roomList = string.Join(",", roomNames.ToArray());
            string message = "ROOM_LIST:" + roomList;

            foreach (int clientId in writers.Keys)
            {
                try
                {
                    writers[clientId].WriteLine(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"방 목록 브로드캐스트 실패 ({clientId}): {ex.Message}");
                }
            }
        }

        private void BroadcastToRoom(string roomName, string message)
        {
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
                            Console.WriteLine($"메시지 전송 실패 ({player.Id}): {ex.Message}");
                        }
                    }
                }
            }
        }

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
                    Console.WriteLine($"클라이언트 {clientId}에게 메시지 전송 실패: {ex.Message}");
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
            return Players.Count(p => !p.IsAI);
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
    }
}
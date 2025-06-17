// MultiPlayGameForm.cs - 완성된 버전
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Linq;

namespace InRang
{
    public partial class MultiPlayGameForm : Form
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread receiveThread;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // 게임 상태
        private string myRole = "";
        private string currentPhase = "Day";
        private List<string> playerList = new List<string>();
        private List<string> alivePlayerList = new List<string>();
        private Dictionary<string, string> playerRoles = new Dictionary<string, string>();
        private Dictionary<string, bool> playerAliveStatus = new Dictionary<string, bool>();
        private Dictionary<string, string> voteResults = new Dictionary<string, string>();
        private Dictionary<string, List<string>> nightActions = new Dictionary<string, List<string>>();

        private bool gameStarted = false;
        private bool isGameEnded = false;

        // 특수 상태
        private bool isProtectedByHunter = false;
        private bool canSeeDeadChat = false; // 영매 능력
        private List<string> deadPlayers = new List<string>();
        private Dictionary<string, string> fortuneTellerResults = new Dictionary<string, string>();

        // 타이머
        private System.Windows.Forms.Timer phaseTimer;
        private int timeRemaining = 60;

        // UI 컨트롤들
        private Panel gamePanel;
        private Label phaseLabel;
        private Label timeLabel;
        private Label roleLabel;
        private RichTextBox chatBox;
        private RichTextBox deadChatBox; // 영매용 죽은 자들의 채팅
        private TextBox messageBox;
        private Button sendButton;
        private ComboBox targetSelector;
        private Button actionButton;
        private Label actionLabel;

        // 플레이어 UI
        private Dictionary<string, PictureBox> playerBoxes = new Dictionary<string, PictureBox>();
        private Dictionary<string, Label> nameLabels = new Dictionary<string, Label>();
        private Dictionary<string, Label> statusLabels = new Dictionary<string, Label>();

        // 이미지 리소스
        private Font font = new Font("Noto Sans KR", 12, FontStyle.Bold);
        private Image playerImage;
        private Image deadOverlay;
        private Image voteOverlay;

        public MultiPlayGameForm(TcpClient tcpClient, StreamReader streamReader, StreamWriter streamWriter)
        {
            InitializeComponent();
            client = tcpClient;
            reader = streamReader;
            writer = streamWriter;

            LoadResources();
            InitializeUI();
            InitializeTimer();

            this.Load += MultiPlayGameForm_Load;
        }

        private void LoadResources()
        {
            string res = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\resources");
            try { playerImage = Image.FromFile(Path.Combine(res, "player.jpg")); } catch { }
            try { deadOverlay = Image.FromFile(Path.Combine(res, "x.jpg")); } catch { }
            try { voteOverlay = Image.FromFile(Path.Combine(res, "vote.jpg")); } catch { }
        }

        private void InitializeUI()
        {
            this.Text = "멀티플레이 인랑 게임";
            this.ClientSize = new Size(1000, 700);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;

            gamePanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Black
            };
            this.Controls.Add(gamePanel);

            // 페이즈 라벨
            phaseLabel = new Label
            {
                Text = "게임 준비 중...",
                Font = new Font("Noto Sans KR", 20, FontStyle.Bold),
                ForeColor = Color.BurlyWood,
                Location = new Point(50, 20),
                AutoSize = true
            };
            gamePanel.Controls.Add(phaseLabel);

            // 시간 라벨
            timeLabel = new Label
            {
                Text = "",
                Font = new Font("Noto Sans KR", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(400, 20),
                AutoSize = true
            };
            gamePanel.Controls.Add(timeLabel);

            // 역할 라벨
            roleLabel = new Label
            {
                Text = "",
                Font = new Font("Noto Sans KR", 14, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Location = new Point(50, 60),
                AutoSize = true
            };
            gamePanel.Controls.Add(roleLabel);

            // 채팅 박스
            chatBox = new RichTextBox
            {
                Location = new Point(20, 100),
                Size = new Size(300, 250),
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Noto Sans KR", 9)
            };
            gamePanel.Controls.Add(chatBox);

            // 죽은 자들 채팅 박스 (영매용)
            deadChatBox = new RichTextBox
            {
                Location = new Point(20, 360),
                Size = new Size(300, 100),
                ReadOnly = true,
                BackColor = Color.FromArgb(50, 20, 20),
                ForeColor = Color.Gray,
                Font = new Font("Noto Sans KR", 8),
                Visible = false
            };
            gamePanel.Controls.Add(deadChatBox);

            // 메시지 입력 박스
            messageBox = new TextBox
            {
                Location = new Point(20, 470),
                Size = new Size(220, 25),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Noto Sans KR", 9)
            };
            messageBox.KeyPress += MessageBox_KeyPress;
            gamePanel.Controls.Add(messageBox);

            // 전송 버튼
            sendButton = new Button
            {
                Text = "Send",
                Location = new Point(250, 470),
                Size = new Size(70, 25),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Noto Sans KR", 9)
            };
            sendButton.Click += SendButton_Click;
            gamePanel.Controls.Add(sendButton);

            // 액션 설명 라벨
            actionLabel = new Label
            {
                Text = "",
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Location = new Point(400, 500),
                AutoSize = true,
                Visible = false
            };
            gamePanel.Controls.Add(actionLabel);

            // 대상 선택 콤보박스
            targetSelector = new ComboBox
            {
                Location = new Point(400, 530),
                Size = new Size(150, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Noto Sans KR", 9)
            };
            gamePanel.Controls.Add(targetSelector);

            // 액션 버튼
            actionButton = new Button
            {
                Text = "Action",
                Location = new Point(560, 530),
                Size = new Size(70, 30),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat,
                Visible = false,
                Font = new Font("Noto Sans KR", 9)
            };
            actionButton.Click += ActionButton_Click;
            gamePanel.Controls.Add(actionButton);
        }

        private void InitializeTimer()
        {
            phaseTimer = new System.Windows.Forms.Timer();
            phaseTimer.Interval = 1000;
            phaseTimer.Tick += PhaseTimer_Tick;
        }

        private void MultiPlayGameForm_Load(object sender, EventArgs e)
        {
            Console.WriteLine("[MultiPlayGameForm] 게임 폼 로드됨");
            SendMessage("GAME_READY");
            StartReceiving();
            SendMessage("REQUEST_PLAYER_LIST");
        }

        private void StartReceiving()
        {
            CancellationToken token = cancellationTokenSource.Token;

            receiveThread = new Thread(() =>
            {
                try
                {
                    while (!token.IsCancellationRequested && !isGameEnded)
                    {
                        if (!client.Connected) break;

                        string msg = reader.ReadLine();
                        if (msg == null) break;

                        this.Invoke((MethodInvoker)(() => HandleServerMessage(msg)));
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
                {
                    Console.WriteLine("[MultiPlayGameForm] 연결 종료됨: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[MultiPlayGameForm] 수신 오류: " + ex.Message);
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void HandleServerMessage(string msg)
        {
            Console.WriteLine("[MultiPlayGameForm] 수신: " + msg);

            if (msg.StartsWith("ROLE:"))
            {
                myRole = msg.Substring("ROLE:".Length);
                roleLabel.Text = $"내 역할: {myRole}";
                AddChatMessage($"[시스템] 당신의 직업은 {myRole}입니다.");
                UpdateUIForRole();
            }
            else if (msg.StartsWith("PLAYER_LIST:"))
            {
                string playerData = msg.Substring("PLAYER_LIST:".Length);
                UpdatePlayerList(playerData);
            }
            else if (msg.StartsWith("GAME_PHASE_START:"))
            {
                string phase = msg.Substring("GAME_PHASE_START:".Length);
                StartPhase(phase);
            }
            else if (msg.StartsWith("CHAT:"))
            {
                string chatMsg = msg.Substring("CHAT:".Length);
                AddChatMessage(chatMsg);
            }
            else if (msg.StartsWith("DEAD_CHAT:"))
            {
                string deadChatMsg = msg.Substring("DEAD_CHAT:".Length);
                AddDeadChatMessage(deadChatMsg);
            }
            else if (msg.StartsWith("VOTE_RESULT:"))
            {
                string result = msg.Substring("VOTE_RESULT:".Length);
                HandleVoteResult(result);
            }
            else if (msg.StartsWith("NIGHT_RESULT:"))
            {
                string result = msg.Substring("NIGHT_RESULT:".Length);
                HandleNightResult(result);
            }
            else if (msg.StartsWith("FORTUNE_RESULT:"))
            {
                string result = msg.Substring("FORTUNE_RESULT:".Length);
                HandleFortuneResult(result);
            }
            else if (msg.StartsWith("PLAYER_DIED:"))
            {
                string deadPlayer = msg.Substring("PLAYER_DIED:".Length);
                HandlePlayerDeath(deadPlayer);
            }
            else if (msg.StartsWith("GAME_END:"))
            {
                string result = msg.Substring("GAME_END:".Length);
                HandleGameEnd(result);
            }
            else if (msg.StartsWith("PROTECTION_INFO:"))
            {
                string info = msg.Substring("PROTECTION_INFO:".Length);
                AddChatMessage($"[보호] {info}");
            }
            else if (msg.StartsWith("GAME_ROLES:"))
            {
                string rolesText = msg.Substring("GAME_ROLES:".Length);
                AddChatMessage("=== 최종 역할 공개 ===");
                AddChatMessage(rolesText);
            }
            else if (msg == "RETURN_TO_LOBBY")
            {
                MessageBox.Show("게임이 종료되어 로비로 돌아갑니다.", "게임 종료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close(); // 혹은 로비 폼으로 전환
            }
        }

        private void UpdateUIForRole()
        {
            // 영매는 죽은 자들의 채팅을 볼 수 있음
            if (myRole == "영매")
            {
                deadChatBox.Visible = true;
                canSeeDeadChat = true;
                AddDeadChatMessage("[시스템] 영매 능력으로 죽은 자들의 대화를 들을 수 있습니다.");
            }
        }

        private void UpdatePlayerList(string playerData)
        {
            if (string.IsNullOrEmpty(playerData)) return;

            string[] players = playerData.Split(',');

            playerList.Clear();
            alivePlayerList.Clear();
            targetSelector.Items.Clear();

            // 기존 UI 정리
            foreach (var kvp in playerBoxes)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }
            foreach (var kvp in nameLabels)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }
            foreach (var kvp in statusLabels)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }

            playerBoxes.Clear();
            nameLabels.Clear();
            statusLabels.Clear();

            // 새 플레이어 UI 생성
            int startX = 400, y = 100, w = 80, h = 80, spacing = 110;

            for (int i = 0; i < players.Length; i++)
            {
                string player = players[i];
                if (string.IsNullOrWhiteSpace(player)) continue;

                string cleanName = player.Trim()
                    .Replace(" [준비]", "")
                    .Replace(" [죽음]", "")
                    .Replace(" [보호됨]", "");

                bool isDead = player.Contains("[죽음]");
                bool isProtected = player.Contains("[보호됨]");

                playerList.Add(cleanName);
                playerAliveStatus[cleanName] = !isDead;

                // 플레이어 이미지
                PictureBox pb = new PictureBox
                {
                    Size = new Size(w, h),
                    Location = new Point(startX + (i % 4) * spacing, y + (i / 4) * spacing),
                    Image = isDead ? deadOverlay : playerImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    BorderStyle = BorderStyle.FixedSingle
                };

                if (isProtected && !isDead)
                {
                    pb.BackColor = Color.Gold; // 보호받는 플레이어 표시
                }

                // 플레이어 이름
                Label nameLabel = new Label
                {
                    Text = cleanName,
                    Location = new Point(pb.Left, pb.Bottom + 5),
                    ForeColor = isDead ? Color.Gray : Color.White,
                    AutoSize = true,
                    Font = new Font("Noto Sans KR", 10, FontStyle.Bold)
                };

                // 상태 라벨
                Label statusLabel = new Label
                {
                    Text = isDead ? "사망" : (isProtected ? "보호됨" : "생존"),
                    Location = new Point(pb.Left, nameLabel.Bottom + 2),
                    ForeColor = isDead ? Color.Red : (isProtected ? Color.Gold : Color.Green),
                    AutoSize = true,
                    Font = new Font("Noto Sans KR", 8)
                };

                gamePanel.Controls.Add(pb);
                gamePanel.Controls.Add(nameLabel);
                gamePanel.Controls.Add(statusLabel);

                playerBoxes[cleanName] = pb;
                nameLabels[cleanName] = nameLabel;
                statusLabels[cleanName] = statusLabel;

                if (!isDead)
                {
                    alivePlayerList.Add(cleanName);
                    if (!cleanName.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSelector.Items.Add(cleanName);
                    }
                }
                else
                {
                    if (!deadPlayers.Contains(cleanName))
                    {
                        deadPlayers.Add(cleanName);
                    }
                }
            }
        }

        private void StartPhase(string phase)
        {
            currentPhase = phase;
            gameStarted = true;

            if (phase == "Day")
            {
                phaseLabel.Text = "낮 (토론 및 투표)";
                phaseLabel.ForeColor = Color.Gold;
                timeRemaining = 180; // 3분

                if (playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                    playerAliveStatus[GameSettings.UserName])
                {
                    ShowActionControls("투표할 대상을 선택하세요", "투표하기");
                }
            }
            else if (phase == "Night")
            {
                phaseLabel.Text = "밤";
                phaseLabel.ForeColor = Color.DarkBlue;
                timeRemaining = 120; // 2분

                if (playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                    playerAliveStatus[GameSettings.UserName] &&
                    IsNightActiveRole())
                {
                    string actionText = GetNightActionText();
                    string labelText = GetNightActionLabel();
                    ShowActionControls(labelText, actionText);
                }
                else
                {
                    HideActionControls();
                }
            }

            timeLabel.Text = $"{timeRemaining}초";
            timeLabel.ForeColor = Color.White;
            phaseTimer.Start();
        }

        private void ShowActionControls(string labelText, string buttonText)
        {
            actionLabel.Text = labelText;
            actionLabel.Visible = true;
            targetSelector.Visible = true;
            actionButton.Text = buttonText;
            actionButton.Visible = true;
        }

        private void HideActionControls()
        {
            actionLabel.Visible = false;
            targetSelector.Visible = false;
            actionButton.Visible = false;
        }

        private bool IsNightActiveRole()
        {
            return myRole == "인랑" || myRole == "점쟁이" || myRole == "영매" ||
                   myRole == "네코마타" || myRole == "요호" || myRole == "사냥꾼";
        }

        private string GetNightActionText()
        {
            switch (myRole)
            {
                case "인랑": return "공격하기";
                case "점쟁이": return "점치기";
                case "영매": return "영혼과 대화";
                case "네코마타": return "능력 사용";
                case "요호": return "교란하기";
                case "사냥꾼": return "보호하기";
                default: return "행동하기";
            }
        }

        private string GetNightActionLabel()
        {
            switch (myRole)
            {
                case "인랑": return "공격할 대상을 선택하세요";
                case "점쟁이": return "점칠 대상을 선택하세요";
                case "영매": return "영혼과 대화할 죽은 자를 선택하세요";
                case "네코마타": return "능력을 사용할 대상을 선택하세요";
                case "요호": return "교란할 대상을 선택하세요";
                case "사냥꾼": return "보호할 대상을 선택하세요";
                default: return "대상을 선택하세요";
            }
        }

        private void PhaseTimer_Tick(object sender, EventArgs e)
        {
            timeRemaining--;
            timeLabel.Text = $"{timeRemaining}초";

            if (timeRemaining <= 0)
            {
                phaseTimer.Stop();
                SendMessage("TIME_UP");
                HideActionControls();
            }
            else if (timeRemaining <= 10)
            {
                timeLabel.ForeColor = Color.Red;
            }
            else if (timeRemaining <= 30)
            {
                timeLabel.ForeColor = Color.Orange;
            }
        }

        private void ActionButton_Click(object sender, EventArgs e)
        {
            if (targetSelector.SelectedItem == null)
            {
                MessageBox.Show("대상을 선택해주세요!", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string target = targetSelector.SelectedItem.ToString();
            string action = GetActionType();
            SendMessage($"ACTION:{action}:{target}");

            HideActionControls();

            string actionText = GetActionDescription(action);
            AddChatMessage($"[내 행동] {target}에게 {actionText}했습니다.");
        }

        private string GetActionType()
        {
            if (currentPhase == "Day") return "VOTE";

            switch (myRole)
            {
                case "인랑": return "ATTACK";
                case "점쟁이": return "FORTUNE";
                case "영매": return "MEDIUM";
                case "네코마타": return "NEKOMATA";
                case "요호": return "DISTURB";
                case "사냥꾼": return "PROTECT";
                default: return "ACTION";
            }
        }

        private string GetActionDescription(string action)
        {
            switch (action)
            {
                case "VOTE": return "투표";
                case "ATTACK": return "공격";
                case "FORTUNE": return "점술";
                case "MEDIUM": return "영혼 대화";
                case "NEKOMATA": return "네코마타 능력";
                case "DISTURB": return "교란";
                case "PROTECT": return "보호";
                default: return "행동";
            }
        }

        private void HandleVoteResult(string result)
        {
            AddChatMessage($"[투표 결과] {result}");
        }

        private void HandleNightResult(string result)
        {
            AddChatMessage($"[밤 결과] {result}");
        }

        private void HandleFortuneResult(string result)
        {
            if (myRole == "점쟁이")
            {
                AddChatMessage($"[점술 결과] {result}");
                string[] parts = result.Split(':');
                if (parts.Length == 2)
                {
                    fortuneTellerResults[parts[0]] = parts[1];
                }
            }
        }

        private void HandlePlayerDeath(string deadPlayer)
        {
            AddChatMessage($"[사망] {deadPlayer}님이 사망했습니다.");

            if (playerBoxes.ContainsKey(deadPlayer))
            {
                playerBoxes[deadPlayer].Image = deadOverlay;
                nameLabels[deadPlayer].ForeColor = Color.Gray;
                statusLabels[deadPlayer].Text = "사망";
                statusLabels[deadPlayer].ForeColor = Color.Red;
            }

            // 자신이 죽었다면
            if (deadPlayer.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
            {
                phaseLabel.Text += " (사망)";
                phaseLabel.ForeColor = Color.Red;
                HideActionControls();
                sendButton.Enabled = false;
                messageBox.Enabled = false;

                // 광인이나 인랑이 죽으면 죽은 자들과 채팅 가능
                if (myRole == "광인" || myRole == "인랑")
                {
                    deadChatBox.Visible = true;
                    AddDeadChatMessage("[시스템] 사망하여 죽은 자들과 대화할 수 있습니다.");
                }
            }

            SendMessage("REQUEST_PLAYER_LIST");
        }

        private void HandleGameEnd(string result)
        {
            isGameEnded = true;
            phaseTimer.Stop();
            HideActionControls();
            sendButton.Enabled = false;
            messageBox.Enabled = false;

            phaseLabel.Text = "게임 종료";
            phaseLabel.ForeColor = Color.Red;

            AddChatMessage("=== 게임 종료 ===");
            AddChatMessage(result);

            // 모든 플레이어의 역할 공개
            AddChatMessage("=== 역할 공개 ===");
            foreach (var player in playerList)
            {
                if (playerRoles.ContainsKey(player))
                {
                    AddChatMessage($"{player}: {playerRoles[player]}");
                }
            }

            MessageBox.Show($"게임이 종료되었습니다!\n\n{result}", "게임 종료",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            SendChat();
        }

        private void MessageBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                SendChat();
                e.Handled = true;
            }
        }

        private void SendChat()
        {
            string message = messageBox.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                // 죽은 플레이어는 죽은 자들끼리만 채팅 가능
                bool isDead = playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                             !playerAliveStatus[GameSettings.UserName];

                if (isDead)
                {
                    SendMessage("DEAD_CHAT:" + message);
                }
                else
                {
                    SendMessage("CHAT:" + message);
                }
                messageBox.Clear();
            }
        }

        private void AddChatMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            chatBox.AppendText($"[{timestamp}] {message}\n");
            chatBox.ScrollToCaret();
        }

        private void AddDeadChatMessage(string message)
        {
            if (canSeeDeadChat ||
                (playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                 !playerAliveStatus[GameSettings.UserName]))
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                deadChatBox.AppendText($"[{timestamp}] {message}\n");
                deadChatBox.ScrollToCaret();
            }
        }

        private void SendMessage(string message)
        {
            try
            {
                writer.WriteLine(message);
                Console.WriteLine("[MultiPlayGameForm] 송신: " + message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiPlayGameForm] 송신 실패: " + ex.Message);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            try
            {
                isGameEnded = true;
                phaseTimer?.Stop();
                phaseTimer?.Dispose();

                cancellationTokenSource.Cancel();
                receiveThread?.Join(1000);

                SendMessage("LEAVE_ROOM");

                playerImage?.Dispose();
                deadOverlay?.Dispose();
                voteOverlay?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiPlayGameForm] 종료 오류: " + ex.Message);
            }
        }
    }


    public enum GamePhase
    {
        Introduction,
        Day,
        DayResult,
        Night,
        NightResult,
        GameEnd
    }
}
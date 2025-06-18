// MultiPlayGameForm.cs - 접근자 수정된 완전한 멀티플레이 구현
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
        // 네트워크 관련
        public TcpClient client;
        public StreamReader reader;
        public StreamWriter writer;
        public Thread receiveThread;
        public CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // 게임 상태 관리
        public string myRole = "";
        public string currentPhase = "Day";
        public List<string> playerList = new List<string>();
        public Dictionary<string, bool> playerAliveStatus = new Dictionary<string, bool>();
        public bool hasVoted = false;
        public bool hasActed = false;
        public bool gameStarted = false;
        public bool isGameEnded = false;
        public int currentDay = 1;
        public bool uiInitialized = false;

        // 타이머 관리
        public System.Windows.Forms.Timer phaseTimer;
        public System.Windows.Forms.Timer uiUpdateTimer;
        public int timeRemaining = 50;
        public int maxPhaseTime = 50;

        // 글꼴 설정
        public Font titleFont;
        public Font roleFont;
        public Font timerFont;
        public Font descFont;
        public Font phaseFont;
        public Font nameFont;

        // UI 컨트롤들
        public Panel gamePanel;
        public Label phaseLabel;
        public Label timeLabel;
        public Label systemMsgLabel;
        public Label roleInfoLabel;

        // 채팅 관련
        public Panel chatPanel;
        public RichTextBox chatBox;
        public TextBox messageBox;
        public Button sendButton;
        public Label chatLabel;

        // 게임 액션 관련
        public ComboBox playerSelectionBox;
        public Button actionButton;
        public Button voteButton;
        public Label selectionLabel;

        // 플레이어 UI
        public List<PictureBox> playerBoxes = new List<PictureBox>();
        public List<Label> playerNameLabels = new List<Label>();

        // 이미지 리소스
        public Image playerImage;
        public Image deadOverlay;

        public MultiPlayGameForm(TcpClient tcpClient, StreamReader streamReader, StreamWriter streamWriter)
        {
            InitializeComponent();
            client = tcpClient;
            reader = streamReader;
            writer = streamWriter;

            InitializeForm();
            LoadResources();
            InitializeGameScreen();
            InitializeTimers();

            this.Load += MultiPlayGameForm_Load;
        }

        public void InitializeForm()
        {
            this.Text = "인랑 게임 - 멀티플레이";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 글꼴 설정
            titleFont = new Font("Noto Sans KR", 32, FontStyle.Bold);
            roleFont = new Font("Noto Sans KR", 20, FontStyle.Bold);
            timerFont = new Font("Noto Sans KR", 24, FontStyle.Bold);
            descFont = new Font("Noto Sans KR", 12, FontStyle.Regular);
            phaseFont = new Font("Noto Sans KR", 28, FontStyle.Bold);
            nameFont = new Font("Noto Sans KR", 10, FontStyle.Bold);
        }

        public void LoadResources()
        {
            string res = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\resources");

            try
            {
                playerImage = Image.FromFile(Path.Combine(res, "player.jpg"));
            }
            catch
            {
                playerImage = new Bitmap(70, 70);
                using (Graphics g = Graphics.FromImage(playerImage))
                {
                    g.FillRectangle(Brushes.LightBlue, 0, 0, 70, 70);
                    g.DrawRectangle(Pens.Black, 0, 0, 69, 69);
                }
            }

            try
            {
                deadOverlay = Image.FromFile(Path.Combine(res, "x.jpg"));
            }
            catch
            {
                deadOverlay = new Bitmap(70, 70);
                using (Graphics g = Graphics.FromImage(deadOverlay))
                {
                    g.Clear(Color.Transparent);
                    using (Pen p = new Pen(Color.Red, 5))
                    {
                        g.DrawLine(p, 10, 10, 60, 60);
                        g.DrawLine(p, 60, 10, 10, 60);
                    }
                }
            }
        }

        public void InitializeGameScreen()
        {
            gamePanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Black,
                Visible = true
            };

            // 상단 게임 정보
            phaseLabel = new Label
            {
                Text = "day 1",
                Font = phaseFont,
                ForeColor = Color.Gold,
                Location = new Point(150, 20),
                Size = new Size(200, 40),
                AutoSize = true
            };

            timeLabel = new Label
            {
                Text = "50",
                Font = timerFont,
                ForeColor = Color.White,
                Location = new Point(350, 20),
                Size = new Size(100, 40),
                AutoSize = true
            };

            // 역할 정보 표시
            roleInfoLabel = new Label
            {
                Text = "역할: 확인 중...",
                Font = roleFont,
                ForeColor = Color.Cyan,
                Location = new Point(500, 20),
                Size = new Size(200, 30),
                AutoSize = true
            };

            // 시스템 메시지
            systemMsgLabel = new Label
            {
                Text = "게임이 시작되었습니다. 낮 토론을 시작하세요.",
                Font = descFont,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(430, 70),
                Size = new Size(300, 70),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 채팅 시스템
            SetupChatSystem();

            // 게임 액션 UI
            SetupGameActionUI();

            // 플레이어 상태 UI
            CreatePlayerStatusUI();

            // 게임 패널에 컴포넌트 추가
            gamePanel.Controls.AddRange(new Control[] {
                phaseLabel, timeLabel, roleInfoLabel, systemMsgLabel,
                chatPanel, chatLabel, voteButton, selectionLabel,
                playerSelectionBox, actionButton
            });

            this.Controls.Add(gamePanel);
            uiInitialized = true;
        }

        public void SetupChatSystem()
        {
            chatPanel = new Panel
            {
                Location = new Point(30, 150),
                Size = new Size(270, 330),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(20, 20, 20)
            };

            chatBox = new RichTextBox
            {
                Location = new Point(10, 10),
                Size = new Size(250, 270),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Noto Sans KR", 9)
            };

            messageBox = new TextBox
            {
                Location = new Point(10, 290),
                Size = new Size(170, 25),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Noto Sans KR", 10)
            };

            sendButton = new Button
            {
                Text = "send",
                Location = new Point(190, 290),
                Size = new Size(70, 25),
                BackColor = Color.BurlyWood,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 9)
            };

            chatLabel = new Label
            {
                Text = "전체 공개 채팅방",
                Location = new Point(30, 130),
                Size = new Size(270, 20),
                ForeColor = Color.BurlyWood,
                Font = new Font("Noto Sans KR", 10),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 이벤트 연결
            sendButton.Click += SendButton_Click;
            messageBox.KeyPress += MessageBox_KeyPress;

            chatPanel.Controls.AddRange(new Control[] { chatBox, messageBox, sendButton });
        }

        public void SetupGameActionUI()
        {
            // 플레이어 선택 UI
            selectionLabel = new Label
            {
                Text = "플레이어 선택",
                Location = new Point(500, 180),
                Size = new Size(150, 25),
                ForeColor = Color.White,
                Font = new Font("Noto Sans KR", 12),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            playerSelectionBox = new ComboBox
            {
                Location = new Point(500, 210),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };

            // 행동 버튼
            actionButton = new Button
            {
                Text = "능력사용",
                Location = new Point(540, 240),
                Size = new Size(70, 30),
                BackColor = Color.Purple,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Visible = false
            };

            // 투표 버튼
            voteButton = new Button
            {
                Text = "vote",
                Location = new Point(540, 480),
                Size = new Size(70, 30),
                BackColor = Color.BurlyWood,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 11, FontStyle.Bold),
                Visible = true
            };

            // 이벤트 연결
            voteButton.Click += VoteButton_Click;
            actionButton.Click += ActionButton_Click;
        }

        public void CreatePlayerStatusUI()
        {
            int playerImageSize = 70;
            int spacing = 30;
            int startX = 350;
            int startY = 180;
            int playersPerRow = 4;

            for (int i = 0; i < 8; i++)
            {
                int row = i / playersPerRow;
                int col = i % playersPerRow;
                int x = startX + col * (playerImageSize + spacing);
                int y = startY + row * (playerImageSize + spacing + 20);

                PictureBox playerBox = new PictureBox
                {
                    Location = new Point(x, y),
                    Size = new Size(playerImageSize, playerImageSize),
                    Image = playerImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Tag = i,
                    BorderStyle = BorderStyle.FixedSingle,
                    Visible = true
                };

                Label playerName = new Label
                {
                    Text = $"player {i + 1}",
                    Location = new Point(x, y + playerImageSize + 5),
                    Size = new Size(playerImageSize, 15),
                    ForeColor = Color.BurlyWood,
                    Font = nameFont,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Visible = true
                };

                playerBoxes.Add(playerBox);
                playerNameLabels.Add(playerName);

                gamePanel.Controls.Add(playerBox);
                gamePanel.Controls.Add(playerName);
            }
        }

        public void InitializeTimers()
        {
            // 메인 페이즈 타이머
            phaseTimer = new System.Windows.Forms.Timer();
            phaseTimer.Interval = 1000;
            phaseTimer.Tick += PhaseTimer_Tick;

            // UI 업데이트 타이머
            uiUpdateTimer = new System.Windows.Forms.Timer();
            uiUpdateTimer.Interval = 100; // 0.1초마다 UI 업데이트
            uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            uiUpdateTimer.Start();
        }

        public void MultiPlayGameForm_Load(object sender, EventArgs e)
        {
            Console.WriteLine("[MultiPlayGameForm] 게임 폼 로드됨");

            // 서버에 연결 확인
            SendMessage("GAME_READY");

            // 수신 스레드 시작
            StartReceiving();

            // 기본 생존 상태 설정
            if (!string.IsNullOrEmpty(GameSettings.UserName))
            {
                playerAliveStatus[GameSettings.UserName] = true;
            }

            // 낮 페이즈로 시작
            StartDayPhase();
        }

        public void StartReceiving()
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

        public void HandleServerMessage(string msg)
        {
            Console.WriteLine("[MultiPlayGameForm] 수신: " + msg);

            if (!uiInitialized) return;

            try
            {
                if (msg.StartsWith("ROLE:"))
                {
                    HandleRoleAssignment(msg);
                }
                else if (msg.StartsWith("PLAYER_LIST:"))
                {
                    HandlePlayerListUpdate(msg);
                }
                else if (msg.StartsWith("GAME_PHASE_START:"))
                {
                    HandlePhaseStart(msg);
                }
                else if (msg.StartsWith("PHASE_TIME:"))
                {
                    HandlePhaseTime(msg);
                }
                else if (msg.StartsWith("CHAT:"))
                {
                    HandleChatMessage(msg);
                }
                else if (msg.StartsWith("VOTE_RESULT:"))
                {
                    HandleVoteResult(msg);
                }
                else if (msg.StartsWith("NIGHT_RESULT:"))
                {
                    HandleNightResult(msg);
                }
                else if (msg.StartsWith("PLAYER_DIED:"))
                {
                    HandlePlayerDeath(msg);
                }
                else if (msg.StartsWith("GAME_END:"))
                {
                    HandleGameEnd(msg);
                }
                else if (msg == "ACTION_CONFIRMED")
                {
                    HandleActionConfirmed();
                }
                else if (msg == "VOTE_CONFIRMED")
                {
                    HandleVoteConfirmed();
                }
                else if (msg.StartsWith("PHASE_TRANSITION:"))
                {
                    // 페이즈 전환 명령 처리
                    string nextPhase = msg.Substring("PHASE_TRANSITION:".Length);
                    HandlePhaseTransition(nextPhase);
                }
                else if (msg == "START_NIGHT")
                {
                    // 강제 밤 시작
                    StartNightPhase(40);
                }
                else if (msg == "START_DAY")
                {
                    // 강제 낮 시작
                    StartDayPhase(50);
                }
                else if (msg.StartsWith("TIME_SYNC:"))
                {
                    // 시간 동기화
                    string timeStr = msg.Substring("TIME_SYNC:".Length);
                    if (int.TryParse(timeStr, out int syncTime))
                    {
                        timeRemaining = syncTime;
                        Console.WriteLine($"[시간 동기화] {timeRemaining}초");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiPlayGameForm] 메시지 처리 오류: " + ex.Message);
            }
        }

        public void HandlePhaseTransition(string nextPhase)
        {
            Console.WriteLine($"[페이즈 전환] {currentPhase} -> {nextPhase}");

            if (nextPhase != "Day" && nextPhase != "Night")
            {
                Console.WriteLine($"[경고] 잘못된 페이즈 요청: {nextPhase}");
                return;
            }

            if (nextPhase == "Night")
            {
                StartNightPhase();
            }
            else if (nextPhase == "Day")
            {
                StartDayPhase();
            }
        }

        public void HandleRoleAssignment(string msg)
        {
            if (string.IsNullOrEmpty(myRole))
            {
                myRole = msg.Substring("ROLE:".Length);
                roleInfoLabel.Text = $"역할: {myRole}";
                AddChatMessage("System", $"당신의 직업은 {myRole}입니다.");

                if (!gameStarted)
                {
                    gameStarted = true;
                    StartDayPhase();
                }
            }
        }

        public void HandlePlayerListUpdate(string msg)
        {
            string playerData = msg.Substring("PLAYER_LIST:".Length);
            UpdatePlayerList(playerData);
        }

        public void HandlePhaseStart(string msg)
        {
            string phaseData = msg.Substring("GAME_PHASE_START:".Length);
            var parts = phaseData.Split(':');

            if (!int.TryParse(parts[1], out int time))
            {
                Console.WriteLine($"[오류] 시간 값이 잘못되었습니다: {parts[1]}");
                return;
            }

            if (parts.Length >= 2)
            {
                string phase = parts[0];
                int times = int.Parse(parts[1]);

                if (phase == "Day")
                {
                    StartDayPhase(times);
                }
                else if (phase == "Night")
                {
                    StartNightPhase(times);
                }
            }
        }

        public void HandlePhaseTime(string msg)
        {
            string timeStr = msg.Substring("PHASE_TIME:".Length);
            if (int.TryParse(timeStr, out int time))
            {
                timeRemaining = time;
            }
        }

        public void HandleChatMessage(string msg)
        {
            string chatMsg = msg.Substring("CHAT:".Length);
            var parts = chatMsg.Split(new char[] { ':' }, 2);

            if (parts.Length == 2)
            {
                AddChatMessage(parts[0], parts[1]);
            }
            else
            {
                AddChatMessage("", chatMsg);
            }
        }

        public void HandleVoteResult(string msg)
        {
            string result = msg.Substring("VOTE_RESULT:".Length);
            AddChatMessage("System", "[투표 결과] " + result);

            // 투표 버튼 리셋
            ResetVoteButton();

            // 투표 결과 후 자동으로 밤으로 전환
            systemMsgLabel.Text = "투표가 완료되었습니다. 밤이 시작됩니다.";

            // 3초 후 밤으로 전환
            System.Windows.Forms.Timer transitionTimer = new System.Windows.Forms.Timer();
            transitionTimer.Interval = 3000;
            transitionTimer.Tick += (s, e) => {
                transitionTimer.Stop();
                transitionTimer.Dispose();

                Console.WriteLine("[자동 전환] 투표 결과 후 밤으로 전환");
                StartNightPhase(40);
            };
            transitionTimer.Start();
        }

        public void HandleNightResult(string msg)
        {
            string result = msg.Substring("NIGHT_RESULT:".Length);
            AddChatMessage("System", "[밤 결과] " + result);

            // 밤 행동 UI 리셋
            ResetNightActions();

            // 밤 결과 후 자동으로 낮으로 전환
            systemMsgLabel.Text = "밤이 끝났습니다. 새로운 낮이 시작됩니다.";

            // 3초 후 낮으로 전환
            System.Windows.Forms.Timer transitionTimer = new System.Windows.Forms.Timer();
            transitionTimer.Interval = 3000;
            transitionTimer.Tick += (s, e) => {
                transitionTimer.Stop();
                transitionTimer.Dispose();

                Console.WriteLine("[자동 전환] 밤 결과 후 낮으로 전환");
                currentDay++; // 날짜 증가
                StartDayPhase(50);
            };
            transitionTimer.Start();
        }

        public void HandlePlayerDeath(string msg)
        {
            string deadPlayer = msg.Substring("PLAYER_DIED:".Length);
            AddChatMessage("System", $"{deadPlayer}님이 사망했습니다.");

            if (playerAliveStatus.ContainsKey(deadPlayer))
            {
                playerAliveStatus[deadPlayer] = false;
            }

            // 자신이 죽었다면 UI 비활성화
            if (deadPlayer.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
            {
                DisablePlayerUI();
            }

            UpdatePlayerVisuals();
        }

        public void HandleGameEnd(string msg)
        {
            string result = msg.Substring("GAME_END:".Length);
            EndGame(result);
        }

        public void HandleActionConfirmed()
        {
            hasActed = true;
            if (actionButton.Visible)
            {
                actionButton.Text = "완료";
                actionButton.Enabled = false;
                actionButton.BackColor = Color.Gray;
            }
            AddChatMessage("System", "행동이 접수되었습니다.");
        }

        public void HandleVoteConfirmed()
        {
            hasVoted = true;
            if (voteButton.Visible)
            {
                voteButton.Text = "투표완료";
                voteButton.Enabled = false;
                voteButton.BackColor = Color.Gray;
            }
            AddChatMessage("System", "투표가 완료되었습니다.");
        }

        // 페이즈 관리
        public void StartDayPhase(int time = 50)
        {
            if (phaseTimer != null)
            {
                phaseTimer.Stop();
                phaseTimer.Dispose();
            }

            currentPhase = "Day";
            timeRemaining = time;
            maxPhaseTime = time;

            phaseLabel.Text = $"day {currentDay}";
            phaseLabel.ForeColor = Color.Gold;
            systemMsgLabel.Text = "낮이 되었습니다. 토론을 시작하세요.";

            // UI 활성화
            EnableDayUI();

            // 타이머 시작
            phaseTimer.Start();

            AddChatMessage("System", $"=== Day {currentDay} 시작 ===");
        }

        public void StartNightPhase(int time = 40)
        {
            if (phaseTimer != null)
            {
                phaseTimer.Stop();
                phaseTimer.Dispose();
            }

            currentPhase = "Night";
            timeRemaining = time;
            maxPhaseTime = time;

            phaseLabel.Text = "night";
            phaseLabel.ForeColor = Color.DarkBlue;
            systemMsgLabel.Text = "밤이 되었습니다. 능력을 사용하세요.";

            // UI 전환
            EnableNightUI();

            // 타이머 시작
            phaseTimer.Start();

            AddChatMessage("System", "=== Night 시작 ===");
        }

        public void EnableDayUI()
        {
            Console.WriteLine("[UI 전환] 낮 UI 활성화");

            bool isAlive = IsPlayerAlive(GameSettings.UserName);

            // 투표 버튼 활성화
            voteButton.Visible = isAlive;
            voteButton.Enabled = isAlive && !hasVoted;
            voteButton.Text = "vote";
            voteButton.BackColor = Color.BurlyWood;

            if (isAlive)
            {
                AddChatMessage("System", "투표를 통해 의심스러운 플레이어를 처형할 수 있습니다.");
            }

            // 채팅 활성화
            messageBox.Enabled = isAlive;
            sendButton.Enabled = isAlive;
            chatLabel.Text = "전체 공개 채팅방";
            chatLabel.ForeColor = Color.BurlyWood;

            // 밤 UI 숨기기
            HideNightActionControls();

            // 상태 리셋
            hasVoted = false;
        }

        public void EnableNightUI()
        {
            Console.WriteLine("[UI 전환] 밤 UI 활성화");

            bool isAlive = IsPlayerAlive(GameSettings.UserName);

            // 투표 버튼 숨기기
            voteButton.Visible = false;

            // 밤 능력 UI
            if (isAlive && IsNightActiveRole())
            {
                ShowNightActionControls();
                AddChatMessage("System", $"당신의 능력을 사용할 수 있습니다. ({GetNightActionText()})");
            }
            else
            {
                HideNightActionControls();
                if (!isAlive)
                {
                    AddChatMessage("System", "사망한 플레이어는 밤 행동을 할 수 없습니다.");
                }
                else
                {
                    AddChatMessage("System", "밤에 특별한 능력이 없습니다. 결과를 기다려주세요.");
                }
            }

            // 채팅 설정 (인랑만 가능)
            bool canChatAtNight = isAlive && myRole == "인랑";
            messageBox.Enabled = canChatAtNight;
            sendButton.Enabled = canChatAtNight;

            if (canChatAtNight)
            {
                chatLabel.Text = "인랑 전용 채팅방";
                chatLabel.ForeColor = Color.Red;
                AddChatMessage("System", "인랑 동료들과 대화할 수 있습니다.");
            }
            else
            {
                chatLabel.Text = "밤에는 채팅이 제한됩니다";
                chatLabel.ForeColor = Color.Gray;
            }

            // 상태 리셋
            hasActed = false;
        }

        public void ShowNightActionControls()
        {
            string actionText = GetNightActionText();
            string labelText = GetNightActionLabel();

            selectionLabel.Text = labelText;
            selectionLabel.Visible = true;
            playerSelectionBox.Visible = true;
            actionButton.Text = actionText;
            actionButton.Enabled = true;
            actionButton.Visible = true;
            actionButton.BackColor = Color.Purple;

            UpdatePlayerSelectionList();
        }

        public void HideNightActionControls()
        {
            selectionLabel.Visible = false;
            playerSelectionBox.Visible = false;
            actionButton.Visible = false;
        }

        public string GetNightActionText()
        {
            switch (myRole)
            {
                case "인랑": return "습격";
                case "점쟁이": return "점술";
                case "영매": return "영매";
                case "사냥꾼": return "보호";
                default: return "능력";
            }
        }

        public string GetNightActionLabel()
        {
            switch (myRole)
            {
                case "인랑": return "습격할 대상 선택";
                case "점쟁이": return "점을 볼 대상 선택";
                case "영매": return "영매 능력 사용";
                case "사냥꾼": return "보호할 대상 선택";
                default: return "대상을 선택하세요";
            }
        }

        public bool IsNightActiveRole()
        {
            return myRole == "인랑" || myRole == "점쟁이" || myRole == "영매" ||
                   myRole == "사냥꾼" || myRole == "네코마타";
        }

        public bool IsPlayerAlive(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return true;

            if (!playerAliveStatus.ContainsKey(playerName))
            {
                playerAliveStatus[playerName] = true;
                return true;
            }

            return playerAliveStatus[playerName];
        }

        // 타이머 이벤트
        public void PhaseTimer_Tick(object sender, EventArgs e)
        {
            timeRemaining--;

            if (timeRemaining <= 0)
            {
                phaseTimer.Stop();
                HandlePhaseEnd();
            }
        }

        public void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateTimeDisplay();
            UpdatePlayerVisuals();
        }

        public void HandlePhaseEnd()
        {
            Console.WriteLine($"[페이즈 종료] {currentPhase} 시간 종료");

            if (currentPhase == "Day")
            {
                AddChatMessage("System", "낮 시간이 종료되었습니다.");
                SendMessage("PHASE_END:Day");
                systemMsgLabel.Text = "투표 집계 중...";

                // 타이머 종료 후 자동으로 밤으로 전환
                System.Windows.Forms.Timer autoNightTimer = new System.Windows.Forms.Timer();
                autoNightTimer.Interval = 5000; // 5초 후
                autoNightTimer.Tick += (s, e) => {
                    autoNightTimer.Stop();
                    autoNightTimer.Dispose();

                    Console.WriteLine("[자동 전환] 낮 시간 종료 후 밤으로 전환");
                    StartNightPhase(40);
                };
                autoNightTimer.Start();
            }
            else if (currentPhase == "Night")
            {
                AddChatMessage("System", "밤 시간이 종료되었습니다.");
                SendMessage("PHASE_END:Night");
                systemMsgLabel.Text = "밤 결과 처리 중...";
                HideNightActionControls();

                // 타이머 종료 후 자동으로 낮으로 전환
                System.Windows.Forms.Timer autoDayTimer = new System.Windows.Forms.Timer();
                autoDayTimer.Interval = 5000; // 5초 후
                autoDayTimer.Tick += (s, e) => {
                    autoDayTimer.Stop();
                    autoDayTimer.Dispose();

                    Console.WriteLine("[자동 전환] 밤 시간 종료 후 낮으로 전환");
                    currentDay++; // 날짜 증가
                    StartDayPhase(50);
                };
                autoDayTimer.Start();
            }
        }

        public void UpdateTimeDisplay()
        {
            if (timeLabel == null) return;

            timeLabel.Text = timeRemaining.ToString();

            if (timeRemaining <= 10)
                timeLabel.ForeColor = Color.Red;
            else if (timeRemaining <= 30)
                timeLabel.ForeColor = Color.Orange;
            else
                timeLabel.ForeColor = Color.White;
        }

        public void UpdatePlayerList(string playerData)
        {
            if (string.IsNullOrEmpty(playerData)) return;

            string[] players = playerData.Split(',');
            playerList.Clear();
            playerAliveStatus.Clear();

            for (int i = 0; i < players.Length && i < playerBoxes.Count; i++)
            {
                string player = players[i];
                if (string.IsNullOrWhiteSpace(player)) continue;

                string cleanName = player.Trim()
                    .Replace(" [준비]", "")
                    .Replace(" [죽음]", "")
                    .Replace(" [보호됨]", "");

                bool isDead = player.Contains("[죽음]");

                playerList.Add(cleanName);
                playerAliveStatus[cleanName] = !isDead;

                playerBoxes[i].Visible = true;
                playerNameLabels[i].Visible = true;

                // 자신인지 확인
                if (cleanName.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    playerNameLabels[i].Text = $"{cleanName} (나)";
                    playerNameLabels[i].ForeColor = Color.Yellow;
                    playerBoxes[i].BorderStyle = BorderStyle.Fixed3D;
                }
                else
                {
                    playerNameLabels[i].Text = cleanName;
                    playerNameLabels[i].ForeColor = isDead ? Color.Gray : Color.BurlyWood;
                    playerBoxes[i].BorderStyle = BorderStyle.FixedSingle;
                }
            }

            UpdatePlayerSelectionList();
        }

        public void UpdatePlayerVisuals()
        {
            for (int i = 0; i < playerList.Count && i < playerBoxes.Count; i++)
            {
                string playerName = playerList[i];
                bool isDead = !IsPlayerAlive(playerName);

                if (isDead)
                {
                    // 사망 상태 표시
                    Bitmap composite = new Bitmap(playerBoxes[i].Width, playerBoxes[i].Height);
                    using (Graphics g = Graphics.FromImage(composite))
                    {
                        g.DrawImage(playerImage, 0, 0, playerBoxes[i].Width, playerBoxes[i].Height);
                        g.DrawImage(deadOverlay, 0, 0, playerBoxes[i].Width, playerBoxes[i].Height);
                    }
                    playerBoxes[i].Image = composite;
                }
                else
                {
                    playerBoxes[i].Image = playerImage;
                }
            }
        }

        public void UpdatePlayerSelectionList()
        {
            if (playerSelectionBox == null) return;

            playerSelectionBox.Items.Clear();

            foreach (var player in playerList)
            {
                if (IsPlayerAlive(player) && !player.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    playerSelectionBox.Items.Add(player);
                }
            }

            if (playerSelectionBox.Items.Count > 0)
            {
                playerSelectionBox.SelectedIndex = 0;
            }
        }

        // 버튼 이벤트 처리
        public void VoteButton_Click(object sender, EventArgs e)
        {
            if (hasVoted)
            {
                MessageBox.Show("이미 투표하셨습니다!", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (currentPhase != "Day")
            {
                MessageBox.Show("투표는 낮에만 가능합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowVoteDialog();
        }

        public void ActionButton_Click(object sender, EventArgs e)
        {
            if (hasActed)
            {
                MessageBox.Show("이미 행동하셨습니다!", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (currentPhase != "Night")
            {
                MessageBox.Show("능력은 밤에만 사용할 수 있습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (playerSelectionBox.SelectedItem == null)
            {
                MessageBox.Show("대상을 선택해주세요!", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string target = playerSelectionBox.SelectedItem.ToString();
            string action = GetActionType();

            SendMessage($"ACTION:{action}:{target}");

            hasActed = true;
            actionButton.Text = "대기중";
            actionButton.Enabled = false;
            actionButton.BackColor = Color.Gray;

            AddChatMessage("System", $"{target}에게 {GetActionDescription(action)} 사용했습니다.");
        }

        public string GetActionType()
        {
            switch (myRole)
            {
                case "인랑": return "ATTACK";
                case "점쟁이": return "FORTUNE";
                case "영매": return "MEDIUM";
                case "사냥꾼": return "PROTECT";
                default: return "ACTION";
            }
        }

        public string GetActionDescription(string action)
        {
            switch (action)
            {
                case "ATTACK": return "습격";
                case "FORTUNE": return "점술";
                case "MEDIUM": return "영매 능력";
                case "PROTECT": return "보호";
                default: return "능력";
            }
        }

        public void ShowVoteDialog()
        {
            Form voteForm = new Form
            {
                Text = "투표",
                Size = new Size(320, 220),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White
            };

            Label label = new Label
            {
                Text = "처형할 플레이어를 선택하세요:",
                Location = new Point(20, 20),
                Size = new Size(280, 20),
                ForeColor = Color.White,
                Font = new Font("Noto Sans KR", 10)
            };

            ComboBox comboBox = new ComboBox
            {
                Location = new Point(20, 50),
                Size = new Size(270, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Noto Sans KR", 10)
            };

            Button voteBtn = new Button
            {
                Text = "투표하기",
                Location = new Point(110, 120),
                Size = new Size(100, 35),
                BackColor = Color.BurlyWood,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Noto Sans KR", 10, FontStyle.Bold)
            };

            // 투표 가능한 플레이어 목록 설정
            List<string> voteTargets = new List<string>();
            foreach (var player in playerList)
            {
                if (IsPlayerAlive(player) && !player.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    voteTargets.Add(player);
                }
            }

            // 테스트용 더미 데이터
            if (voteTargets.Count == 0)
            {
                voteTargets.AddRange(new[] { "AI_1", "AI_2", "AI_3", "AI_4" });
            }

            foreach (string target in voteTargets)
            {
                comboBox.Items.Add(target);
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }

            // 투표 이벤트
            voteBtn.Click += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    string selectedName = comboBox.SelectedItem.ToString();
                    SendMessage($"ACTION:VOTE:{selectedName}");

                    hasVoted = true;
                    voteButton.Text = "대기중";
                    voteButton.Enabled = false;
                    voteButton.BackColor = Color.Gray;

                    AddChatMessage("System", $"{selectedName}에게 투표했습니다.");
                    voteForm.Close();
                }
            };

            voteForm.Controls.AddRange(new Control[] { label, comboBox, voteBtn });
            voteForm.ShowDialog();
        }

        // 채팅 관련
        public void SendButton_Click(object sender, EventArgs e)
        {
            SendChat();
        }

        public void MessageBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                SendChat();
                e.Handled = true;
            }
        }

        public void SendChat()
        {
            string message = messageBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            bool canChat = CanPlayerChat();

            if (canChat)
            {
                SendMessage("CHAT:" + message);
                messageBox.Clear();
            }
            else
            {
                if (!IsPlayerAlive(GameSettings.UserName))
                {
                    AddChatMessage("System", "사망한 플레이어는 채팅할 수 없습니다.");
                }
                else if (currentPhase == "Night" && myRole != "인랑")
                {
                    AddChatMessage("System", "밤에는 인랑만 채팅할 수 있습니다.");
                }
                messageBox.Clear();
            }
        }

        public bool CanPlayerChat()
        {
            bool isAlive = IsPlayerAlive(GameSettings.UserName);

            if (currentPhase == "Day")
            {
                return isAlive;
            }
            else if (currentPhase == "Night")
            {
                return isAlive && myRole == "인랑";
            }

            return false;
        }

        public void AddChatMessage(string sender, string message)
        {
            if (chatBox == null || chatBox.InvokeRequired)
            {
                if (chatBox != null)
                {
                    chatBox.Invoke((MethodInvoker)(() => AddChatMessage(sender, message)));
                }
                return;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");

                if (sender == "System")
                {
                    chatBox.SelectionColor = Color.Yellow;
                    chatBox.AppendText($"[{timestamp}] System: {message}\n");
                }
                else
                {
                    // 인랑 채팅 구분
                    if (currentPhase == "Night" && myRole == "인랑")
                    {
                        chatBox.SelectionColor = Color.Red;
                        chatBox.AppendText($"[{timestamp}] [인랑] ");
                    }
                    else
                    {
                        chatBox.SelectionColor = Color.Gray;
                        chatBox.AppendText($"[{timestamp}] ");
                    }

                    chatBox.SelectionColor = Color.LightBlue;
                    chatBox.AppendText($"{sender}: ");
                    chatBox.SelectionColor = Color.White;
                    chatBox.AppendText($"{message}\n");
                }

                chatBox.ScrollToCaret();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiPlayGameForm] 채팅 메시지 추가 오류: " + ex.Message);
            }
        }

        // 상태 관리
        public void ResetVoteButton()
        {
            hasVoted = false;
            if (voteButton.Visible)
            {
                voteButton.Text = "vote";
                voteButton.Enabled = true;
                voteButton.BackColor = Color.BurlyWood;
            }
        }

        public void ResetNightActions()
        {
            hasActed = false;
            if (actionButton.Visible)
            {
                actionButton.Text = GetNightActionText();
                actionButton.Enabled = true;
                actionButton.BackColor = Color.Purple;
            }
        }

        public void DisablePlayerUI()
        {
            phaseLabel.Text += " (사망)";
            phaseLabel.ForeColor = Color.Red;
            systemMsgLabel.Text = "당신은 사망했습니다. 관전 모드입니다.";
            roleInfoLabel.ForeColor = Color.Gray;

            // UI 비활성화
            voteButton.Visible = false;
            HideNightActionControls();
            messageBox.Enabled = false;
            sendButton.Enabled = false;
        }

        public void EndGame(string result)
        {
            isGameEnded = true;
            phaseTimer.Stop();
            uiUpdateTimer.Stop();

            HideNightActionControls();
            voteButton.Visible = false;
            sendButton.Enabled = false;
            messageBox.Enabled = false;

            phaseLabel.Text = "게임 종료";
            phaseLabel.ForeColor = Color.Red;
            systemMsgLabel.Text = "게임이 종료되었습니다.";

            AddChatMessage("System", "=== 게임 종료 ===");
            AddChatMessage("System", result);

            MessageBox.Show($"게임이 종료되었습니다!\n\n{result}", "게임 종료",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // 서버 통신
        public void SendMessage(string message)
        {
            try
            {
                if (writer != null && client != null && client.Connected)
                {
                    writer.WriteLine(message);
                    Console.WriteLine("[MultiPlayGameForm] 송신: " + message);
                }
                else
                {
                    Console.WriteLine("[MultiPlayGameForm] 연결이 끊어진 상태에서 송신 시도: " + message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiPlayGameForm] 송신 실패: " + ex.Message);
            }
        }

        // 리소스 정리
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            try
            {
                isGameEnded = true;

                // 타이머 정리
                phaseTimer?.Stop();
                phaseTimer?.Dispose();
                uiUpdateTimer?.Stop();
                uiUpdateTimer?.Dispose();

                // 스레드 정리
                cancellationTokenSource.Cancel();
                if (receiveThread != null && receiveThread.IsAlive)
                {
                    receiveThread.Join(1000);
                }

                // 서버에 퇴장 알림
                SendMessage("LEAVE_ROOM");

                // 이미지 리소스 정리
                playerImage?.Dispose();
                deadOverlay?.Dispose();

                // 폰트 리소스 정리
                titleFont?.Dispose();
                roleFont?.Dispose();
                timerFont?.Dispose();
                descFont?.Dispose();
                phaseFont?.Dispose();
                nameFont?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiPlayGameForm] 종료 오류: " + ex.Message);
            }
        }
    }

    // 게임 페이즈 열거형
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
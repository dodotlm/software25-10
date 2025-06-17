using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System;

namespace InRang
{
    public partial class MultiPlayGameForm : Form
    {
        private TcpClient client;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread receiveThread;
        private CancellationTokenSource cancellationTokenSource;

        // 게임 상태
        private string myRole = "";
        private string currentPhase = "Introduction";
        private List<string> playerList = new List<string>();
        private Dictionary<string, bool> playerAliveStatus = new Dictionary<string, bool>();
        private bool gameStarted = false;
        private bool isGameEnded = false;
        private int currentDay = 1;

        // 특수 상태
        private bool canSeeDeadChat = false;
        private Dictionary<string, int> playerSuspicionLevels = new Dictionary<string, int>();
        private Dictionary<string, string> lastPlayerActions = new Dictionary<string, string>();

        // 타이머
        private System.Windows.Forms.Timer phaseTimer;
        private System.Windows.Forms.Timer speechBubbleTimer;
        private System.Windows.Forms.Timer roleViewTimer; // 직업 확인 타이머 추가
        private int timeRemaining = 60;
        private int remainingTime = 8; // 직업 확인 시간

        // UI 컨트롤들 (직업 확인 화면용)
        private bool showingRole = true; // 직업 확인 화면 표시 여부

        // UI 컨트롤들 (게임 화면용)
        private Panel gamePanel;
        private Label phaseLabel;
        private Label timeLabel;
        private Label systemMsgLabel;

        // 채팅 시스템 (왼쪽)
        private Panel chatPanel;
        private RichTextBox chatBox;
        private RichTextBox deadChatBox;
        private TextBox messageBox;
        private Button sendButton;
        private Label chatLabel;

        // 투표 및 행동 시스템 (오른쪽)
        private Panel actionPanel;
        private Label actionLabel;
        private ComboBox targetSelector;
        private Button voteButton;
        private Button actionButton;
        private Label selectionLabel;

        // 플레이어 UI (가운데)
        private Dictionary<string, PictureBox> playerBoxes = new Dictionary<string, PictureBox>();
        private Dictionary<string, Label> playerNameLabels = new Dictionary<string, Label>();
        private Dictionary<string, Label> playerStatusLabels = new Dictionary<string, Label>();
        private Dictionary<string, Label> speechBubbles = new Dictionary<string, Label>();
        private Dictionary<string, Panel> suspicionIndicators = new Dictionary<string, Panel>();

        // 역할 정보 표시
        private Label roleLabel;
        private PictureBox roleImageBox;

        // 이미지 리소스
        private Font titleFont;
        private Font phaseFont;
        private Font timerFont;
        private Font descFont;
        private Font nameFont;
        private Font chatFont;
        private Font roleFont; // 직업 화면용 폰트

        private Image playerImage;
        private Image deadOverlay;
        private Image voteOverlay;
        private Image roleImage; // 직업 이미지
        private Dictionary<string, Image> roleImages = new Dictionary<string, Image>();

        public MultiPlayGameForm(TcpClient tcpClient, StreamReader streamReader, StreamWriter streamWriter)
        {
            InitializeComponent();
            client = tcpClient;
            reader = streamReader;
            writer = streamWriter;

            LoadResources();
            InitializeUI();
            InitializeTimer();

            // 직업 확인 화면부터 시작
            showingRole = true;
            gameStarted = false;

            this.Load += MultiPlayGameForm_Load;
        }

        public MultiPlayGameForm(TcpClient tcpClient, StreamReader streamReader, StreamWriter streamWriter, CancellationTokenSource cancellationTokenSource)
            : this(tcpClient, streamReader, streamWriter)
        {
            this.cancellationTokenSource = cancellationTokenSource;
        }

        private void LoadResources()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            // 글꼴 설정
            titleFont = new Font("Noto Sans KR", 32, FontStyle.Bold);
            phaseFont = new Font("Noto Sans KR", 28, FontStyle.Bold);
            timerFont = new Font("Noto Sans KR", 24, FontStyle.Bold);
            descFont = new Font("Noto Sans KR", 14, FontStyle.Regular);
            nameFont = new Font("Noto Sans KR", 10, FontStyle.Bold);
            chatFont = new Font("Noto Sans KR", 9, FontStyle.Regular);
            roleFont = new Font("Noto Sans KR", 36, FontStyle.Bold); // 직업 화면용

            // 기본 이미지 로드
            try { playerImage = Image.FromFile(Path.Combine(resourcePath, "player.jpg")); } catch { }
            try { deadOverlay = Image.FromFile(Path.Combine(resourcePath, "x.jpg")); } catch { }
            try { voteOverlay = Image.FromFile(Path.Combine(resourcePath, "vote.jpg")); } catch { }

            // 역할 이미지 로드
            string[] roles = { "시민", "인랑", "점쟁이", "영매", "사냥꾼", "네코마타", "광인", "여우", "배덕자" };
            string[] roleFiles = { "civ1.jpg", "inrang.jpg", "fortuneTeller.jpg", "medium.jpg",
                                 "hunter.jpg", "nekomata.jpg", "madman.jpg", "fox.jpg", "immoral.jpg" };

            for (int i = 0; i < roles.Length; i++)
            {
                try
                {
                    string imagePath = Path.Combine(resourcePath, roleFiles[i]);
                    if (File.Exists(imagePath))
                    {
                        roleImages[roles[i]] = Image.FromFile(imagePath);
                    }
                }
                catch { }
            }
        }

        private void InitializeUI()
        {
            // 폼 기본 설정 (싱글플레이와 동일)
            this.Text = "인랑 게임 - 멀티플레이";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;

            // 게임 패널 (처음에는 숨김)
            gamePanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Black,
                Visible = false
            };
            this.Controls.Add(gamePanel);

            InitializeGameScreen();

            // 직업 확인 화면에서 클릭 이벤트
            this.Click += (sender, e) =>
            {
                if (showingRole && roleViewTimer != null && roleViewTimer.Enabled)
                {
                    StartGame();
                }
            };
        }

        private void InitializeGameScreen()
        {
            // 상단 정보 (페이즈, 타이머)
            phaseLabel = new Label
            {
                Text = "day 1",
                Font = phaseFont,
                ForeColor = Color.BurlyWood,
                Location = new Point(150, 20),
                Size = new Size(200, 40),
                AutoSize = true
            };
            gamePanel.Controls.Add(phaseLabel);

            timeLabel = new Label
            {
                Text = "180",
                Font = timerFont,
                ForeColor = Color.White,
                Location = new Point(350, 20),
                Size = new Size(100, 40),
                AutoSize = true
            };
            gamePanel.Controls.Add(timeLabel);

            // 역할 정보 표시
            roleLabel = new Label
            {
                Text = "",
                Font = new Font("Noto Sans KR", 14, FontStyle.Bold),
                ForeColor = Color.Yellow,
                Location = new Point(500, 20),
                AutoSize = true
            };
            gamePanel.Controls.Add(roleLabel);

            roleImageBox = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(650, 15),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Visible = false
            };
            gamePanel.Controls.Add(roleImageBox);

            // 시스템 메시지 라벨
            systemMsgLabel = new Label
            {
                Text = "",
                Font = descFont,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(430, 70),
                Size = new Size(300, 70),
                BorderStyle = BorderStyle.FixedSingle
            };
            gamePanel.Controls.Add(systemMsgLabel);

            // 채팅 시스템 (왼쪽, 싱글플레이와 동일한 위치)
            InitializeChatSystem();

            // 투표/행동 시스템 (오른쪽)
            InitializeActionSystem();

            // 플레이어 상태 UI는 나중에 플레이어 목록을 받으면 생성
        }

        private void InitializeChatSystem()
        {
            // 채팅 패널
            chatPanel = new Panel
            {
                Location = new Point(30, 150),
                Size = new Size(270, 330),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(20, 20, 20)
            };
            gamePanel.Controls.Add(chatPanel);

            // 채팅 라벨
            chatLabel = new Label
            {
                Text = "전체 공개 채팅방",
                Location = new Point(30, 130),
                Size = new Size(270, 20),
                ForeColor = Color.BurlyWood,
                Font = new Font("Noto Sans KR", 10),
                TextAlign = ContentAlignment.MiddleCenter
            };
            gamePanel.Controls.Add(chatLabel);

            // 메인 채팅박스
            chatBox = new RichTextBox
            {
                Location = new Point(10, 10),
                Size = new Size(250, 250),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = chatFont
            };
            chatPanel.Controls.Add(chatBox);

            // 죽은 자들 채팅박스 (영매용, 초기에는 숨김)
            deadChatBox = new RichTextBox
            {
                Location = new Point(10, 270),
                Size = new Size(250, 50),
                BackColor = Color.FromArgb(50, 20, 20),
                ForeColor = Color.Gray,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Noto Sans KR", 8),
                Visible = false
            };
            chatPanel.Controls.Add(deadChatBox);

            // 메시지 입력란
            messageBox = new TextBox
            {
                Location = new Point(10, 290),
                Size = new Size(170, 25),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = chatFont
            };
            messageBox.KeyPress += MessageBox_KeyPress;
            chatPanel.Controls.Add(messageBox);

            // 전송 버튼
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
            sendButton.Click += SendButton_Click;
            chatPanel.Controls.Add(sendButton);
        }

        private void InitializeActionSystem()
        {
            // 투표 버튼 (싱글플레이와 동일한 위치)
            voteButton = new Button
            {
                Text = "vote",
                Location = new Point(540, 480),
                Size = new Size(70, 30),
                BackColor = Color.BurlyWood,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 11, FontStyle.Bold)
            };
            voteButton.Click += VoteButton_Click;
            gamePanel.Controls.Add(voteButton);

            // 플레이어 선택 UI (밤에만 표시)
            selectionLabel = new Label
            {
                Text = "플레이어 선택",
                Location = new Point(500, 180),
                Size = new Size(150, 25),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Noto Sans KR", 12),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            gamePanel.Controls.Add(selectionLabel);

            targetSelector = new ComboBox
            {
                Location = new Point(500, 210),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            gamePanel.Controls.Add(targetSelector);

            // 행동 버튼 (능력 사용)
            actionButton = new Button
            {
                Text = "action",
                Location = new Point(540, 240),
                Size = new Size(70, 30),
                BackColor = Color.BurlyWood,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 11, FontStyle.Bold),
                Visible = false
            };
            actionButton.Click += ActionButton_Click;
            gamePanel.Controls.Add(actionButton);
        }

        private void CreatePlayerStatusUI()
        {
            // 기존 플레이어 UI 정리
            ClearPlayerUI();

            Console.WriteLine($"[MultiPlayGameForm] 플레이어 UI 생성 시작: {playerList.Count}명");

            // 플레이어 상태 UI 생성 (싱글플레이와 동일한 레이아웃)
            int playerImageSize = 70;
            int spacing = 30;
            int startX = 350;
            int startY = 180;
            int playersPerRow = 4;

            for (int i = 0; i < playerList.Count; i++)
            {
                string playerName = playerList[i];
                int row = i / playersPerRow;
                int col = i % playersPerRow;
                int x = startX + col * (playerImageSize + spacing);
                int y = startY + row * (playerImageSize + spacing + 20);

                Console.WriteLine($"[MultiPlayGameForm] 플레이어 {playerName} UI 생성 위치: ({x}, {y})");

                // 플레이어 이미지
                PictureBox playerBox = new PictureBox
                {
                    Location = new Point(x, y),
                    Size = new Size(playerImageSize, playerImageSize),
                    Image = playerImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Tag = playerName,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.FromArgb(60, 60, 60)
                };

                // 플레이어 이름 레이블
                Label playerName_label = new Label
                {
                    Text = playerName,
                    Location = new Point(x, y + playerImageSize + 5),
                    Size = new Size(playerImageSize, 15),
                    ForeColor = Color.BurlyWood,
                    Font = nameFont,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };

                // 플레이어 상태 레이블
                Label statusLabel = new Label
                {
                    Text = "생존",
                    Location = new Point(x, y + playerImageSize + 20),
                    Size = new Size(playerImageSize, 15),
                    ForeColor = Color.Green,
                    Font = new Font("Noto Sans KR", 8),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };

                // 말풍선 (채팅 표시용)
                Label speechBubble = new Label
                {
                    Text = "",
                    Location = new Point(x - 20, y - 30),
                    Size = new Size(playerImageSize + 40, 25),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(150, 0, 0, 0),
                    Font = new Font("Noto Sans KR", 8),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Visible = false,
                    BorderStyle = BorderStyle.FixedSingle
                };

                // 의심도 표시기
                Panel suspicionIndicator = new Panel
                {
                    Location = new Point(x + playerImageSize - 10, y),
                    Size = new Size(10, 10),
                    BackColor = Color.Green,
                    Visible = false
                };

                playerBoxes[playerName] = playerBox;
                playerNameLabels[playerName] = playerName_label;
                playerStatusLabels[playerName] = statusLabel;
                speechBubbles[playerName] = speechBubble;
                suspicionIndicators[playerName] = suspicionIndicator;
                playerSuspicionLevels[playerName] = 0;

                gamePanel.Controls.Add(playerBox);
                gamePanel.Controls.Add(playerName_label);
                gamePanel.Controls.Add(statusLabel);
                gamePanel.Controls.Add(speechBubble);
                gamePanel.Controls.Add(suspicionIndicator);

                // 플레이어 클릭 이벤트 (빠른 타겟 선택용)
                playerBox.Click += (sender, e) =>
                {
                    if (targetSelector.Visible)
                    {
                        string clickedPlayer = (string)((PictureBox)sender).Tag;
                        for (int idx = 0; idx < targetSelector.Items.Count; idx++)
                        {
                            if (targetSelector.Items[idx].ToString() == clickedPlayer)
                            {
                                targetSelector.SelectedIndex = idx;
                                break;
                            }
                        }
                    }
                };

                Console.WriteLine($"[MultiPlayGameForm] 플레이어 {playerName} UI 생성 완료");
            }

            Console.WriteLine($"[MultiPlayGameForm] 모든 플레이어 UI 생성 완료: {playerBoxes.Count}개");

            // UI 갱신 강제 실행
            gamePanel.Invalidate();
            this.Invalidate();
        }

        private void ClearPlayerUI()
        {
            foreach (var kvp in playerBoxes)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }
            foreach (var kvp in playerNameLabels)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }
            foreach (var kvp in playerStatusLabels)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }
            foreach (var kvp in speechBubbles)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }
            foreach (var kvp in suspicionIndicators)
            {
                gamePanel.Controls.Remove(kvp.Value);
                kvp.Value.Dispose();
            }

            playerBoxes.Clear();
            playerNameLabels.Clear();
            playerStatusLabels.Clear();
            speechBubbles.Clear();
            suspicionIndicators.Clear();
        }

        private void InitializeTimer()
        {
            phaseTimer = new System.Windows.Forms.Timer();
            phaseTimer.Interval = 1000;
            phaseTimer.Tick += PhaseTimer_Tick;

            speechBubbleTimer = new System.Windows.Forms.Timer();
            speechBubbleTimer.Interval = 3000; // 3초 후 말풍선 숨김
            speechBubbleTimer.Tick += SpeechBubbleTimer_Tick;

            // 직업 확인 타이머 (싱글플레이와 동일)
            roleViewTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000 // 1초
            };
            roleViewTimer.Tick += RoleViewTimer_Tick;
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
            if (cancellationTokenSource == null)
                cancellationTokenSource = new CancellationTokenSource();

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

        private void RoleViewTimer_Tick(object sender, EventArgs e)
        {
            remainingTime--;
            this.Invalidate(); // 화면 갱신

            // 타이머 종료 시 게임 화면으로 전환
            if (remainingTime <= 0)
            {
                StartGame();
            }
        }

        private void StartGame()
        {
            // 직업 확인 타이머 중지
            if (roleViewTimer != null)
            {
                roleViewTimer.Stop();
                roleViewTimer.Dispose();
                roleViewTimer = null;
            }

            // 게임 시작 상태로 변경
            showingRole = false;
            gameStarted = true;

            Console.WriteLine("[MultiPlayGameForm] 게임 화면으로 전환");

            // 게임 패널 표시
            gamePanel.Visible = true;

            // 플레이어 목록이 있다면 UI 생성
            if (playerList.Count > 0)
            {
                Console.WriteLine($"[MultiPlayGameForm] 플레이어 UI 생성: {playerList.Count}명");
                CreatePlayerStatusUI();
                UpdateAllPlayerStatus();
            }
            else
            {
                // 플레이어 목록이 아직 없다면 요청
                SendMessage("REQUEST_PLAYER_LIST");
            }

            // 시작 메시지 추가 (채팅박스가 보이는 상태에서)
            AddChatMessage("System", "게임이 시작되었습니다.");
            AddChatMessage("System", $"당신의 역할은 {myRole}입니다.");

            // 인랑 팀 정보 제공 (인랑이나 광인인 경우)
            if (myRole == "인랑" || myRole == "광인")
            {
                AddChatMessage("System", "인랑 팀으로 참여합니다. 밤에는 인랑끼리 채팅이 가능합니다.");
            }

            // 배덕자에게 여우 정보 제공
            if (myRole == "배덕자")
            {
                AddChatMessage("System", "배덕자로 참여합니다. 여우를 찾아 도우세요.");
            }

            // 서버에 게임 준비 완료 신호 전송
            SendMessage("GAME_START_READY");

            // 화면 갱신
            this.Invalidate();
        }

        private void HandleServerMessage(string msg)
        {
            Console.WriteLine("[MultiPlayGameForm] 수신: " + msg);

            if (msg.StartsWith("ROLE:"))
            {
                myRole = msg.Substring("ROLE:".Length);
                roleLabel.Text = $"내 역할: {myRole}";

                // 역할 이미지 표시
                if (roleImages.ContainsKey(myRole))
                {
                    roleImage = roleImages[myRole];
                    roleImageBox.Image = roleImages[myRole];
                    roleImageBox.Visible = true;
                }

                Console.WriteLine($"[MultiPlayGameForm] 역할 배정됨: {myRole}");
                UpdateUIForRole();

                // 직업 확인 화면 표시 및 타이머 시작
                if (showingRole)
                {
                    remainingTime = 8; // 8초로 초기화
                    if (roleViewTimer != null)
                    {
                        roleViewTimer.Start();
                        Console.WriteLine("[MultiPlayGameForm] 직업 확인 타이머 시작");
                    }
                    this.Invalidate(); // 화면 다시 그리기
                }
            }
            else if (msg.StartsWith("PLAYER_LIST:"))
            {
                string playerData = msg.Substring("PLAYER_LIST:".Length);
                UpdatePlayerList(playerData);
            }
            else if (msg.StartsWith("GAME_PHASE_START:"))
            {
                string phase = msg.Substring("GAME_PHASE_START:".Length);

                // 직업 확인 화면이 아직 표시 중이면 즉시 게임 시작
                if (showingRole)
                {
                    Console.WriteLine("[MultiPlayGameForm] 게임 페이즈 시작 - 직업 확인 화면 건너뛰기");
                    StartGame();
                }

                StartPhase(phase);
            }
            else if (msg.StartsWith("PHASE_END:"))
            {
                string endedPhase = msg.Substring("PHASE_END:".Length);
                Console.WriteLine($"[MultiPlayGameForm] 페이즈 종료 신호: {endedPhase}");

                // 타이머 강제 정지
                if (phaseTimer.Enabled)
                {
                    phaseTimer.Stop();
                    timeLabel.Text = "전환중";
                    timeLabel.ForeColor = Color.Yellow;
                }

                // UI 비활성화
                if (endedPhase == "Day")
                {
                    voteButton.Enabled = false;
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;
                    AddSystemMessage("낮 페이즈가 종료되었습니다.");
                }
                else if (endedPhase == "Night")
                {
                    HideActionControls();
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;
                    AddSystemMessage("밤 페이즈가 종료되었습니다.");
                }
            }
            else if (msg.StartsWith("CHAT:"))
            {
                string chatMsg = msg.Substring("CHAT:".Length);
                HandleChatMessage(chatMsg);
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
            else if (msg.StartsWith("MEDIUM_RESULT:"))
            {
                string result = msg.Substring("MEDIUM_RESULT:".Length);
                HandleMediumResult(result);
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
                AddSystemMessage("당신은 오늘 밤 보호받았습니다!");
            }
            else if (msg.StartsWith("GAME_ROLES:"))
            {
                string rolesText = msg.Substring("GAME_ROLES:".Length);
                AddChatMessage("=== 최종 역할 공개 ===");
                AddChatMessage(rolesText);
            }
            else if (msg.StartsWith("ACTION_CONFIRMED:"))
            {
                string confirmMsg = msg.Substring("ACTION_CONFIRMED:".Length);
                AddSystemMessage(confirmMsg);
                HideActionControls();
            }
            else if (msg == "RETURN_TO_LOBBY")
            {
                MessageBox.Show("게임이 종료되어 로비로 돌아갑니다.", "게임 종료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
        }

        // 나머지 메서드들은 기존 코드와 동일하므로 생략...
        // (HandleChatMessage, ShowSpeechBubble, UpdateSuspicionLevels 등)

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!showingRole || gameStarted)
            {
                // 게임 시작 후에는 그리기 처리 안함 (게임 패널이 표시됨)
                return;
            }

            // 직업 확인 화면 그리기 (싱글플레이와 동일)
            Graphics g = e.Graphics;
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            // 직업 화면 표시
            g.DrawString("당신의 역할", titleFont, Brushes.BurlyWood, new RectangleF(0, 30, this.ClientSize.Width, 60), centerFormat);
            g.DrawString("다른 플레이어에게 보여주지 마세요!", descFont, Brushes.Red, new RectangleF(0, 90, this.ClientSize.Width, 30), centerFormat);

            // 직업 이미지
            if (roleImage != null)
            {
                int imgSize = 200;
                int imgX = (this.ClientSize.Width - imgSize) / 2;
                int imgY = 130;
                Rectangle imgRect = new Rectangle(imgX, imgY, imgSize, imgSize);
                g.DrawImage(roleImage, imgRect);
            }

            // 직업명
            if (!string.IsNullOrEmpty(myRole))
            {
                g.DrawString(myRole, roleFont, Brushes.White, new RectangleF(0, 350, this.ClientSize.Width, 60), centerFormat);

                // 직업 설명
                string roleDescription = GetRoleDescription(myRole);
                g.DrawString(roleDescription, descFont, Brushes.BurlyWood,
                    new RectangleF(100, 410, this.ClientSize.Width - 200, 100), centerFormat);
            }

            // 타이머
            g.DrawString($"{remainingTime}초", timerFont, Brushes.White, new RectangleF(0, 500, this.ClientSize.Width, 40), centerFormat);

            // 클릭 안내
            g.DrawString("화면을 클릭하여 확인 완료", descFont, Brushes.Gray,
                new RectangleF(0, 540, this.ClientSize.Width, 30), centerFormat);
        }

        private string GetRoleDescription(string roleName)
        {
            switch (roleName)
            {
                case "시민":
                    return "특수 능력은 없지만, 토론과 투표로 인랑을 찾아내세요.";
                case "인랑":
                    return "매일 밤 한 명을 습격하여 제거할 수 있습니다.\n다른 인랑을 알아볼 수 있습니다.";
                case "점쟁이":
                    return "매일 밤 한 명을 지목하여 그 사람이 인랑인지 아닌지 확인할 수 있습니다.";
                case "영매":
                    return "처형된 사람의 정체를 확인할 수 있습니다.";
                case "사냥꾼":
                    return "매일 밤 한 명을 선택하여 인랑의 습격으로부터 보호할 수 있습니다.";
                case "네코마타":
                    return "인랑에게 습격당하면, 습격한 인랑도 같이 죽습니다.\n처형당하면 랜덤으로 다른 누군가와 같이 죽습니다.";
                case "광인":
                    return "특수 능력은 없지만, 점쟁이에게는 시민으로 보입니다.\n인랑을 돕는 것이 목표입니다.";
                case "여우":
                    return "게임 종료 시까지 생존하면 단독 승리합니다.\n점쟁이에게 점 대상이 되면 사망합니다.";
                case "배덕자":
                    return "게임 시작 시 여우가 누구인지 알 수 있습니다.\n여우가 승리하면 함께 승리합니다.";
                default:
                    return "";
            }
        }

        private void HandleChatMessage(string chatMsg)
        {
            // 채팅 메시지 표시
            if (chatMsg.StartsWith("[인랑 채팅]"))
            {
                // 인랑 채팅 특별 처리
                AddChatMessage(chatMsg);
            }
            else
            {
                AddChatMessage(chatMsg);
            }

            // 말풍선 표시 로직
            if (chatMsg.Contains(":"))
            {
                string[] parts = chatMsg.Split(new char[] { ':' }, 2);
                if (parts.Length == 2)
                {
                    string speakerName = parts[0].Trim();
                    string message = parts[1].Trim();

                    // 시스템 메시지나 특수 메시지는 말풍선 표시 안함
                    if (!speakerName.StartsWith("[") && !speakerName.Contains("시스템"))
                    {
                        ShowSpeechBubble(speakerName, message);
                        UpdateSuspicionLevels(speakerName, message);
                    }
                }
            }

            Console.WriteLine($"[MultiPlayGameForm] 채팅 메시지 처리: {chatMsg}");
        }

        private void ShowSpeechBubble(string playerName, string message)
        {
            if (speechBubbles.ContainsKey(playerName))
            {
                Label bubble = speechBubbles[playerName];
                bubble.Text = message.Length > 15 ? message.Substring(0, 15) + "..." : message;
                bubble.Visible = true;

                // 이전 타이머 정지하고 새로 시작
                speechBubbleTimer.Stop();
                speechBubbleTimer.Start();
            }
        }

        private void UpdateSuspicionLevels(string speakerName, string message)
        {
            // 의심스러운 키워드에 따라 의심도 조정
            string[] suspiciousWords = { "인랑", "의심", "수상", "이상", "거짓말" };
            string[] trustWords = { "믿어", "신뢰", "확신", "시민" };

            foreach (string player in playerList)
            {
                if (player == speakerName) continue;

                int currentSuspicion = playerSuspicionLevels[player];

                // 메시지에 플레이어 이름이 언급되면서 의심스러운 단어가 있으면 의심도 증가
                if (message.Contains(player))
                {
                    foreach (string word in suspiciousWords)
                    {
                        if (message.Contains(word))
                        {
                            currentSuspicion = Math.Min(100, currentSuspicion + 20);
                            break;
                        }
                    }

                    foreach (string word in trustWords)
                    {
                        if (message.Contains(word))
                        {
                            currentSuspicion = Math.Max(0, currentSuspicion - 15);
                            break;
                        }
                    }
                }

                playerSuspicionLevels[player] = currentSuspicion;
                UpdateSuspicionIndicator(player, currentSuspicion);
            }
        }

        private void UpdateSuspicionIndicator(string playerName, int suspicionLevel)
        {
            if (suspicionIndicators.ContainsKey(playerName))
            {
                Panel indicator = suspicionIndicators[playerName];

                if (suspicionLevel < 25)
                {
                    indicator.BackColor = Color.Green;
                    indicator.Visible = false;
                }
                else if (suspicionLevel < 50)
                {
                    indicator.BackColor = Color.Yellow;
                    indicator.Visible = true;
                }
                else if (suspicionLevel < 75)
                {
                    indicator.BackColor = Color.Orange;
                    indicator.Visible = true;
                }
                else
                {
                    indicator.BackColor = Color.Red;
                    indicator.Visible = true;
                }
            }
        }

        private void SpeechBubbleTimer_Tick(object sender, EventArgs e)
        {
            // 모든 말풍선 숨기기
            foreach (var bubble in speechBubbles.Values)
            {
                bubble.Visible = false;
            }
            speechBubbleTimer.Stop();
        }

        private void UpdateUIForRole()
        {
            // 영매는 죽은 자들의 채팅을 볼 수 있음
            if (myRole == "영매")
            {
                deadChatBox.Visible = true;
                canSeeDeadChat = true;
                AddDeadChatMessage("[시스템] 영매 능력으로 죽은 자들의 대화를 들을 수 있습니다.");

                // 채팅박스 크기 조정
                chatBox.Size = new Size(250, 200);
                deadChatBox.Location = new Point(10, 220);
                messageBox.Location = new Point(10, 290);
                sendButton.Location = new Point(190, 290);
            }
        }

        private void UpdatePlayerList(string playerData)
        {
            if (string.IsNullOrEmpty(playerData)) return;

            string[] players = playerData.Split(',');
            playerList.Clear();
            playerAliveStatus.Clear();
            targetSelector.Items.Clear();

            foreach (string player in players)
            {
                if (string.IsNullOrWhiteSpace(player)) continue;

                string cleanName = player.Trim()
                    .Replace(" [준비]", "")
                    .Replace(" [죽음]", "")
                    .Replace(" [보호됨]", "")
                    .Replace(" [AI]", ""); // AI 태그 제거하되 목록에는 포함

                bool isDead = player.Contains("[죽음]");

                // AI 플레이어는 제외하고 실제 플레이어만 목록에 추가
                if (!player.Contains("[AI]") && !cleanName.StartsWith("AI_"))
                {
                    playerList.Add(cleanName);
                    playerAliveStatus[cleanName] = !isDead;

                    if (!isDead && !cleanName.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSelector.Items.Add(cleanName);
                    }
                }
            }

            Console.WriteLine($"[MultiPlayGameForm] 플레이어 목록 업데이트: {string.Join(", ", playerList)}");

            // 플레이어 UI 재생성 (게임이 시작되었다면)
            if (gameStarted)
            {
                CreatePlayerStatusUI();
                UpdateAllPlayerStatus();
            }
        }

        private void UpdateAllPlayerStatus()
        {
            foreach (string playerName in playerList)
            {
                bool isAlive = playerAliveStatus[playerName];

                if (playerBoxes.ContainsKey(playerName))
                {
                    // 이미지 업데이트
                    playerBoxes[playerName].Image = isAlive ? playerImage : deadOverlay;

                    // 이름 색상 업데이트
                    playerNameLabels[playerName].ForeColor = isAlive ? Color.BurlyWood : Color.Gray;

                    // 상태 레이블 업데이트
                    playerStatusLabels[playerName].Text = isAlive ? "생존" : "사망";
                    playerStatusLabels[playerName].ForeColor = isAlive ? Color.Green : Color.Red;
                }
            }
        }

        private void StartPhase(string phase)
        {
            currentPhase = phase;
            gameStarted = true;

            if (phase == "Day")
            {
                phaseLabel.Text = $"day {currentDay}";
                phaseLabel.ForeColor = Color.Gold;
                timeRemaining = 50; // 50초
                AddSystemMessage("낮이 되었습니다. 토론을 시작하세요.");

                // 투표 버튼 활성화
                voteButton.Visible = true;

                // 밤 전용 UI 숨기기
                HideActionControls();

                // 메시지 입력 활성화 (살아있는 플레이어만)
                bool isAlive = playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                              playerAliveStatus[GameSettings.UserName];
                messageBox.Enabled = isAlive;
                sendButton.Enabled = isAlive;
            }
            else if (phase == "Night")
            {
                phaseLabel.Text = "night";
                phaseLabel.ForeColor = Color.DarkBlue;
                timeRemaining = 40; // 40초
                AddSystemMessage("밤이 되었습니다. 능력을 사용하세요.");

                // 투표 버튼 비활성화
                voteButton.Visible = false;

                // 밤 행동 UI 설정
                SetupNightUI();

                // 메시지 입력 - 인랑만 채팅 가능 (인랑 채팅)
                bool canChat = IsWerewolf() &&
                              playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                              playerAliveStatus[GameSettings.UserName];
                messageBox.Enabled = canChat;
                sendButton.Enabled = canChat;

                if (canChat)
                {
                    chatLabel.Text = "인랑 전용 채팅";
                    chatLabel.ForeColor = Color.Red;
                }
                else
                {
                    chatLabel.Text = "밤 - 채팅 불가";
                    chatLabel.ForeColor = Color.Gray;
                }
            }

            timeLabel.Text = $"{timeRemaining}초";
            timeLabel.ForeColor = Color.White;
            phaseTimer.Start();
        }

        private void SetupNightUI()
        {
            bool isAlive = playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                          playerAliveStatus[GameSettings.UserName];

            if (!isAlive)
            {
                Console.WriteLine("[MultiPlayGameForm] 사망한 플레이어는 밤 행동 불가");
                return;
            }

            Console.WriteLine($"[MultiPlayGameForm] 밤 UI 설정: {myRole}");

            // 자신의 직업에 따른 UI 설정
            switch (myRole)
            {
                case "인랑":
                    ShowActionControls("습격할 대상 선택", "습격하기");
                    UpdateTargetSelectorForRole("인랑");
                    Console.WriteLine("[MultiPlayGameForm] 인랑 UI 설정 완료");
                    break;
                case "점쟁이":
                    ShowActionControls("점을 볼 대상 선택", "점 보기");
                    UpdateTargetSelectorForRole("점쟁이");
                    Console.WriteLine("[MultiPlayGameForm] 점쟁이 UI 설정 완료");
                    break;
                case "사냥꾼":
                    ShowActionControls("보호할 대상 선택", "보호하기");
                    UpdateTargetSelectorForRole("사냥꾼");
                    Console.WriteLine("[MultiPlayGameForm] 사냥꾼 UI 설정 완료");
                    break;
                case "영매":
                    ShowActionControls("영혼과 대화할 대상 선택", "영혼 대화");
                    UpdateTargetSelectorForMedium();
                    Console.WriteLine("[MultiPlayGameForm] 영매 UI 설정 완료");
                    break;
                case "네코마타":
                    ShowActionControls("능력을 사용할 대상 선택", "능력 사용");
                    UpdateTargetSelectorForRole("네코마타");
                    Console.WriteLine("[MultiPlayGameForm] 네코마타 UI 설정 완료");
                    break;
                case "시민":
                case "광인":
                case "여우":
                case "배덕자":
                default:
                    HideActionControls();
                    Console.WriteLine($"[MultiPlayGameForm] {myRole}는 밤 행동 없음");
                    break;
            }
        }

        private void UpdateTargetSelectorForRole(string role)
        {
            targetSelector.Items.Clear();

            switch (role)
            {
                case "인랑":
                    // 인랑이 아닌 살아있는 플레이어들
                    foreach (string playerName in playerList)
                    {
                        if (playerAliveStatus[playerName] && playerName != GameSettings.UserName)
                        {
                            targetSelector.Items.Add(playerName);
                        }
                    }
                    break;
                case "점쟁이":
                case "사냥꾼":
                case "네코마타":
                    // 자신을 제외한 살아있는 플레이어들
                    foreach (string playerName in playerList)
                    {
                        if (playerAliveStatus[playerName] && playerName != GameSettings.UserName)
                        {
                            targetSelector.Items.Add(playerName);
                        }
                    }
                    break;
            }

            if (targetSelector.Items.Count > 0)
            {
                targetSelector.SelectedIndex = 0;
            }

            Console.WriteLine($"[MultiPlayGameForm] {role} 대상 목록 업데이트: {targetSelector.Items.Count}명");
        }

        private void UpdateTargetSelectorForMedium()
        {
            if (myRole != "영매") return;

            targetSelector.Items.Clear();
            foreach (string playerName in playerList)
            {
                if (!playerAliveStatus[playerName] && playerName != GameSettings.UserName)
                {
                    targetSelector.Items.Add(playerName);
                }
            }
        }

        private void ShowActionControls(string labelText, string buttonText)
        {
            selectionLabel.Text = labelText;
            selectionLabel.Visible = true;
            targetSelector.Visible = true;
            actionButton.Text = buttonText;
            actionButton.Visible = true;
        }

        private void HideActionControls()
        {
            selectionLabel.Visible = false;
            targetSelector.Visible = false;
            actionButton.Visible = false;
        }

        private bool IsWerewolf()
        {
            return myRole == "인랑";
        }

        private void PhaseTimer_Tick(object sender, EventArgs e)
        {
            timeRemaining--;
            timeLabel.Text = $"{timeRemaining}초";

            if (timeRemaining <= 0)
            {
                phaseTimer.Stop();

                // 타이머 종료 시 페이즈별 처리
                if (currentPhase == "Day")
                {
                    AddSystemMessage("낮 시간이 종료되었습니다. 투표 결과를 집계 중...");
                    timeLabel.Text = "집계중";
                    timeLabel.ForeColor = Color.Yellow;

                    // 투표 버튼 비활성화
                    voteButton.Enabled = false;
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;
                }
                else if (currentPhase == "Night")
                {
                    AddSystemMessage("밤 시간이 종료되었습니다. 결과를 처리 중...");
                    timeLabel.Text = "처리중";
                    timeLabel.ForeColor = Color.Yellow;

                    // 밤 행동 UI 비활성화
                    HideActionControls();
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;
                }

                SendMessage("TIME_UP");
                Console.WriteLine($"[MultiPlayGameForm] {currentPhase} 페이즈 시간 종료");
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

        private void VoteButton_Click(object sender, EventArgs e)
        {
            if (currentPhase != "Day")
            {
                MessageBox.Show("투표는 낮에만 가능합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowVoteDialog();
        }

        private void ShowVoteDialog()
        {
            // 투표 가능한 플레이어가 있는지 확인
            List<string> voteTargets = new List<string>();
            foreach (string playerName in playerList)
            {
                if (playerAliveStatus[playerName] && playerName != GameSettings.UserName)
                {
                    voteTargets.Add(playerName);
                }
            }

            if (voteTargets.Count == 0)
            {
                MessageBox.Show("투표할 수 있는 플레이어가 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 투표 대상 선택 폼 생성 (싱글플레이와 동일)
            Form voteForm = new Form
            {
                Text = "투표",
                Size = new Size(300, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(40, 40, 40)
            };

            Label label = new Label
            {
                Text = "처형할 플레이어를 선택하세요:",
                Location = new Point(20, 20),
                Size = new Size(260, 20),
                ForeColor = Color.White,
                Font = descFont
            };

            ComboBox comboBox = new ComboBox
            {
                Location = new Point(20, 50),
                Size = new Size(250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            Button voteBtn = new Button
            {
                Text = "투표",
                Location = new Point(100, 100),
                Size = new Size(80, 30),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat
            };

            Button cancelBtn = new Button
            {
                Text = "취소",
                Location = new Point(190, 100),
                Size = new Size(80, 30),
                BackColor = Color.Gray,
                FlatStyle = FlatStyle.Flat
            };

            // 투표 가능한 플레이어 목록 설정
            foreach (string playerName in voteTargets)
            {
                comboBox.Items.Add(playerName);
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
                    AddChatMessage($"[내 투표] {selectedName}에게 투표했습니다.");
                    AddSystemMessage("투표가 완료되었습니다.");
                    Console.WriteLine($"[MultiPlayGameForm] 투표 실행: {selectedName}");

                    // 투표 버튼 비활성화
                    voteButton.Enabled = false;
                    voteButton.Text = "투표 완료";

                    voteForm.Close();
                }
            };

            // 취소 이벤트
            cancelBtn.Click += (s, e) =>
            {
                voteForm.Close();
            };

            voteForm.Controls.Add(label);
            voteForm.Controls.Add(comboBox);
            voteForm.Controls.Add(voteBtn);
            voteForm.Controls.Add(cancelBtn);

            voteForm.ShowDialog();
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

            // 서버에 행동 전송
            SendMessage($"ACTION:NIGHT_ACTION:{action}:{target}");

            string actionText = GetActionDescription(action);
            AddChatMessage($"[내 행동] {target}에게 {actionText}를 사용했습니다.");
            AddSystemMessage($"{actionText} 사용 완료!");

            Console.WriteLine($"[MultiPlayGameForm] 밤 행동 실행: {action} -> {target}");

            // 능력 사용 후 UI 숨김
            HideActionControls();
        }

        private string GetActionType()
        {
            switch (myRole)
            {
                case "인랑": return "ATTACK";
                case "점쟁이": return "FORTUNE";
                case "영매": return "MEDIUM";
                case "네코마타": return "NEKOMATA";
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
            AddSystemMessage("투표 결과가 발표되었습니다.");
        }

        private void HandleNightResult(string result)
        {
            AddChatMessage($"[밤 결과] {result}");
            AddSystemMessage("밤 사이 일어난 일을 발표합니다.");
        }

        private void HandleFortuneResult(string result)
        {
            if (myRole == "점쟁이")
            {
                AddChatMessage($"[점술 결과] {result}");
                AddSystemMessage("점술 결과를 확인했습니다.");

                // 점술 결과에 따라 의심도 업데이트
                string[] parts = result.Split(':');
                if (parts.Length == 2)
                {
                    string targetName = parts[0];
                    string resultType = parts[1];

                    if (resultType == "인랑")
                    {
                        playerSuspicionLevels[targetName] = 100;
                        UpdateSuspicionIndicator(targetName, 100);
                    }
                    else
                    {
                        playerSuspicionLevels[targetName] = 0;
                        UpdateSuspicionIndicator(targetName, 0);
                    }
                }
            }
        }

        private void HandleMediumResult(string result)
        {
            if (myRole == "영매")
            {
                AddChatMessage($"[영매 결과] {result}");
                AddSystemMessage("영혼과의 대화 결과입니다.");
            }
        }

        private void HandlePlayerDeath(string deadPlayer)
        {
            AddChatMessage($"[사망] {deadPlayer}님이 사망했습니다.");
            AddSystemMessage($"{deadPlayer}님이 사망했습니다.");

            // 플레이어 상태 업데이트
            if (playerAliveStatus.ContainsKey(deadPlayer))
            {
                playerAliveStatus[deadPlayer] = false;
            }

            // 자신이 죽었다면
            if (deadPlayer.Equals(GameSettings.UserName, StringComparison.OrdinalIgnoreCase))
            {
                phaseLabel.Text += " (사망)";
                phaseLabel.ForeColor = Color.Red;
                HideActionControls();
                voteButton.Visible = false;
                AddSystemMessage("당신이 사망했습니다.");

                // 광인이나 인랑이 죽으면 죽은 자들과 채팅 가능
                if (myRole == "광인" || myRole == "인랑")
                {
                    deadChatBox.Visible = true;
                    chatBox.Size = new Size(250, 200);
                    deadChatBox.Location = new Point(10, 220);
                    messageBox.Location = new Point(10, 290);
                    sendButton.Location = new Point(190, 290);

                    AddDeadChatMessage("[시스템] 사망하여 죽은 자들과 대화할 수 있습니다.");
                    messageBox.Enabled = true;
                    sendButton.Enabled = true;
                    chatLabel.Text = "죽은 자들의 채팅";
                    chatLabel.ForeColor = Color.Gray;
                }
                else
                {
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;
                    chatLabel.Text = "사망 - 채팅 불가";
                    chatLabel.ForeColor = Color.Red;
                }
            }

            UpdateAllPlayerStatus();
            SendMessage("REQUEST_PLAYER_LIST");
        }

        private void HandleGameEnd(string result)
        {
            isGameEnded = true;
            phaseTimer.Stop();
            HideActionControls();
            voteButton.Visible = false;
            sendButton.Enabled = false;
            messageBox.Enabled = false;

            phaseLabel.Text = "게임 종료";
            phaseLabel.ForeColor = Color.Red;
            AddSystemMessage("게임이 종료되었습니다.");

            AddChatMessage("=== 게임 종료 ===");
            AddChatMessage(result);

            // 모든 말풍선과 의심도 표시기 숨기기
            foreach (var bubble in speechBubbles.Values)
            {
                bubble.Visible = false;
            }
            foreach (var indicator in suspicionIndicators.Values)
            {
                indicator.Visible = false;
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
                    // 죽은 플레이어 채팅
                    SendMessage("DEAD_CHAT:" + message);
                    AddDeadChatMessage($"{GameSettings.UserName}: {message}");
                }
                else if (currentPhase == "Night" && IsWerewolf())
                {
                    // 밤에 인랑은 인랑 전용 채팅
                    SendMessage("CHAT:" + message);
                    // 인랑 채팅은 서버에서 다시 받아서 처리하므로 여기서는 추가하지 않음
                }
                else if (currentPhase == "Day")
                {
                    // 낮에는 일반 채팅
                    SendMessage("CHAT:" + message);
                    // 일반 채팅도 서버에서 다시 받아서 처리
                }
                else
                {
                    // 채팅 불가능한 상황
                    AddSystemMessage("현재 채팅을 할 수 없습니다.");
                    messageBox.Clear();
                    return;
                }

                messageBox.Clear();
                Console.WriteLine($"[MultiPlayGameForm] 채팅 전송: {message}");
            }
        }

        private void AddChatMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            chatBox.SelectionColor = message.StartsWith("[인랑 채팅]") ? Color.Red : Color.White;
            chatBox.AppendText($"[{timestamp}] {message}\n");
            chatBox.ScrollToCaret();
        }

        private void AddChatMessage(string sender, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (sender == "System")
            {
                chatBox.SelectionColor = Color.Yellow;
                chatBox.AppendText($"[{timestamp}] [시스템] {message}\n");
            }
            else
            {
                chatBox.SelectionColor = Color.White;
                chatBox.AppendText($"[{timestamp}] {sender}: {message}\n");
            }
            chatBox.ScrollToCaret();
        }

        private void AddDeadChatMessage(string message)
        {
            if (canSeeDeadChat ||
                (playerAliveStatus.ContainsKey(GameSettings.UserName) &&
                 !playerAliveStatus[GameSettings.UserName]))
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                deadChatBox.SelectionColor = Color.Gray;
                deadChatBox.AppendText($"[{timestamp}] {message}\n");
                deadChatBox.ScrollToCaret();
            }
        }

        private void AddSystemMessage(string message)
        {
            systemMsgLabel.Text = message;
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
                speechBubbleTimer?.Stop();
                speechBubbleTimer?.Dispose();
                roleViewTimer?.Stop();
                roleViewTimer?.Dispose();

                cancellationTokenSource?.Cancel();
                receiveThread?.Join(1000);

                SendMessage("LEAVE_ROOM");

                // 이미지 리소스 정리
                playerImage?.Dispose();
                deadOverlay?.Dispose();
                voteOverlay?.Dispose();
                roleImage?.Dispose();
                foreach (var roleImg in roleImages.Values)
                {
                    roleImg?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MultiPlayGameForm] 종료 오류: " + ex.Message);
            }
        }
    }

    // GamePhase enum
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
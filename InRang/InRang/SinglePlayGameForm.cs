using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace InRang
{
    /// <summary>
    /// 게임 진행 단계
    /// </summary>
    //public enum GamePhase
    //{
    //    Introduction,    // 직업 소개
    //    Day,            // 낮 - 토론 및 투표
    //    DayResult,      // 낮 결과 - 투표 결과 발표
    //    Night,          // 밤 - 능력 사용
    //    NightResult,    // 밤 결과 - 사망자 발표
    //    GameEnd         // 게임 종료
    //}
    // 멀티플레이 부분과 충돌이 나서 일단 주석처리함. 나중에 쓸 일이 있다면 풀고 다시 충돌 해결해 보자.

    /// <summary>
    /// 싱글 플레이 게임 폼 - 직업 확인 후 게임 진행
    /// </summary>
    public partial class SinglePlayGameForm : Form
    {
        #region 필드

        // 직업 이미지 관련
        private System.Drawing.Image roleImage;
        private Dictionary<string, System.Drawing.Image> roleImages = new Dictionary<string, System.Drawing.Image>();
        private System.Drawing.Image playerImage;     // 기본 플레이어 이미지
        private System.Drawing.Image playerDeadOverlay;  // 사망 오버레이 이미지
        private System.Drawing.Image playerVoteOverlay;  // 투표 오버레이 이미지
        private System.Drawing.Image wolfImage;       // 인랑 이미지 (밤 화면용)

        // 플레이어 정보
        private int playerCount;
        private int aiCount;
        private bool yaminabeMode;
        private bool quantumMode;

        // 직업 정보
        private List<string> assignedRoles = new List<string>();
        private string playerRole = ""; // 현재 플레이어의 직업만 저장

        // 타이머 관련
        private Timer roleViewTimer;     // 직업 확인 타이머
        private Timer gamePhaseTimer;    // 게임 진행 타이머
        private int remainingTime = 8;   // 남은 시간 (초)
        private int dayTime = 50;        // 낮 진행 시간 (초)
        private int nightTime = 30;      // 밤 진행 시간 (초)

        // 글꼴 설정
        private Font titleFont;
        private Font roleFont;
        private Font timerFont;
        private Font descFont;
        private Font phaseFont;
        private Font nameFont;

        // 게임 상태
        private bool gameStarted = false;
        private GamePhase currentPhase = GamePhase.Introduction;
        private int currentDay = 1;

        // 게임 플레이 관련 컴포넌트
        private Panel gamePanel;
        private Panel chatPanel;
        private RichTextBox chatBox;
        private TextBox messageBox;
        private Button sendButton;
        private Button voteButton;
        private Label phaseLabel;
        private Label timeLabel;
        private Label systemMsgLabel;
        private ComboBox playerSelectionBox;
        private Label selectionLabel;
        private Button actionButton;

        // 플레이어 UI 관련
        private List<PictureBox> playerBoxes = new List<PictureBox>();
        private List<Label> playerNameLabels = new List<Label>();

        // 게임 진행 정보
        private List<Player> players = new List<Player>();
        private int votedPlayerId = -1;  // 투표로 선택된 플레이어
        private List<int> deadPlayers = new List<int>();  // 사망한 플레이어 ID
        private Dictionary<int, int> votes = new Dictionary<int, int>();  // 투표 정보 (플레이어ID, 투표대상ID)

        // 밤 행동 저장
        private Dictionary<int, NightAction> nightActions = new Dictionary<int, NightAction>();

        // 보호받은 플레이어
        private int protectedPlayerId = -1;

        // 사망 예정 플레이어
        private List<int> pendingDeaths = new List<int>();

        // AI 관련
        private Dictionary<int, List<int>> aiSuspicions = new Dictionary<int, List<int>>();
        private Random random = new Random();
        // AI별 플레이어 신뢰도 테이블 (0~100, 기본값 50)
        private Dictionary<int, Dictionary<int, int>> aiTrustScores = new Dictionary<int, Dictionary<int, int>>();

        // 랜덤 선언


        #endregion

        #region 생성자
        /// <summary>
        /// 싱글 플레이용 생성자
        /// </summary>
        public SinglePlayGameForm(int playerCount = 8, int aiCount = 4, bool yaminabeMode = false, bool quantumMode = false)
        {
            // 기본 초기화
            InitializeForm(playerCount, aiCount, yaminabeMode, quantumMode);

            // 직업 배정
            AssignRoles();

            // 로컬 플레이어의 직업 설정 (0번째 플레이어)
            playerRole = assignedRoles[0];

            // 직업 이미지 설정
            if (roleImages.ContainsKey(playerRole))
            {
                roleImage = roleImages[playerRole];
            }

            // 플레이어 목록 생성
            CreatePlayers();

            // AI 초기화
            InitializeAI();

            // 타이머 시작
            roleViewTimer.Start();
        }
        #endregion

        #region 초기화

        /// <summary>
        /// 폼 초기화
        /// </summary>
        private void InitializeForm(int playerCount, int aiCount, bool yaminabeMode, bool quantumMode)
        {
            // 폼 기본 속성 설정
            this.Text = "인랑 게임 - 싱글 플레이";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;

            // 게임 설정 저장
            this.playerCount = playerCount;
            this.aiCount = aiCount;
            this.yaminabeMode = yaminabeMode;
            this.quantumMode = quantumMode;

            // 글꼴 설정
            titleFont = new Font("Noto Sans KR", 32, FontStyle.Bold);
            roleFont = new Font("Noto Sans KR", 36, FontStyle.Bold);
            timerFont = new Font("Noto Sans KR", 24, FontStyle.Bold);
            descFont = new Font("Noto Sans KR", 14, FontStyle.Regular);
            phaseFont = new Font("Noto Sans KR", 28, FontStyle.Bold);
            nameFont = new Font("Noto Sans KR", 10, FontStyle.Bold);

            // 이미지 로드
            LoadRoleImages();
            LoadGameImages();

            // 타이머 설정 - 8초 카운트다운
            roleViewTimer = new Timer
            {
                Interval = 1000 // 1초
            };
            roleViewTimer.Tick += RoleViewTimer_Tick;

            // 게임 진행 타이머
            gamePhaseTimer = new Timer
            {
                Interval = 1000 // 1초
            };
            gamePhaseTimer.Tick += GamePhaseTimer_Tick;

            // 마우스 클릭으로 확인 완료
            this.Click += (sender, e) =>
            {
                if (roleViewTimer.Enabled)
                {
                    StartGame();
                }
            };

            // 게임 화면 초기화 (처음에는 숨김)
            InitializeGameScreen();
        }

        /// <summary>
        /// 직업 이미지 파일 로드
        /// </summary>
        private void LoadRoleImages()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            try
            {
                // 각 이미지 파일이 있는지 확인하고 로드
                TryLoadRoleImage("시민", Path.Combine(resourcePath, "civ1.jpg"));
                TryLoadRoleImage("인랑", Path.Combine(resourcePath, "inrang.jpg"));
                TryLoadRoleImage("점쟁이", Path.Combine(resourcePath, "fortuneTeller.jpg"));
                TryLoadRoleImage("영매", Path.Combine(resourcePath, "medium.jpg"));
                TryLoadRoleImage("사냥꾼", Path.Combine(resourcePath, "hunter.jpg"));
                TryLoadRoleImage("네코마타", Path.Combine(resourcePath, "nekomata.jpg"));
                TryLoadRoleImage("광인", Path.Combine(resourcePath, "madman.jpg"));
                TryLoadRoleImage("여우", Path.Combine(resourcePath, "fox.jpg"));
                TryLoadRoleImage("배덕자", Path.Combine(resourcePath, "immoral.jpg"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 역할 이미지 로드 시도
        /// </summary>
        private void TryLoadRoleImage(string role, string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    roleImages.Add(role, System.Drawing.Image.FromFile(path));
                }
                else
                {
                    Console.WriteLine($"역할 이미지 파일이 없습니다: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"역할 이미지 로드 실패: {role}, {ex.Message}");
            }
        }

        /// <summary>
        /// 게임 UI 이미지 로드
        /// </summary>
        private void LoadGameImages()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            try
            {
                string playerPath = Path.Combine(resourcePath, "player.jpg");
                if (File.Exists(playerPath))
                {
                    playerImage = System.Drawing.Image.FromFile(playerPath);
                }

                string deadPath = Path.Combine(resourcePath, "x.jpg");
                if (File.Exists(deadPath))
                {
                    playerDeadOverlay = System.Drawing.Image.FromFile(deadPath);
                }
                else
                {
                    // 기본 X 오버레이 생성
                    playerDeadOverlay = new Bitmap(100, 100);
                    using (Graphics g = Graphics.FromImage(playerDeadOverlay))
                    {
                        g.Clear(Color.Transparent);
                        using (Pen p = new Pen(Color.Red, 5))
                        {
                            g.DrawLine(p, 10, 10, 90, 90);
                            g.DrawLine(p, 90, 10, 10, 90);
                        }
                    }
                }

                // 투표 오버레이 이미지 (없으면 생성)
                string votePath = Path.Combine(resourcePath, "vote.jpg");
                if (File.Exists(votePath))
                {
                    playerVoteOverlay = System.Drawing.Image.FromFile(votePath);
                }
                else
                {
                    // 기본 투표 오버레이 생성
                    playerVoteOverlay = new Bitmap(100, 100);
                    using (Graphics g = Graphics.FromImage(playerVoteOverlay))
                    {
                        g.Clear(Color.Transparent);
                        g.FillEllipse(new SolidBrush(Color.FromArgb(120, 255, 0, 0)), 10, 10, 80, 80);
                    }
                }

                // 인랑 이미지 (밤 화면용)
                string wolfPath = Path.Combine(resourcePath, "wolf_night.jpg");
                if (File.Exists(wolfPath))
                {
                    wolfImage = System.Drawing.Image.FromFile(wolfPath);
                }
                else if (roleImages.ContainsKey("인랑"))
                {
                    wolfImage = roleImages["인랑"];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"게임 이미지 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 게임 플레이 화면 초기화
        /// </summary>
        private void InitializeGameScreen()
        {
            // 게임 화면 패널
            gamePanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Black,
                Visible = false
            };

            // 상단 정보 표시
            phaseLabel = new Label
            {
                Text = "day 1",
                Font = phaseFont,
                ForeColor = Color.BurlyWood,
                Location = new Point(150, 20),
                Size = new Size(200, 40),
                AutoSize = true
            };

            timeLabel = new Label
            {
                Text = "32",
                Font = timerFont,
                ForeColor = Color.White,
                Location = new Point(350, 20),
                Size = new Size(100, 40),
                AutoSize = true
            };

            // 시스템 메시지 레이블
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

            // 채팅 패널
            chatPanel = new Panel
            {
                Location = new Point(30, 150),
                Size = new Size(270, 330),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(20, 20, 20)
            };

            // 채팅 내용
            chatBox = new RichTextBox
            {
                Location = new Point(10, 10),
                Size = new Size(250, 270),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            // 채팅 입력란
            messageBox = new TextBox
            {
                Location = new Point(10, 290),
                Size = new Size(170, 25),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

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

            // 설명 레이블
            Label chatLabel = new Label
            {
                Text = "전체 공개 채팅방",
                Location = new Point(30, 130),
                Size = new Size(270, 20),
                ForeColor = Color.BurlyWood,
                Font = new Font("Noto Sans KR", 10),
                TextAlign = ContentAlignment.MiddleCenter
            };

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

            // 행동 버튼 (투표 또는 능력 사용)
            actionButton = new Button
            {
                Text = "kill",
                Location = new Point(540, 240),
                Size = new Size(70, 30),
                BackColor = Color.BurlyWood,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 11, FontStyle.Bold),
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
                Font = new Font("Arial", 11, FontStyle.Bold)
            };

            // 이벤트 연결
            sendButton.Click += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(messageBox.Text))
                {
                    // 채팅 메시지 추가
                    string message = messageBox.Text;

                    if (currentPhase == GamePhase.Night && IsWerewolf(0))
                    {
                        // 인랑 전용 채팅 (밤에만 가능)
                        SendWerewolfChat(message);
                    }
                    else if (currentPhase == GamePhase.Day)
                    {
                        // 일반 채팅 (낮에만 가능)
                        AddChatMessage("나", message);
                        SimulateAIResponse(message);
                    }

                    messageBox.Clear();
                }
            };

            voteButton.Click += (sender, e) =>
            {
                if (currentPhase == GamePhase.Day)
                {
                    ShowVoteDialog();
                }
                else
                {
                    MessageBox.Show("투표는 낮에만 가능합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            actionButton.Click += (sender, e) =>
            {
                if (playerSelectionBox.SelectedIndex >= 0)
                {
                    int targetId = GetPlayerIdFromSelection(playerSelectionBox.SelectedItem.ToString());
                    PerformNightAction(0, targetId);

                    // 능력 사용 후 UI 비활성화
                    selectionLabel.Visible = false;
                    playerSelectionBox.Visible = false;
                    actionButton.Visible = false;
                }
                else
                {
                    MessageBox.Show("대상 플레이어를 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            // 컴포넌트 추가
            chatPanel.Controls.Add(chatBox);
            chatPanel.Controls.Add(messageBox);
            chatPanel.Controls.Add(sendButton);

            // 게임 패널에 컴포넌트 추가
            gamePanel.Controls.Add(phaseLabel);
            gamePanel.Controls.Add(timeLabel);
            gamePanel.Controls.Add(systemMsgLabel);
            gamePanel.Controls.Add(chatPanel);
            gamePanel.Controls.Add(chatLabel);
            gamePanel.Controls.Add(voteButton);
            gamePanel.Controls.Add(selectionLabel);
            gamePanel.Controls.Add(playerSelectionBox);
            gamePanel.Controls.Add(actionButton);

            // 플레이어 상태 UI 생성
            CreatePlayerStatusUI();

            // 폼에 게임 패널 추가
            this.Controls.Add(gamePanel);
        }

        /// <summary>
        /// 플레이어 상태 UI 생성 - 플레이어 일러스트를 더 넓게 퍼트림
        /// </summary>
        private void CreatePlayerStatusUI()
        {
            int playerImageSize = 70;
            int spacing = 30; // 간격 증가
            int startX = 350;
            int startY = 180; // 시작 지점을 아래로 이동
            int playersPerRow = 4;

            for (int i = 0; i < playerCount; i++)
            {
                int row = i / playersPerRow;
                int col = i % playersPerRow;
                int x = startX + col * (playerImageSize + spacing);
                int y = startY + row * (playerImageSize + spacing + 20);

                // 플레이어 이미지
                PictureBox playerBox = new PictureBox
                {
                    Location = new Point(x, y),
                    Size = new Size(playerImageSize, playerImageSize),
                    Image = playerImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Tag = i // 플레이어 ID 저장
                };

                // 플레이어 이름 레이블
                Label playerName = new Label
                {
                    Text = i == 0 ? "player 1" : $"player {i + 1}",
                    Location = new Point(x, y + playerImageSize + 5),
                    Size = new Size(playerImageSize, 15),
                    ForeColor = Color.BurlyWood,
                    Font = nameFont,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                playerBoxes.Add(playerBox);
                playerNameLabels.Add(playerName);

                gamePanel.Controls.Add(playerBox);
                gamePanel.Controls.Add(playerName);
            }
        }

        /// <summary>
        /// 플레이어 객체 생성
        /// </summary>
        private void CreatePlayers()
        {
            players.Clear();

            // 로컬 플레이어 (0번 플레이어)
            players.Add(new Player
            {
                Id = 0,
                Name = "player 1",
                Role = assignedRoles[0],
                IsAI = false,
                IsAlive = true
            });

            // AI 플레이어
            for (int i = 1; i < playerCount; i++)
            {
                players.Add(new Player
                {
                    Id = i,
                    Name = $"player {i + 1}",
                    Role = assignedRoles[i],
                    IsAI = true,
                    IsAlive = true
                });
            }
        }

        ///// <summary> (옛날버전0525이전)AI 초기화 - 의심 리스트 등
        ///// AI 초기화 - 의심 리스트 등
        ///// </summary>
        //private void InitializeAI()
        //{
        //    // AI별 의심 리스트 초기화
        //    for (int i = 1; i < playerCount; i++)
        //    {
        //        aiSuspicions[i] = new List<int>();

        //        // 초기 의심 리스트는 랜덤 플레이어 1-2명
        //        int suspicionCount = random.Next(1, 3);
        //        while (aiSuspicions[i].Count < suspicionCount)
        //        {
        //            int suspectedId = random.Next(0, playerCount);
        //            // 자기 자신, 같은 인랑, 이미 의심 중인 플레이어는 제외
        //            if (suspectedId != i &&
        //                !aiSuspicions[i].Contains(suspectedId) &&
        //                !(IsWerewolf(i) && IsWerewolf(suspectedId)))
        //            {
        //                aiSuspicions[i].Add(suspectedId);
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// AI 초기화 - 의심 리스트 및 신뢰도 테이블
        /// </summary>
        private void InitializeAI()
        {
            // AI별 의심 리스트 초기화
            for (int i = 1; i < playerCount; i++)
            {
                aiSuspicions[i] = new List<int>();

                // 초기 의심 리스트는 랜덤 플레이어 1-2명
                int suspicionCount = random.Next(1, 3);
                while (aiSuspicions[i].Count < suspicionCount)
                {
                    int suspectedId = random.Next(0, playerCount);
                    // 자기 자신, 같은 인랑, 이미 의심 중인 플레이어는 제외
                    if (suspectedId != i &&
                        !aiSuspicions[i].Contains(suspectedId) &&
                        !(IsWerewolf(i) && IsWerewolf(suspectedId)))
                    {
                        aiSuspicions[i].Add(suspectedId);
                    }
                }
            }

            // ✅ AI 신뢰도 초기화 (랜덤 분산: 45~55)
            foreach (var ai in players.Where(p => p.IsAI))
            {
                aiTrustScores[ai.Id] = new Dictionary<int, int>();
                foreach (var target in players)
                {
                    if (ai.Id == target.Id)
                    {
                        aiTrustScores[ai.Id][target.Id] = 100; // 자기 자신은 무조건 신뢰
                    }
                    else
                    {
                        aiTrustScores[ai.Id][target.Id] = 45 + random.Next(11); // 45~55
                    }
                }
            }
        }



        #endregion

        #region 직업 배정

        /// <summary>
        /// 직업 배정
        /// </summary>
        private void AssignRoles()
        {
            Random random = new Random();

            if (yaminabeMode)
            {
                AssignYaminabeRoles(random);
            }
            else
            {
                AssignStandardRoles(random);
            }
        }

        /// <summary>
        /// 표준 모드 직업 배정 - 인원 조정 요구사항 반영
        /// </summary>
        private void AssignStandardRoles(Random random)
        {
            List<string> availableRoles = new List<string>();

            // 플레이어 수에 따른 인랑측 인원 결정 (인랑 + 광인)
            int wolfTeamCount;
            int wolfCount;
            int madmanCount;

            if (playerCount < 6)
            {
                // 6명 미만: 인랑 1명, 광인 0명
                wolfTeamCount = 1;
                wolfCount = 1;
                madmanCount = 0;
            }
            else if (playerCount <= 9)
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
            int mediumCount = playerCount >= 7 ? 1 : 0;  // 영매 (7명 이상일 때)
            int hunterCount = playerCount >= 6 ? 1 : 0;  // 사냥꾼 (6명 이상일 때)
            int foxCount = playerCount >= 8 ? 1 : 0;  // 여우 (8명 이상일 때)
            int immoralCount = foxCount > 0 && playerCount >= 9 ? 1 : 0;  // 배덕자 (9명 이상이고 여우가 있을 때)
            int nekomataCount = playerCount >= 10 ? 1 : 0;  // 네코마타 (10명 이상일 때)

            // 나머지는 시민으로 채움
            int civilianCount = playerCount - (wolfCount + fortuneTellerCount + mediumCount +
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
        }

        /// <summary>
        /// 야미나베 모드 직업 배정
        /// </summary>
        private void AssignYaminabeRoles(Random random)
        {
            // 모든 가능한 직업 목록
            List<string> allRoles = new List<string>
            {
                "시민", "점쟁이", "영매", "사냥꾼", "네코마타",
                "인랑", "광인",
                "여우", "배덕자"
            };

            // 최소 1명의 인랑은 보장
            assignedRoles.Add("인랑");

            // 나머지 인원 랜덤 배정
            for (int i = 1; i < playerCount; i++)
            {
                int randomIndex = random.Next(allRoles.Count);
                assignedRoles.Add(allRoles[randomIndex]);
            }

            // 결과 셔플
            assignedRoles = assignedRoles.OrderBy(x => random.Next()).ToList();
        }

        #endregion

        #region 타이머 및 전환

        /// <summary>
        /// 타이머 틱 이벤트 - 카운트다운 처리
        /// </summary>
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

        /// <summary>
        /// 게임 진행 타이머 이벤트
        /// </summary>
        private void GamePhaseTimer_Tick(object sender, EventArgs e)
        {
            remainingTime--;

            // 타이머 표시 업데이트
            timeLabel.Text = remainingTime.ToString();

            // 남은 시간이 10초 이하면 빨간색으로 표시
            if (remainingTime <= 10)
            {
                timeLabel.ForeColor = Color.Red;
            }

            // 타이머 종료 시 다음 단계로
            if (remainingTime <= 0)
            {
                AdvanceToNextPhase();
            }
        }

        /// <summary>
        /// 게임 시작
        /// </summary>
        private void StartGame()
        {
            // 타이머 중지
            roleViewTimer.Stop();

            // 게임 시작 상태로 변경
            gameStarted = true;

            // 게임 패널 표시
            gamePanel.Visible = true;

            // 시작 단계 설정
            SetGamePhase(GamePhase.Day);

            // 시작 메시지 추가
            AddSystemMessage("게임이 시작되었습니다. 낮 토론을 시작하세요.");
            AddChatMessage("System", "게임이 시작되었습니다. 첫날 토론을 시작하세요.");
            AddChatMessage("System", $"당신의 역할은 {playerRole}입니다.");

            // 인랑 팀 정보 제공 (인랑이나 광인인 경우)
            if (IsWerewolf(0) || playerRole == "광인")
            {
                string wolfInfo = "인랑 팀 정보: ";
                List<string> wolfPlayers = new List<string>();

                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Role == "인랑")
                    {
                        wolfPlayers.Add(players[i].Name);
                    }
                }

                wolfInfo += string.Join(", ", wolfPlayers);
                AddChatMessage("System", wolfInfo);
            }

            // 배덕자에게 여우 정보 제공
            if (playerRole == "배덕자")
            {
                string foxInfo = "여우 정보: ";
                List<string> foxPlayers = new List<string>();

                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Role == "여우")
                    {
                        foxPlayers.Add(players[i].Name);
                    }
                }

                if (foxPlayers.Count > 0)
                {
                    foxInfo += string.Join(", ", foxPlayers);
                }
                else
                {
                    foxInfo += "이번 게임에 여우가 없습니다.";
                }

                AddChatMessage("System", foxInfo);
            }

            // AI 인사 메시지 시뮬레이션
            SimulateAIGreetings();

            // 화면 갱신
            this.Invalidate();
        }

        /// <summary>
        /// 다음 게임 단계로 진행
        /// </summary>
        private void AdvanceToNextPhase()
        {
            gamePhaseTimer.Stop();

            // 현재 단계에 따라 다음 단계 설정
            switch (currentPhase)
            {
                case GamePhase.Day:
                    // 투표 결과 계산
                    CalculateDayVoteResult();
                    SetGamePhase(GamePhase.DayResult);
                    break;

                case GamePhase.DayResult:
                    // 밤 단계로 이동
                    SetGamePhase(GamePhase.Night);
                    break;

                case GamePhase.Night:
                    // 밤 결과 계산 - AI의 밤 행동 실행
                    SimulateAINightActions();
                    CalculateNightResult();
                    SetGamePhase(GamePhase.NightResult);
                    break;

                case GamePhase.NightResult:
                    // 날짜 증가 후 다음 날 낮으로 이동
                    currentDay++;
                    SetGamePhase(GamePhase.Day);
                    break;

                case GamePhase.GameEnd:
                    // 게임 종료
                    this.Close();
                    break;
            }
        }

        /// <summary>
        /// 게임 단계 설정
        /// </summary>
        private void SetGamePhase(GamePhase phase)
        {
            currentPhase = phase;

            // 단계별 UI 및 타이머 설정
            switch (phase)
            {
                case GamePhase.Day:
                    phaseLabel.Text = $"day {currentDay}";
                    remainingTime = dayTime;
                    timeLabel.ForeColor = Color.White;
                    AddSystemMessage("낮이 되었습니다. 토론을 시작하세요.");

                    // 투표 버튼 활성화
                    voteButton.Visible = true;

                    // 밤 전용 UI 숨기기
                    selectionLabel.Visible = false;
                    playerSelectionBox.Visible = false;
                    actionButton.Visible = false;

                    // 메시지 입력란 활성화
                    messageBox.Enabled = true;
                    sendButton.Enabled = true;

                    // 게임 종료 체크
                    CheckGameEndCondition();
                    break;

                case GamePhase.DayResult:
                    remainingTime = 5; // 투표 결과 표시 시간
                    timeLabel.ForeColor = Color.White;
                    AddSystemMessage("투표 결과를 발표합니다.");

                    // 투표 결과 표시
                    ShowVoteResult();

                    // 투표 버튼 비활성화
                    voteButton.Visible = false;

                    // 메시지 입력란 비활성화
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;
                    break;

                case GamePhase.Night:
                    phaseLabel.Text = "night";
                    remainingTime = nightTime;
                    timeLabel.ForeColor = Color.White;
                    AddSystemMessage("밤이 되었습니다. 능력을 사용하세요.");

                    // 밤 전용 UI 설정
                    SetupNightUI();

                    // 투표 버튼 비활성화
                    voteButton.Visible = false;

                    // 메시지 입력란 - 인랑만 채팅 가능
                    messageBox.Enabled = IsWerewolf(0);
                    sendButton.Enabled = IsWerewolf(0);

                    // 야간 행동 초기화
                    nightActions.Clear();
                    protectedPlayerId = -1;
                    pendingDeaths.Clear();
                    break;

                case GamePhase.NightResult:
                    remainingTime = 5; // 밤 결과 표시 시간
                    timeLabel.ForeColor = Color.White;
                    AddSystemMessage("밤 사이 일어난 일을 발표합니다.");

                    // 밤 전용 UI 숨기기
                    selectionLabel.Visible = false;
                    playerSelectionBox.Visible = false;
                    actionButton.Visible = false;

                    // 메시지 입력란 비활성화
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;

                    // 밤 결과 표시
                    ShowNightResult();
                    break;

                case GamePhase.GameEnd:
                    remainingTime = 10;
                    timeLabel.ForeColor = Color.White;
                    AddSystemMessage("게임이 종료되었습니다.");

                    // 승리 팀 표시
                    ShowGameResult();

                    // 모든 버튼 비활성화
                    voteButton.Visible = false;
                    selectionLabel.Visible = false;
                    playerSelectionBox.Visible = false;
                    actionButton.Visible = false;
                    messageBox.Enabled = false;
                    sendButton.Enabled = false;
                    break;
            }

            // 타이머 시작
            gamePhaseTimer.Start();
        }

        /// <summary>
        /// 낮 투표 결과 계산
        /// </summary>
        private void CalculateDayVoteResult()
        {
            // 모든 살아있는 플레이어가 투표했는지 확인
            EnsureAllPlayersVoted();

            // 플레이어의 투표 결과 계산
            Dictionary<int, int> voteCount = new Dictionary<int, int>();

            // 투표 받은 횟수 계산
            foreach (var vote in votes.Values)
            {
                if (!voteCount.ContainsKey(vote))
                    voteCount[vote] = 0;
                voteCount[vote]++;
            }

            // 최다 득표자 찾기
            int maxVotes = 0;
            votedPlayerId = -1;
            List<int> tiePlayers = new List<int>();

            foreach (var pair in voteCount)
            {
                if (pair.Value > maxVotes)
                {
                    maxVotes = pair.Value;
                    votedPlayerId = pair.Key;
                    tiePlayers.Clear();
                    tiePlayers.Add(pair.Key);
                }
                else if (pair.Value == maxVotes) // 동률
                {
                    tiePlayers.Add(pair.Key);
                }
            }

            // 동률일 경우 추첨
            if (tiePlayers.Count > 1)
            {
                int randomIndex = random.Next(tiePlayers.Count);
                votedPlayerId = tiePlayers[randomIndex];
            }
        }

        /// <summary>
        /// 모든 플레이어가 투표했는지 확인하고, 안했으면 랜덤 투표
        /// </summary>
        private void EnsureAllPlayersVoted()
        {
            foreach (var player in players)
            {
                if (player.IsAlive && !votes.ContainsKey(player.Id))
                {
                    // AI의 투표가 없는 경우 랜덤 투표
                    if (player.IsAI)
                    {
                        int randomTarget;
                        // AI의 의심 리스트에서 먼저 선택
                        if (aiSuspicions.ContainsKey(player.Id) && aiSuspicions[player.Id].Count > 0)
                        {
                            List<int> validSuspicions = aiSuspicions[player.Id]
                                .Where(id => id != player.Id && players[id].IsAlive)
                                .ToList();

                            if (validSuspicions.Count > 0)
                            {
                                randomTarget = validSuspicions[random.Next(validSuspicions.Count)];
                            }
                            else
                            {
                                randomTarget = GetRandomValidTarget(player.Id);
                            }
                        }
                        else
                        {
                            randomTarget = GetRandomValidTarget(player.Id);
                        }

                        votes[player.Id] = randomTarget;
                    }
                    else // 플레이어가 투표 안했으면 랜덤 대상 선택
                    {
                        votes[player.Id] = GetRandomValidTarget(player.Id);
                    }
                }
            }
        }

        /// <summary>
        /// 유효한 투표 대상 랜덤 선택
        /// </summary>
        private int GetRandomValidTarget(int playerId)
        {
            List<int> possibleTargets = players
                .Where(p => p.IsAlive && p.Id != playerId)
                .Select(p => p.Id)
                .ToList();

            if (possibleTargets.Count > 0)
            {
                return possibleTargets[random.Next(possibleTargets.Count)];
            }

            return -1; // 유효한 대상이 없음
        }

        /// <summary>
        /// 투표 결과 표시
        /// </summary>
        private void ShowVoteResult()
        {
            if (votedPlayerId != -1 && players.Any(p => p.Id == votedPlayerId))
            {
                Player votedPlayer = players.First(p => p.Id == votedPlayerId);
                string message = $"{votedPlayer.Name}이(가) 투표로 처형되었습니다.";

                // 직업 공개는 하지 않음 (마피아 게임 규칙)

                AddSystemMessage(message);
                AddChatMessage("System", message);

                // 사망 처리
                KillPlayer(votedPlayerId);

                // 투표 결과 표시 - 이미지에 X 표시
                UpdatePlayerUI();
            }
            else
            {
                AddSystemMessage("아무도 처형되지 않았습니다.");
                AddChatMessage("System", "아무도 처형되지 않았습니다.");
            }

            // 투표 정보 초기화
            votes.Clear();
            votedPlayerId = -1;
        }

        /// <summary>
        /// AI의 밤 행동 시뮬레이션
        /// </summary>
        private void SimulateAINightActions()
        {
            // 인랑 AI의 습격 대상 선택
            SimulateWerewolfNightActions();

            // 점쟁이 AI의 점 대상 선택
            SimulateFortuneTellerNightActions();

            // 사냥꾼 AI의 보호 대상 선택
            SimulateHunterNightActions();

            // 영매 AI의 능력 사용
            SimulateMediumNightActions();
        }

        /// <summary>
        /// 인랑 AI의 밤 행동 시뮬레이션
        /// </summary>
        private void SimulateWerewolfNightActions()
        {
            // 인랑 팀 플레이어들 찾기
            List<Player> werewolves = players
                .Where(p => p.IsAlive && p.Role == "인랑")
                .ToList();

            if (werewolves.Count == 0) return;

            // 플레이어가 인랑인 경우, 이미 선택했는지 확인
            bool playerIsWerewolf = players[0].Role == "인랑" && players[0].IsAlive;
            bool playerHasActed = nightActions.ContainsKey(0) && nightActions[0].ActionType == NightActionType.Kill;

            // 습격 대상 선택 (인랑들이 합의)
            int targetId = -1;

            // 플레이어가 이미 선택한 경우 그 대상을 사용
            if (playerIsWerewolf && playerHasActed)
            {
                targetId = nightActions[0].TargetId;
            }
            else
            {
                // AI 인랑들이 선택 (의심 리스트 또는 랜덤)
                List<int> possibleTargets = players
                    .Where(p => p.IsAlive && p.Role != "인랑" && p.Role != "광인")
                    .Select(p => p.Id)
                    .ToList();

                if (possibleTargets.Count > 0)
                {
                    // 의심 리스트에서 우선 선택
                    List<int> suspectedTargets = new List<int>();
                    foreach (var wolf in werewolves)
                    {
                        if (wolf.IsAI && aiSuspicions.ContainsKey(wolf.Id))
                        {
                            suspectedTargets.AddRange(
                                aiSuspicions[wolf.Id]
                                .Where(id => possibleTargets.Contains(id))
                            );
                        }
                    }

                    // 가장 많이 의심받는 대상 선택
                    if (suspectedTargets.Count > 0)
                    {
                        var mostSuspected = suspectedTargets
                            .GroupBy(id => id)
                            .OrderByDescending(g => g.Count())
                            .First()
                            .Key;

                        targetId = mostSuspected;
                    }
                    else
                    {
                        // 랜덤 선택
                        targetId = possibleTargets[random.Next(possibleTargets.Count)];
                    }
                }
            }

            // 타겟이 유효하면 모든 인랑의 행동 등록
            if (targetId != -1)
            {
                foreach (var wolf in werewolves)
                {
                    if (wolf.IsAI)
                    {
                        nightActions[wolf.Id] = new NightAction
                        {
                            PlayerId = wolf.Id,
                            ActionType = NightActionType.Kill,
                            TargetId = targetId
                        };
                    }
                }

                // 인랑 공통 타겟 설정
                pendingDeaths.Add(targetId);
            }
        }

        /// <summary>
        /// 점쟁이 AI의 밤 행동 시뮬레이션
        /// </summary>
        private void SimulateFortuneTellerNightActions()
        {
            // AI 점쟁이 찾기
            var fortuneTeller = players
                .FirstOrDefault(p => p.IsAlive && p.IsAI && p.Role == "점쟁이");

            if (fortuneTeller == null) return;

            // 점 대상 선택 (가장 의심스러운 사람 또는 랜덤)
            int targetId = -1;

            // 의심 리스트에서 우선 선택
            if (aiSuspicions.ContainsKey(fortuneTeller.Id) && aiSuspicions[fortuneTeller.Id].Count > 0)
            {
                List<int> validSuspicions = aiSuspicions[fortuneTeller.Id]
                    .Where(id => id != fortuneTeller.Id && players[id].IsAlive)
                    .ToList();

                if (validSuspicions.Count > 0)
                {
                    targetId = validSuspicions[random.Next(validSuspicions.Count)];
                }
            }

            // 의심 리스트가 없거나 비어있으면 랜덤 선택
            if (targetId == -1)
            {
                List<int> possibleTargets = players
                    .Where(p => p.IsAlive && p.Id != fortuneTeller.Id)
                    .Select(p => p.Id)
                    .ToList();

                if (possibleTargets.Count > 0)
                {
                    targetId = possibleTargets[random.Next(possibleTargets.Count)];
                }
            }

            // 타겟이 유효하면 점쟁이 행동 등록
            if (targetId != -1)
            {
                nightActions[fortuneTeller.Id] = new NightAction
                {
                    PlayerId = fortuneTeller.Id,
                    ActionType = NightActionType.Check,
                    TargetId = targetId
                };

                // 점 결과에 따라 의심 리스트 업데이트
                Player target = players[targetId];
                bool isWerewolf = target.Role == "인랑";
                bool isAppearAsWerewolf = isWerewolf && target.Role != "광인"; // 광인은 인랑이 아닌 것으로 보임

                if (isAppearAsWerewolf)
                {
                    // 인랑으로 확인되면 항상 의심 리스트에 추가
                    if (!aiSuspicions[fortuneTeller.Id].Contains(targetId))
                    {
                        aiSuspicions[fortuneTeller.Id].Add(targetId);
                    }
                }
                else
                {
                    // 인랑이 아니면 의심 리스트에서 제거
                    aiSuspicions[fortuneTeller.Id].Remove(targetId);
                }

                // 여우는 점쟁이에 의해 죽음
                if (target.Role == "여우")
                {
                    pendingDeaths.Add(targetId);
                }
            }
        }

        /// <summary>
        /// 사냥꾼 AI의 밤 행동 시뮬레이션
        /// </summary>
        private void SimulateHunterNightActions()
        {
            // AI 사냥꾼 찾기
            var hunter = players
                .FirstOrDefault(p => p.IsAlive && p.IsAI && p.Role == "사냥꾼");

            if (hunter == null) return;

            // 보호 대상 선택 (가장 보호가 필요한 사람 또는 랜덤)
            int targetId = -1;

            // 점쟁이, 영매 등 중요한 역할을 우선 보호
            var priorityRoles = new[] { "점쟁이", "영매" };

            List<Player> priorityTargets = players
                .Where(p => p.IsAlive && p.Id != hunter.Id && priorityRoles.Contains(p.Role))
                .ToList();

            if (priorityTargets.Count > 0)
            {
                // 우선순위 역할 중 랜덤 선택
                targetId = priorityTargets[random.Next(priorityTargets.Count)].Id;
            }
            else
            {
                // 우선순위 역할이 없으면 의심하지 않는 사람 중에서 선택
                List<int> trustedPlayers = players
                    .Where(p => p.IsAlive && p.Id != hunter.Id &&
                        (!aiSuspicions.ContainsKey(hunter.Id) || !aiSuspicions[hunter.Id].Contains(p.Id)))
                    .Select(p => p.Id)
                    .ToList();

                if (trustedPlayers.Count > 0)
                {
                    targetId = trustedPlayers[random.Next(trustedPlayers.Count)];
                }
                else
                {
                    // 모두 의심되면 랜덤 선택
                    List<int> possibleTargets = players
                        .Where(p => p.IsAlive && p.Id != hunter.Id)
                        .Select(p => p.Id)
                        .ToList();

                    if (possibleTargets.Count > 0)
                    {
                        targetId = possibleTargets[random.Next(possibleTargets.Count)];
                    }
                }
            }

            // 타겟이 유효하면 사냥꾼 행동 등록
            if (targetId != -1)
            {
                nightActions[hunter.Id] = new NightAction
                {
                    PlayerId = hunter.Id,
                    ActionType = NightActionType.Protect,
                    TargetId = targetId
                };

                // 보호받은 플레이어 설정
                protectedPlayerId = targetId;
            }
        }

        /// <summary>
        /// 영매 AI의 밤 행동 시뮬레이션
        /// </summary>
        private void SimulateMediumNightActions()
        {
            // AI 영매 찾기
            var medium = players
                .FirstOrDefault(p => p.IsAlive && p.IsAI && p.Role == "영매");

            if (medium == null) return;

            // 직전에 처형된 사람 확인
            if (votedPlayerId != -1)
            {
                nightActions[medium.Id] = new NightAction
                {
                    PlayerId = medium.Id,
                    ActionType = NightActionType.Identify,
                    TargetId = votedPlayerId
                };

                // 처형된 사람의 역할에 따라 의심 리스트 업데이트
                Player executedPlayer = players[votedPlayerId];
                bool wasWerewolf = executedPlayer.Role == "인랑";

                // 영매는 처형된 사람의 진짜 직업을 알 수 있음
                if (wasWerewolf)
                {
                    // 인랑이었다면 의심 리스트에서 제거
                    aiSuspicions[medium.Id].Remove(votedPlayerId);
                }
                else
                {
                    // 인랑이 아니었다면, 그를 지목한 사람들을 의심
                    foreach (var vote in votes)
                    {
                        if (vote.Value == votedPlayerId && players[vote.Key].IsAlive)
                        {
                            if (!aiSuspicions[medium.Id].Contains(vote.Key))
                            {
                                aiSuspicions[medium.Id].Add(vote.Key);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 밤 결과 계산
        /// </summary>
        private void CalculateNightResult()
        {
            // 여기서는 pendingDeaths에 죽을 예정인 플레이어들이 이미 들어가 있음

            // 보호받은 플레이어는 사망에서 제외
            if (protectedPlayerId != -1)
            {
                pendingDeaths.Remove(protectedPlayerId);
            }

            // 네코마타 능력 처리 (인랑에 의해 죽으면 인랑도 죽음)
            ProcessNekomataAbility();

            // 실제 사망 처리
            foreach (int playerId in pendingDeaths)
            {
                KillPlayer(playerId);
            }
        }

        /// <summary>
        /// 네코마타 특수 능력 처리
        /// </summary>
        private void ProcessNekomataAbility()
        {
            List<int> additionalDeaths = new List<int>();

            foreach (int deadId in pendingDeaths)
            {
                if (deadId < players.Count && players[deadId].Role == "네코마타")
                {
                    // 네코마타가 인랑에게 죽는 경우, 죽인 인랑도 죽음
                    foreach (var action in nightActions.Values)
                    {
                        if (action.ActionType == NightActionType.Kill &&
                            action.TargetId == deadId &&
                            !pendingDeaths.Contains(action.PlayerId))
                        {
                            additionalDeaths.Add(action.PlayerId);
                        }
                    }
                }
            }

            // 추가 사망자 등록
            foreach (int id in additionalDeaths)
            {
                if (!pendingDeaths.Contains(id))
                {
                    pendingDeaths.Add(id);
                }
            }
        }

        /// <summary>
        /// 밤 결과 표시
        /// </summary>
        private void ShowNightResult()
        {
            bool anyDeath = false;

            // 사망자 목록 처리
            foreach (int playerId in pendingDeaths)
            {
                if (playerId < players.Count && !players[playerId].IsAlive)
                {
                    anyDeath = true;
                    string message = $"{players[playerId].Name}이(가) 밤 사이 사망했습니다.";
                    AddChatMessage("System", message);
                }
            }

            // 보호 결과 처리 (인랑의 습격을 막은 경우만)
            bool protectionWorked = false;
            if (protectedPlayerId != -1)
            {
                foreach (var action in nightActions.Values)
                {
                    if (action.ActionType == NightActionType.Kill && action.TargetId == protectedPlayerId)
                    {
                        protectionWorked = true;
                        break;
                    }
                }
            }

            // 사망자 요약 메시지
            if (anyDeath)
            {
                AddSystemMessage("밤 사이 사망자가 발생했습니다.");
            }
            else if (protectionWorked)
            {
                AddSystemMessage("사냥꾼의 보호로 인해 습격이 실패했습니다.");
                AddChatMessage("System", "사냥꾼의 보호로 인해 습격이 실패했습니다.");
            }
            else
            {
                AddSystemMessage("밤 사이 아무 일도 일어나지 않았습니다.");
                AddChatMessage("System", "밤 사이 아무 일도 일어나지 않았습니다.");
            }

            // 플레이어 UI 업데이트
            UpdatePlayerUI();

            // 행동 초기화
            pendingDeaths.Clear();
            nightActions.Clear();
        }

        /// <summary>
        /// 게임 결과 표시
        /// </summary>
        private void ShowGameResult()
        {
            // 게임 승리 조건 확인
            string resultMessage = "게임이 종료되었습니다.\n";
            bool villageWin = false;
            bool werewolfWin = false;
            bool foxWin = false;

            // 생존 인원 확인
            int aliveVillagers = players.Count(p => p.IsAlive && IsVillager(p.Id));
            int aliveWerewolves = players.Count(p => p.IsAlive && p.Role == "인랑");
            int aliveFoxes = players.Count(p => p.IsAlive && p.Role == "여우");

            // 승리 조건 체크
            if (aliveWerewolves == 0)
            {
                villageWin = true;
                resultMessage += "마을 팀이 승리했습니다! 모든 인랑이 제거되었습니다.";
            }
            else if (aliveWerewolves >= aliveVillagers)
            {
                werewolfWin = true;
                resultMessage += "인랑 팀이 승리했습니다! 인랑이 마을을 장악했습니다.";
            }

            // 여우 승리 조건 (생존)
            if (aliveFoxes > 0)
            {
                foxWin = true;
                resultMessage += "\n여우가 생존하여 여우 진영이 승리했습니다!";
            }

            // 모든 플레이어의 직업 공개 (게임 종료 시에만)
            resultMessage += "\n\n플레이어 정보:";
            foreach (var player in players)
            {
                string status = player.IsAlive ? "생존" : "사망";
                resultMessage += $"\n{player.Name}: {player.Role} ({status})";
            }

            MessageBox.Show(resultMessage, "게임 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 밤 UI 설정
        /// </summary>
        private void SetupNightUI()
        {
            // 플레이어 선택 콤보박스 업데이트
            playerSelectionBox.Items.Clear();

            // 자신의 직업에 따른 UI 설정
            string myRole = playerRole;

            if (myRole == "인랑" && players[0].IsAlive)
            {
                // 인랑 UI - 습격 대상 선택
                selectionLabel.Text = "습격할 대상 선택";
                actionButton.Text = "kill";

                // 습격 가능한 대상 추가 (인랑이 아닌 생존자)
                foreach (var player in players)
                {
                    if (player.IsAlive && player.Role != "인랑" && player.Role != "광인" && player.Id != 0)
                    {
                        playerSelectionBox.Items.Add(player.Name);
                    }
                }

                selectionLabel.Visible = true;
                playerSelectionBox.Visible = true;
                actionButton.Visible = true;
            }
            else if (myRole == "점쟁이" && players[0].IsAlive)
            {
                // 점쟁이 UI - 점 대상 선택
                selectionLabel.Text = "점을 볼 대상 선택";
                actionButton.Text = "점 보기";

                // 점 가능한 대상 추가 (자신 제외 생존자)
                foreach (var player in players)
                {
                    if (player.IsAlive && player.Id != 0)
                    {
                        playerSelectionBox.Items.Add(player.Name);
                    }
                }

                selectionLabel.Visible = true;
                playerSelectionBox.Visible = true;
                actionButton.Visible = true;
            }
            else if (myRole == "사냥꾼" && players[0].IsAlive)
            {
                // 사냥꾼 UI - 보호 대상 선택
                selectionLabel.Text = "보호할 대상 선택";
                actionButton.Text = "보호";

                // 보호 가능한 대상 추가 (자신 제외 생존자)
                foreach (var player in players)
                {
                    if (player.IsAlive && player.Id != 0)
                    {
                        playerSelectionBox.Items.Add(player.Name);
                    }
                }

                selectionLabel.Visible = true;
                playerSelectionBox.Visible = true;
                actionButton.Visible = true;
            }
            else if (myRole == "영매" && players[0].IsAlive && votedPlayerId != -1)
            {
                // 영매 UI - 처형된 사람의 직업 확인
                selectionLabel.Text = "영매 능력";
                actionButton.Text = "사망자 확인";

                playerSelectionBox.Items.Add(players[votedPlayerId].Name);

                selectionLabel.Visible = true;
                playerSelectionBox.Visible = true;
                actionButton.Visible = true;
            }
            else
            {
                // 능력이 없는 직업이거나 자신의 능력을 사용할 수 없는 상황
                selectionLabel.Visible = false;
                playerSelectionBox.Visible = false;
                actionButton.Visible = false;
                return;
            }

            // 기본 선택
            if (playerSelectionBox.Items.Count > 0)
            {
                playerSelectionBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 밤 능력 수행 (플레이어)
        /// </summary>
        //private void PerformNightAction(int playerId, int targetId)
        //{
        //    if (targetId == -1) return;

        //    string myRole = players[playerId].Role;

        //    if (myRole == "인랑")
        //    {
        //        // 인랑 - 습격
        //        AddChatMessage("System", $"{players[targetId].Name}을(를) 습격했습니다.");

        //        // 행동 등록
        //        nightActions[playerId] = new NightAction
        //        {
        //            PlayerId = playerId,
        //            ActionType = NightActionType.Kill,
        //            TargetId = targetId
        //        };

        //        // 습격 예정 등록
        //        if (!pendingDeaths.Contains(targetId))
        //        {
        //            pendingDeaths.Add(targetId);
        //        }

        //        // AI 인랑들도 같은 플레이어 습격
        //        foreach (var player in players)
        //        {
        //            if (player.IsAI && player.IsAlive && player.Role == "인랑")
        //            {
        //                // AI 인랑들은 같은 대상 습격에 동의 (채팅으로만 표시)
        //                AddWerewolfChatMessage(player.Name, $"{players[targetId].Name}을(를) 습격하는데 동의합니다.");
        //            }
        //        }
        //    }
        //    else if (myRole == "점쟁이")
        //    {
        //        // 점쟁이 - 점 보기
        //        Player target = players[targetId];
        //        bool isWerewolf = target.Role == "인랑";
        //        bool isFox = target.Role == "여우";
        //        bool isDeepFake = target.Role == "광인"; // 광인은 인랑이 아닌 것으로 보임

        //        // 행동 등록
        //        nightActions[playerId] = new NightAction
        //        {
        //            PlayerId = playerId,
        //            ActionType = NightActionType.Check,
        //            TargetId = targetId
        //        };

        //        if (isFox)
        //        {
        //            // 여우는 점을 보면 사망
        //            AddChatMessage("System", $"{target.Name}을(를) 점쳤습니다. 이 플레이어는 여우입니다. 여우는 점에 의해 사망합니다.");
        //            pendingDeaths.Add(targetId);
        //        }
        //        else if (isWerewolf && !isDeepFake)
        //        {
        //            // 인랑 (광인 아님)
        //            AddChatMessage("System", $"{target.Name}을(를) 점쳤습니다. 이 플레이어는 인랑입니다.");
        //        }
        //        else
        //        {
        //            // 인랑이 아님 (또는 광인)
        //            AddChatMessage("System", $"{target.Name}을(를) 점쳤습니다. 이 플레이어는 인랑이 아닙니다.");
        //        }
        //    }
        //    else if (myRole == "사냥꾼")
        //    {
        //        // 사냥꾼 - 보호
        //        AddChatMessage("System", $"{players[targetId].Name}을(를) 보호했습니다.");

        //        // 행동 등록
        //        nightActions[playerId] = new NightAction
        //        {
        //            PlayerId = playerId,
        //            ActionType = NightActionType.Protect,
        //            TargetId = targetId
        //        };

        //        // 보호 대상 등록
        //        protectedPlayerId = targetId;
        //    }
        //    else if (myRole == "영매")
        //    {
        //        // 영매 - 처형된 사람의 직업 확인
        //        AddChatMessage("System", $"처형된 {players[targetId].Name}의 직업을 확인했습니다. 이 플레이어는 {players[targetId].Role}이었습니다.");

        //        // 행동 등록
        //        nightActions[playerId] = new NightAction
        //        {
        //            PlayerId = playerId,
        //            ActionType = NightActionType.Identify,
        //            TargetId = targetId
        //        };
        //    }
        //}

        private void PerformNightAction(int playerId, int targetId)
        {
            Player currentPlayer = players[playerId];
            Player target = players[targetId];

            if (!currentPlayer.IsAlive)
            {
                MessageBox.Show("죽은 플레이어는 행동할 수 없습니다.", "에러", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!target.IsAlive && currentPlayer.Role != "영매")
            {
                MessageBox.Show("살아있는 플레이어만 선택할 수 있습니다.", "에러", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            switch (currentPlayer.Role)
            {
                case "점쟁이":
                    {
                        bool isWerewolf = target.Role == "인랑";
                        bool isFox = target.Role == "여우";
                        bool isDeepFake = target.Role == "광인";

                        nightActions[playerId] = new NightAction
                        {
                            PlayerId = playerId,
                            ActionType = NightActionType.Check,
                            TargetId = targetId
                        };

                        if (isFox)
                        {
                            MessageBox.Show($"{target.Name}의 직업은 여우입니다. 점에 의해 사망합니다.", "점괘 결과", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            pendingDeaths.Add(targetId);
                        }
                        else if (isWerewolf && !isDeepFake)
                        {
                            MessageBox.Show($"{target.Name}의 직업은 인랑입니다.", "점괘 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show($"{target.Name}의 직업은 인랑이 아닙니다.", "점괘 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        break;
                    }

                case "영매":
                    {
                        if (!target.IsAlive)
                        {
                            MessageBox.Show($"{target.Name}의 직업은 {target.Role}입니다.", "영매 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            nightActions[playerId] = new NightAction
                            {
                                PlayerId = playerId,
                                ActionType = NightActionType.Identify,
                                TargetId = targetId
                            };
                        }
                        else
                        {
                            MessageBox.Show("영매는 죽은 사람만 볼 수 있습니다.", "영매 능력", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        break;
                    }

                case "사냥꾼":
                    {
                        MessageBox.Show($"{target.Name}을(를) 보호하기로 했습니다.", "사냥꾼 능력", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        nightActions[playerId] = new NightAction
                        {
                            PlayerId = playerId,
                            ActionType = NightActionType.Protect,
                            TargetId = targetId
                        };

                        protectedPlayerId = targetId;
                        break;
                    }

                case "인랑":
                    {
                        MessageBox.Show($"{target.Name}을(를) 습격하기로 했습니다.", "인랑 능력", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        nightActions[playerId] = new NightAction
                        {
                            PlayerId = playerId,
                            ActionType = NightActionType.Kill,
                            TargetId = targetId
                        };

                        if (!pendingDeaths.Contains(targetId))
                            pendingDeaths.Add(targetId);

                        foreach (var player in players)
                        {
                            if (player.IsAI && player.IsAlive && player.Role == "인랑")
                            {
                                AddWerewolfChatMessage(player.Name, $"{target.Name}을(를) 습격하는데 동의합니다.");
                            }
                        }
                        break;
                    }

                case "배덕자":
                case "광인":
                case "요호":
                case "네코마타":
                case "시민":
                default:
                    AddChatMessage("System", $"{currentPlayer.Name}은(는) 특별한 행동을 하지 않았습니다.");
                    break;
            }
        }


        /// <summary>
        /// 플레이어 ID 찾기
        /// </summary>
        private int GetPlayerIdFromSelection(string playerName)
        {
            var player = players.FirstOrDefault(p => p.Name == playerName);
            return player != null ? player.Id : -1;
        }

        #endregion

        #region 게임 로직

        /// <summary>
        /// 플레이어 사망 처리
        /// </summary>
        private void KillPlayer(int playerId)
        {
            if (playerId >= 0 && playerId < players.Count)
            {
                Player player = players[playerId];
                if (player.IsAlive)
                {
                    player.IsAlive = false;
                    deadPlayers.Add(playerId);

                    // 네코마타 능력 (처형당하면 다른 플레이어도 사망) - 낮 투표에서만 발동
                    if (player.Role == "네코마타" && currentPhase == GamePhase.DayResult)
                    {
                        TriggerNekomataAbility(playerId);
                    }
                }
            }
        }

        /// <summary>
        /// 네코마타 능력 발동
        /// </summary>
        private void TriggerNekomataAbility(int nekomataId)
        {
            // 네코마타가 사망하면 랜덤으로 다른 사람도 사망
            var alivePlayers = players.Where(p => p.IsAlive && p.Id != nekomataId).ToList();
            if (alivePlayers.Any())
            {
                Random random = new Random();
                int victimIndex = random.Next(alivePlayers.Count);
                Player victim = alivePlayers[victimIndex];

                KillPlayer(victim.Id);
                AddChatMessage("System", $"네코마타의 능력으로 {victim.Name}이(가) 함께 사망했습니다.");
            }
        }

        /// <summary>
        /// 게임 종료 조건 체크
        /// </summary>
        private void CheckGameEndCondition()
        {
            // 생존자 수 체크
            int aliveVillagers = players.Count(p => p.IsAlive && IsVillager(p.Id));
            int aliveWerewolves = players.Count(p => p.IsAlive && p.Role == "인랑");

            // 게임이 이미 종료 단계면 패스
            if (currentPhase == GamePhase.GameEnd) return;

            // 인랑 승리 조건: 인랑 수 >= 시민 수
            if (aliveWerewolves >= aliveVillagers && aliveWerewolves > 0)
            {
                // 게임 종료로 설정
                gamePhaseTimer.Stop();
                SetGamePhase(GamePhase.GameEnd);
                return;
            }

            // 시민 승리 조건: 모든 인랑 사망
            if (aliveWerewolves == 0)
            {
                gamePhaseTimer.Stop();
                SetGamePhase(GamePhase.GameEnd);
                return;
            }
        }

        /// <summary>
        /// 해당 플레이어가 인랑인지 확인
        /// </summary>
        private bool IsWerewolf(int playerId)
        {
            return players[playerId].Role == "인랑";
        }

        /// <summary>
        /// 해당 플레이어가 인랑 팀인지 확인
        /// </summary>
        private bool IsWerewolfTeam(int playerId)
        {
            string role = players[playerId].Role;
            return role == "인랑" || role == "광인";
        }

        /// <summary>
        /// 해당 플레이어가 시민 진영인지 확인
        /// </summary>
        private bool IsVillager(int playerId)
        {
            string role = players[playerId].Role;
            return role == "시민" || role == "점쟁이" || role == "영매" ||
                   role == "사냥꾼" || role == "네코마타";
            // 광인은 마을 진영이 아님!
        }

        /// <summary>
        /// 투표 다이얼로그 표시
        /// </summary>
        private void ShowVoteDialog()
        {
            // 투표 대상 선택 폼 생성
            Form voteForm = new Form
            {
                Text = "투표",
                Size = new Size(300, 200),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label label = new Label
            {
                Text = "처형할 플레이어를 선택하세요:",
                Location = new Point(20, 20),
                Size = new Size(260, 20)
            };

            ComboBox comboBox = new ComboBox
            {
                Location = new Point(20, 50),
                Size = new Size(250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            Button voteButton = new Button
            {
                Text = "투표",
                Location = new Point(100, 100),
                Size = new Size(80, 30)
            };

            // 투표 가능한 플레이어 목록 설정
            foreach (var player in players)
            {
                if (player.IsAlive && player.Id != 0) // 자신 제외 생존자
                {
                    comboBox.Items.Add(player.Name);
                }
            }

            // 기본 선택
            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }

            // 투표 이벤트
            voteButton.Click += (s, e) =>
            {
                if (comboBox.SelectedItem != null)
                {
                    string selectedName = comboBox.SelectedItem.ToString();
                    int selectedId = GetPlayerIdFromSelection(selectedName);

                    if (selectedId != -1)
                    {
                        // 투표 정보 저장
                        votes[0] = selectedId; // 플레이어의 투표

                        // 메시지 표시 (비밀 투표이므로 채팅에 누구를 투표했는지는 표시 안함)
                        AddChatMessage("System", "투표가 완료되었습니다.");

                        // AI 투표 시뮬레이션
                        SimulateAIVotes();

                        voteForm.Close();
                    }
                }
            };

            voteForm.Controls.Add(label);
            voteForm.Controls.Add(comboBox);
            voteForm.Controls.Add(voteButton);

            voteForm.ShowDialog();
        }

        /// <summary>
        /// AI 투표 시뮬레이션 - 좀더 논리적인 AI
        /// </summary>
        private void SimulateAIVotes()
        {
            // 각 AI 플레이어의 투표 설정
            foreach (var player in players)
            {
                if (player.IsAI && player.IsAlive)
                {
                    int targetId = -1;

                    // 인랑 AI는 전략적으로 투표
                    if (player.Role == "인랑" || player.Role == "광인")
                    {
                        // 인랑팀은 마을 핵심 인물(점쟁이, 영매)을 우선 타겟
                        List<Player> priorityTargets = players
                            .Where(p => p.IsAlive && p.Id != player.Id &&
                                    (p.Role == "점쟁이" || p.Role == "영매"))
                            .ToList();

                        if (priorityTargets.Count > 0)
                        {
                            targetId = priorityTargets[random.Next(priorityTargets.Count)].Id;
                        }
                        else
                        {
                            // 없으면 의심하는 플레이어에게 투표
                            if (aiSuspicions.ContainsKey(player.Id) && aiSuspicions[player.Id].Count > 0)
                            {
                                List<int> validSuspicions = aiSuspicions[player.Id]
                                    .Where(id => id != player.Id && players[id].IsAlive && players[id].Role != "인랑")
                                    .ToList();

                                if (validSuspicions.Count > 0)
                                {
                                    targetId = validSuspicions[random.Next(validSuspicions.Count)];
                                }
                            }
                        }
                    }
                    // 시민 AI는 의심 리스트에 따라 투표
                    else
                    {
                        if (aiSuspicions.ContainsKey(player.Id) && aiSuspicions[player.Id].Count > 0)
                        {
                            List<int> validSuspicions = aiSuspicions[player.Id]
                                .Where(id => id != player.Id && players[id].IsAlive)
                                .ToList();

                            if (validSuspicions.Count > 0)
                            {
                                targetId = validSuspicions[random.Next(validSuspicions.Count)];
                            }
                        }
                    }

                    // 타겟이 없으면 랜덤 선택
                    if (targetId == -1)
                    {
                        List<Player> possibleTargets = players
                            .Where(p => p.IsAlive && p.Id != player.Id)
                            .ToList();

                        if (possibleTargets.Count > 0)
                        {
                            targetId = possibleTargets[random.Next(possibleTargets.Count)].Id;
                        }
                    }

                    // 투표 등록
                    if (targetId != -1)
                    {
                        votes[player.Id] = targetId;
                    }
                }
            }
        }

        /// <summary>
        /// 인랑 전용 채팅 전송 (플레이어)
        /// </summary>
        private void SendWerewolfChat(string message)
        {
            // 인랑 플레이어만 볼 수 있는 채팅
            AddWerewolfChatMessage("나", message);

            // AI 인랑 응답 시뮬레이션
            SimulateWerewolfAIResponse(message);
        }

        /// <summary>
        /// 인랑 채팅 메시지 추가
        /// </summary>
        private void AddWerewolfChatMessage(string sender, string message)
        {
            if (chatBox == null || chatBox.IsDisposed)
                return;

            chatBox.SelectionColor = Color.Red;
            chatBox.AppendText("[인랑 채팅] ");
            chatBox.SelectionColor = Color.LightCoral;
            chatBox.AppendText($"{sender}: ");
            chatBox.SelectionColor = Color.LightGray;
            chatBox.AppendText($"{message}\n");
            chatBox.ScrollToCaret();
        }

        /// <summary>
        /// 인랑 AI 응답 시뮬레이션 - 더 지능적인 대화
        /// </summary>
        private void SimulateWerewolfAIResponse(string message)
        {
            // 키워드에 따른 응답
            List<string> responses = new List<string>();
            string lowerMessage = message.ToLower();

            if (lowerMessage.Contains("누구") || lowerMessage.Contains("목표") || lowerMessage.Contains("타겟"))
            {
                responses.Add("저는 점쟁이나 사냥꾼을 노리고 있어요.");
                responses.Add("이번에는 가장 의심스러운 사람을 제거해야 해요.");
                responses.Add("아직 결정 못했어요. 누가 좋을까요?");
            }
            else if (lowerMessage.Contains("점쟁이") || lowerMessage.Contains("점") || lowerMessage.Contains("fortune"))
            {
                responses.Add("점쟁이는 빨리 찾아서 제거해야 해요.");
                responses.Add("누가 점쟁인지 아직 모르겠어요.");
                responses.Add("점쟁이가 누군지 알아낼 방법이 있을까요?");
            }
            else if (lowerMessage.Contains("사냥꾼") || lowerMessage.Contains("보호") || lowerMessage.Contains("hunter"))
            {
                responses.Add("사냥꾼이 있으면 공격이 막힐 수 있으니 주의해야 해요.");
                responses.Add("사냥꾼은 매번 다른 사람을 보호할 거예요.");
                responses.Add("사냥꾼을 먼저 제거하는 게 좋을 것 같아요.");
            }
            else if (lowerMessage.Contains("전략") || lowerMessage.Contains("plan") || lowerMessage.Contains("작전"))
            {
                responses.Add("시민들이 서로 의심하게 만드는 게 좋을 것 같아요.");
                responses.Add("매일 다른 사람을 공격해서 패턴을 숨겨요.");
                responses.Add("마을 사람인 척 하면서 시민 역할에 맞는 발언을 하는 게 중요해요.");
            }
            else
            {
                responses.Add("맞아요, 그렇게 하는 게 좋겠어요.");
                responses.Add("조심해야 해요. 우리가 누군지 들키면 안 돼요.");
                responses.Add("다음 습격 타겟은 신중하게 선택해야 해요.");
                responses.Add("네, 좋은 생각이에요.");
            }

            // 생존한 인랑 AI만 응답
            foreach (var player in players)
            {
                if (player.IsAI && player.IsAlive && player.Role == "인랑")
                {
                    // 랜덤 응답 선택
                    string response = responses[random.Next(responses.Count)];
                    AddWerewolfChatMessage(player.Name, response);

                    // 약간의 딜레이
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(random.Next(500, 1000));
                }
            }
        }

        /// <summary>
        /// 플레이어 상태 UI 업데이트
        /// </summary>
        private void UpdatePlayerUI()
        {
            // 각 플레이어 상태 업데이트
            for (int i = 0; i < players.Count; i++)
            {
                if (i < playerBoxes.Count)
                {
                    Player player = players[i];
                    PictureBox box = playerBoxes[i];

                    // 기본 이미지
                    box.Image = playerImage;

                    // 사망 상태 표시
                    if (!player.IsAlive)
                    {
                        // 사망 이미지 오버레이
                        if (playerDeadOverlay != null)
                        {
                            // 합성 이미지 생성
                            Bitmap composite = new Bitmap(box.Width, box.Height);
                            using (Graphics g = Graphics.FromImage(composite))
                            {
                                g.DrawImage(playerImage, 0, 0, box.Width, box.Height);
                                g.DrawImage(playerDeadOverlay, 0, 0, box.Width, box.Height);
                            }
                            box.Image = composite;
                        }

                        // 라벨 색상 변경
                        playerNameLabels[i].ForeColor = Color.Gray;
                    }
                    else
                    {
                        // 살아있는 플레이어
                        playerNameLabels[i].ForeColor = Color.BurlyWood;
                    }
                }
            }
        }

        #endregion

        #region 채팅 및 메시지

        /// <summary>
        /// AI 인사 메시지 시뮬레이션
        /// </summary>
        private void SimulateAIGreetings()
        {
            string[] greetings =
            {
                "안녕하세요!",
                "모두 반갑습니다.",
                "좋은 게임 되세요~",
                "안녕하세요, 잘 부탁드립니다!",
                "인랑은 누구일까요?",
                "다들 조심하세요, 인랑이 숨어있어요.",
                "처음 뵙겠습니다, 즐거운 게임 해요!"
            };

            Random random = new Random();

            // 프로세싱 바로 교체
            // AI 플레이어들의 인사 메시지 시뮬레이션
            SimulateAIGreetingsWithTimer(greetings, random);
        }

        /// <summary>
        /// 타이머를 사용한 AI 인사 시뮬레이션
        /// </summary>
        private void SimulateAIGreetingsWithTimer(string[] greetings, Random random)
        {
            int index = 1;
            Timer greetingTimer = new Timer();
            greetingTimer.Interval = 800; // 0.8초 간격

            greetingTimer.Tick += (s, e) =>
            {
                if (index >= players.Count)
                {
                    greetingTimer.Stop();
                    greetingTimer.Dispose();
                    return;
                }

                if (players[index].IsAI)
                {
                    string greeting = greetings[random.Next(greetings.Length)];
                    AddChatMessage(players[index].Name, greeting);
                }

                index++;
            };

            greetingTimer.Start();
        }

        /// <summary> (옛날버전0525이전)
        /// AI 응답 시뮬레이션 - 더 논리적인, 역할에 따른 대화
        /// </summary>
        //private void SimulateAIResponse(string playerMessage)
        //{
        //    // 현재 날짜와 게임 상황에 따른 응답 생성
        //    Dictionary<string, List<string>> roleResponses = new Dictionary<string, List<string>>
        //    {
        //        // 시민
        //        ["시민"] = new List<string>
        //        {
        //            "마을을 지키기 위해 인랑을 찾아야 해요.",
        //            "누가 의심스러운지 말씀해 주실 수 있나요?",
        //            "저는 시민입니다. 평화롭게 살고 싶어요.",
        //            "어젯밤에 이상한 소리가 들린 것 같았어요."
        //        },

        //        // 인랑
        //        ["인랑"] = new List<string>
        //        {
        //            "저도 시민입니다. 함께 인랑을 찾아요.",
        //            "어제 누가 이상하게 행동했던 것 같아요.",
        //            "점쟁이가 누구인지 아시나요?",
        //            "너무 조용한 사람이 의심스러워요."
        //        },

        //        // 점쟁이
        //        ["점쟁이"] = new List<string>
        //        {
        //            "제 생각에는 조금 더 관찰해봐야 할 것 같아요.",
        //            "의심스러운 사람이 몇 명 있네요.",
        //            "증거가 필요해요. 성급한 판단은 위험합니다.",
        //            "저도 인랑을 찾는데 집중하고 있어요."
        //        },

        //        // 사냥꾼
        //        ["사냥꾼"] = new List<string>
        //        {
        //            "마을을 지키는 게 최우선입니다.",
        //            "모두 진정하고 차분하게 생각해봅시다.",
        //            "지나치게 공격적인 사람은 의심해볼 필요가 있어요.",
        //            "함께 머리를 맞대면 인랑을 찾을 수 있을 거예요."
        //        },

        //        // 영매
        //        ["영매"] = new List<string>
        //        {
        //            "사실을 밝혀내야 합니다.",
        //            "의심스러운 행동을 주의 깊게 살펴보세요.",
        //            "진실을 향해 한 걸음씩 나아가고 있어요.",
        //            "증거에 기반한 판단이 중요합니다."
        //        },

        //        // 광인
        //        ["광인"] = new List<string>
        //        {
        //            "그 사람이 인랑 같아요! 의심해보세요!",
        //            "분위기가 이상하네요. 누군가 거짓말을 하고 있어요.",
        //            "저는 확실히 시민이에요. 다른 사람들을 의심해보세요.",
        //            "너무 조용한 사람이 위험할 수 있어요."
        //        }
        //    };

        //    // 키워드에 기반한 특별 응답
        //    string lowerMessage = playerMessage.ToLower();
        //    Dictionary<string, List<string>> keywordResponses = new Dictionary<string, List<string>>();

        //    // 키워드 응답 설정
        //    if (lowerMessage.Contains("인랑") || lowerMessage.Contains("늑대"))
        //    {
        //        keywordResponses["인랑언급"] = new List<string>
        //        {
        //            "인랑은 반드시 찾아내야 합니다.",
        //            "인랑의 행동 패턴을 분석해봐야 해요.",
        //            "인랑은 보통 눈에 띄지 않게 행동하려고 합니다.",
        //            "인랑은 서로를 알아본다고 하죠."
        //        };
        //    }

        //    if (lowerMessage.Contains("투표") || lowerMessage.Contains("처형"))
        //    {
        //        keywordResponses["투표언급"] = new List<string>
        //        {
        //            "투표는 신중하게 해야 해요.",
        //            "의심스러운 사람에게 투표해야 합니다.",
        //            "투표로 인랑을 제거할 수 있어요.",
        //            "증거 없이 투표하면 위험할 수 있어요."
        //        };
        //    }

        //    if (lowerMessage.Contains("의심") || lowerMessage.Contains("수상"))
        //    {
        //        keywordResponses["의심언급"] = new List<string>
        //        {
        //            "맞아요. 의심스러운 행동을 주의 깊게 봐야 해요.",
        //            "의심만으로 판단하기는 어려워요.",
        //            "누구를 의심하시나요?",
        //            "저도 몇 명 의심하고 있어요."
        //        };
        //    }

        //    // 1~3명의 AI가 응답
        //    int dayFactor = Math.Min(currentDay, 3); // 날이 갈수록 더 많은 AI가 응답
        //    int respondingAIs = random.Next(1, Math.Min(2 + dayFactor, players.Count(p => p.IsAI && p.IsAlive)));

        //    // 응답할 AI 선택
        //    var livingAIs = players.Where(p => p.IsAI && p.IsAlive).ToList();
        //    if (livingAIs.Count > 0)
        //    {
        //        List<int> selectedAIs = new List<int>();

        //        while (selectedAIs.Count < respondingAIs && selectedAIs.Count < livingAIs.Count)
        //        {
        //            int aiIndex = random.Next(livingAIs.Count);

        //            if (!selectedAIs.Contains(aiIndex))
        //            {
        //                selectedAIs.Add(aiIndex);

        //                Player ai = livingAIs[aiIndex];
        //                string response;

        //                // 키워드 응답 우선
        //                if (keywordResponses.Count > 0)
        //                {
        //                    var keywordType = keywordResponses.Keys.ElementAt(random.Next(keywordResponses.Count));
        //                    response = keywordResponses[keywordType][random.Next(keywordResponses[keywordType].Count)];
        //                }
        //                // 역할 기반 응답
        //                else if (roleResponses.ContainsKey(ai.Role))
        //                {
        //                    response = roleResponses[ai.Role][random.Next(roleResponses[ai.Role].Count)];
        //                }
        //                // 기본 응답
        //                else
        //                {
        //                    response = roleResponses["시민"][random.Next(roleResponses["시민"].Count)];
        //                }

        //                int delay = random.Next(1000, 3000);
        //                Timer responseTimer = new Timer();
        //                responseTimer.Interval = delay;
        //                responseTimer.Tick += (s, e) =>
        //                {
        //                    AddChatMessage(ai.Name, response);
        //                    responseTimer.Stop();
        //                    responseTimer.Dispose();
        //                };
        //                responseTimer.Start();
        //            }
        //        }
        //    }
        //}

        /// <summary>
        /// 플레이어의 메시지에 반응하는 AI의 대화 로직 (직업별 + 행동 패턴 + 키워드 기반 + 신뢰도 반영)
        /// </summary>
        private void SimulateAIResponse(string playerMessage)
        {
            string lowerMessage = playerMessage.ToLower();

            // 정규식으로 숫자 기반 지목 대상 추출 (예: "2번 수상해요")
            var numberMatches = System.Text.RegularExpressions.Regex.Matches(playerMessage, @"\b\d{1,2}\b");
            List<int> mentionedPlayerIds = new List<int>();

            foreach (System.Text.RegularExpressions.Match match in numberMatches)
            {
                if (int.TryParse(match.Value, out int mentionedId))
                {
                    if (mentionedId >= 0 && mentionedId < players.Count && players[mentionedId].IsAlive)
                    {
                        mentionedPlayerIds.Add(mentionedId);
                    }
                }
            }

            foreach (var ai in players.Where(p => p.IsAI && p.IsAlive))
            {
                int aiId = ai.Id;
                string aiRole = ai.Role;

                // 1. 플레이어가 언급한 대상의 신뢰도 하락 (이름 기반 + 숫자 기반 모두)
                foreach (var player in players)
                {
                    if (player.Id != aiId && player.IsAlive &&
                        (playerMessage.Contains(player.Name) || mentionedPlayerIds.Contains(player.Id)))
                    {
                        if (!aiTrustScores.ContainsKey(aiId)) aiTrustScores[aiId] = new Dictionary<int, int>();
                        if (!aiTrustScores[aiId].ContainsKey(player.Id)) aiTrustScores[aiId][player.Id] = 50;

                        aiTrustScores[aiId][player.Id] -= 10;
                        if (aiTrustScores[aiId][player.Id] < 0)
                            aiTrustScores[aiId][player.Id] = 0;
                    }
                }

                // 2. 현재 AI가 가장 신뢰하지 않는 대상 선정 (신뢰도 최저)
                int targetId = -1;
                int minTrust = int.MaxValue;

                if (aiTrustScores.ContainsKey(aiId))
                {
                    foreach (var entry in aiTrustScores[aiId])
                    {
                        int pid = entry.Key;
                        int trust = entry.Value;

                        if (pid != aiId && players[pid].IsAlive && trust < minTrust)
                        {
                            minTrust = trust;
                            targetId = pid;
                        }
                    }
                }

                // 3. 대사 결정
                string response = "";

                if (lowerMessage.Contains("의심") || lowerMessage.Contains("누구") || lowerMessage.Contains("수상"))
                {
                    if (targetId != -1)
                    {
                        response = GenerateSuspicionLine(aiRole, players[targetId].Name);
                    }
                    else
                    {
                        response = GenerateNeutralLine(aiRole);
                    }
                }
                else if (lowerMessage.Contains("같이") || lowerMessage.Contains("협력") || lowerMessage.Contains("믿어"))
                {
                    response = GenerateCooperativeLine(aiRole);
                }
                else
                {
                    response = GenerateNeutralLine(aiRole);
                }

                AddChatMessage(ai.Name, response);
                Application.DoEvents();
                System.Threading.Thread.Sleep(random.Next(300, 700));
            }
        }

        private string GenerateSuspicionLine(string role, string targetName)
        {
            switch (role)
            {
                case "점쟁이":
                    {
                        string[] options = {
                $"{targetName}은(는) 뭔가 이상한 기운이 느껴져요.",
                $"{targetName}이(가) 어젯밤 꿈에 나왔어요. 찜찜하네요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "영매":
                    {
                        string[] options = {
                $"{targetName}은(는) 뭔가 이상해요... 느낌이 좀 그래요.",
                $"{targetName}은(는) 굉장히 수상하네요... 위험한 사람인 것 같아요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "네코마타":
                    {
                        string[] options = {
                $"{targetName}? 의심스럽긴 한데, 함부로 말하면 안 되겠죠.",
                $"만약 {targetName}이(가) 인랑이면... 제가 다음일지도 모르겠네요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "인랑":
                    {
                        string[] options = {
                $"{targetName}은(는) 너무 조용해요. 수상해요.",
                $"{targetName}은(는) 행동이 이상해요. 저 사람부터 처리해야 해요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "광인":
                    {
                        string[] options = {
                $"{targetName}? 전혀 아닌 것 같아요. 괜히 의심하지 맙시다.",
                $"{targetName}이(가) 의심을 받다니 말도 안 돼요. 그런 사람 아니에요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "요호":
                    {
                        string[] options = {
                $"{targetName}은(는) 뭔가 많이 아는 것 같지 않아요?",
                $"{targetName}은(는) 너무 자신 있는 척해요. 수상해요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "배덕자":
                    {
                        string[] options = {
                $"{targetName}은(는) 뭔가 알고 있는 것 같네요...",
                $"{targetName}은(는) 중요한 비밀을 숨기고 있을지도 몰라요."
            };
                        return options[random.Next(options.Length)];
                    }

                default:
                    {
                        string[] options = {
                $"{targetName}이(가) 수상하다고 생각해요.",
                $"{targetName}은(는) 뭔가 이상해요. 주의해서 봐야 해요."
            };
                        return options[random.Next(options.Length)];
                    }
            }
        }


        private string GenerateCooperativeLine(string role)
        {
            switch (role)
            {
                case "점쟁이":
                    {
                        string[] options = {
                "서로를 믿고 천천히 진실에 다가가요.",
                "신중하게 협력하면 진실이 보일 거예요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "영매":
                    {
                        string[] options = {
                "조심스럽게 협력하는 게 중요해요.",
                "죽은 사람들의 뜻을 잇기 위해 함께해요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "사냥꾼":
                    {
                        string[] options = {
                "제가 누군가를 지킬게요. 같이 힘을 합쳐요.",
                "밤에도 서로 믿고 지켜줘야 해요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "네코마타":
                    {
                        string[] options = {
                "저를 믿어도 좋아요. 도움이 될 수 있어요.",
                "의심받기 싫지만, 저도 마을을 위해 협력할게요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "인랑":
                    {
                        string[] options = {
                "우린 같은 편이잖아요. 협력하자고요.",  // 시민인 척
                "저도 마을을 지키고 싶어요. 의심은 접어둬요."  // 거짓 협력
            };
                        return options[random.Next(options.Length)];
                    }

                case "광인":
                    {
                        string[] options = {
                "지금은 서로 싸울 때가 아니에요.",
                "협력이 중요하죠. 다들 진정합시다."
            };
                        return options[random.Next(options.Length)];
                    }

                case "요호":
                    {
                        string[] options = {
                "다 같이 힘을 합치면 해결될 거예요.",
                "너무 믿지는 말고, 그래도 협력은 필요해요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "배덕자":
                    {
                        string[] options = {
                "각자 이득이 있겠지만, 일단은 함께하죠.",
                "협력은 필요하지만 누구나 속셈은 있죠."
            };
                        return options[random.Next(options.Length)];
                    }

                default:
                    {
                        string[] options = {
                "좋아요. 함께 힘을 모아요.",
                "믿음이 중요하죠. 같이 가요."
            };
                        return options[random.Next(options.Length)];
                    }
            }
        }


        private string GenerateNeutralLine(string role)
        {
            switch (role)
            {
                case "시민":
                    {
                        string[] options = {
                "조심스럽게 움직여야 해요.",
                "섣부른 판단은 우리를 위험하게 만들 수 있어요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "점쟁이":
                    {
                        string[] options = {
                "정보는 아직 부족하지만 천천히 모아갈게요.",
                "지금 말하긴 이르지만, 조만간 도움이 될 거예요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "영매":
                    {
                        string[] options = {
                "죽은 사람들의 정보가 실마리가 될 거예요.",
                "조용히 영을 느껴볼게요. 무언가 보일지도 몰라요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "사냥꾼":
                    {
                        string[] options = {
                "밤에는 모두가 안전해야 해요.",
                "누구를 지켜야 할지 고민되네요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "네코마타":
                    {
                        string[] options = {
                "전 중요하니까 살려두는 게 좋을 거예요.",
                "제 역할은 분명히 있어요. 눈여겨보세요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "인랑":
                    {
                        string[] options = {
                "불필요한 의심은 삼가죠.",
                "모두가 너무 예민한 것 같아요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "광인":
                    {
                        string[] options = {
                "사람은 겉만 봐선 몰라요.",
                "지금 누구도 확신할 수 없죠."
            };
                        return options[random.Next(options.Length)];
                    }

                case "요호":
                    {
                        string[] options = {
                "섣부른 판단은 위험하죠.",
                "가짜 정보에 휘둘리면 안 돼요."
            };
                        return options[random.Next(options.Length)];
                    }

                case "배덕자":
                    {
                        string[] options = {
                "모두가 자기 입장이 있겠죠.",
                "눈에 보이는 게 다는 아닐 수도 있어요."
            };
                        return options[random.Next(options.Length)];
                    }

                default:
                    {
                        string[] options = {
                "더 많은 정보가 필요해요.",
                "조금만 더 기다려 봐요."
            };
                        return options[random.Next(options.Length)];
                    }
            }
        }



        /// <summary>
        /// 채팅 메시지 추가
        /// </summary>
        private void AddChatMessage(string sender, string message)
        {
            if (chatBox == null || chatBox.IsDisposed)
                return;

            chatBox.SelectionColor = sender == "System" ? Color.Yellow : Color.White;
            chatBox.AppendText($"{sender}: ");
            chatBox.SelectionColor = Color.LightGray;
            chatBox.AppendText($"{message}\n");
            chatBox.ScrollToCaret();
        }

        /// <summary>
        /// 시스템 메시지 추가
        /// </summary>
        private void AddSystemMessage(string message)
        {
            systemMsgLabel.Text = message;
        }

        #endregion

        #region 화면 그리기

        /// <summary>
        /// 화면 그리기
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (gameStarted)
            {
                // 게임 시작 후에는 그리기 처리 안함 (게임 패널이 표시됨)
                return;
            }

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
            if (!string.IsNullOrEmpty(playerRole))
            {
                g.DrawString(playerRole, roleFont, Brushes.White, new RectangleF(0, 350, this.ClientSize.Width, 60), centerFormat);

                // 직업 설명
                string roleDescription = GetRoleDescription(playerRole);
                g.DrawString(roleDescription, descFont, Brushes.BurlyWood,
                    new RectangleF(100, 410, this.ClientSize.Width - 200, 100), centerFormat);
            }

            // 타이머
            g.DrawString($"{remainingTime}초", timerFont, Brushes.White, new RectangleF(0, 500, this.ClientSize.Width, 40), centerFormat);

            // 클릭 안내
            g.DrawString("화면을 클릭하여 확인 완료", descFont, Brushes.Gray,
                new RectangleF(0, 540, this.ClientSize.Width, 30), centerFormat);
        }

        /// <summary>
        /// 직업별 설명 반환
        /// </summary>
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

        #endregion
    }

    /// <summary>
    /// 플레이어 정보 클래스
    /// </summary>
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public bool IsAI { get; set; }
        public bool IsAlive { get; set; }
    }

    /// <summary>
    /// 게임 결과 정보 클래스
    /// </summary>
    public class GameResultInfo
    {
        public bool WerewolfTeamWin { get; set; }
        public bool VillageTeamWin { get; set; }
        public bool FoxTeamWin { get; set; }
        public int AliveWerewolves { get; set; }
        public int AliveVillagers { get; set; }
        public int AliveFoxes { get; set; }
        public int AliveImmorals { get; set; }
    }

    /// <summary>
    /// 밤 행동 타입
    /// </summary>
    public enum NightActionType
    {
        None,
        Kill,       // 인랑의 습격
        Check,      // 점쟁이의 점
        Protect,    // 사냥꾼의 보호
        Identify    // 영매의 확인
    }

    /// <summary>
    /// 밤 행동 정보 클래스
    /// </summary>
    public class NightAction
    {
        public int PlayerId { get; set; }         // 행동하는 플레이어 ID
        public NightActionType ActionType { get; set; } // 행동 타입
        public int TargetId { get; set; }         // 대상 플레이어 ID
    }
}
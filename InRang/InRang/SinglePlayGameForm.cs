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
    public enum GamePhase
    {
        Introduction,    // 직업 소개
        Day,            // 낮 - 토론 및 투표
        DayResult,      // 낮 결과 - 투표 결과 발표
        Night,          // 밤 - 능력 사용
        NightResult,    // 밤 결과 - 사망자 발표
        GameEnd         // 게임 종료
    }

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
        private int dayTime = 60;        // 낮 진행 시간 (초)
        private int nightTime = 50;      // 밤 진행 시간 (초)

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
        private int targetPlayerId = -1; // 능력 대상 플레이어
        private List<int> deadPlayers = new List<int>();  // 사망한 플레이어 ID
        private Dictionary<int, int> votes = new Dictionary<int, int>();  // 투표 정보 (플레이어ID, 투표대상ID)

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
                Text = "아직이 밤입니다.\n투표를 진행합니다.",
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
                    targetPlayerId = GetPlayerIdFromSelection(playerSelectionBox.SelectedItem.ToString());
                    PerformNightAction();
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
        /// 플레이어 상태 UI 생성
        /// </summary>
        private void CreatePlayerStatusUI()
        {
            int playerImageSize = 70;
            int spacing = 20;
            int startX = 350;
            int startY = 150;
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
        /// 표준 모드 직업 배정
        /// </summary>
        private void AssignStandardRoles(Random random)
        {
            List<string> availableRoles = new List<string>();

            // 플레이어 수에 따른 인랑 수 결정
            int wolfCount;
            if (playerCount <= 5) wolfCount = 1;
            else if (playerCount <= 8) wolfCount = 2;  // 8명일 때 인랑 2명 (균형을 위해)
            else if (playerCount <= 11) wolfCount = 3;
            else wolfCount = 4;

            // 특수 직업 배정
            int fortuneTellerCount = 1;  // 점쟁이 1명
            int mediumCount = playerCount >= 7 ? 1 : 0;  // 영매 (7명 이상일 때)
            int hunterCount = playerCount >= 6 ? 1 : 0;  // 사냥꾼 (6명 이상일 때)
            int madmanCount = playerCount >= 8 ? 1 : 0;  // 광인 (8명 이상일 때)
            int foxCount = playerCount >= 9 ? 1 : 0;  // 여우 (9명 이상일 때)
            int immoralCount = foxCount > 0 && playerCount >= 10 ? 1 : 0;  // 배덕자 (10명 이상이고 여우가 있을 때)
            int nekomataCount = playerCount >= 11 ? 1 : 0;  // 네코마타 (11명 이상일 때)

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
                    // 밤 결과 계산
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
                    break;

                case GamePhase.DayResult:
                    remainingTime = 5; // 투표 결과 표시 시간
                    timeLabel.ForeColor = Color.White;
                    AddSystemMessage("투표 결과를 발표합니다.");

                    // 투표 결과 표시
                    ShowVoteResult();

                    // 투표 버튼 비활성화
                    voteButton.Visible = false;
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
                    break;

                case GamePhase.NightResult:
                    remainingTime = 5; // 밤 결과 표시 시간
                    timeLabel.ForeColor = Color.White;
                    AddSystemMessage("밤 사이 일어난 일을 발표합니다.");

                    // 밤 전용 UI 숨기기
                    selectionLabel.Visible = false;
                    playerSelectionBox.Visible = false;
                    actionButton.Visible = false;
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

            foreach (var pair in voteCount)
            {
                if (pair.Value > maxVotes)
                {
                    maxVotes = pair.Value;
                    votedPlayerId = pair.Key;
                }
            }

            // 동률일 경우 처리 (여기서는 간단히 첫 번째 최다 득표자 선택)
            if (votedPlayerId != -1)
            {
                // 사망 처리
                KillPlayer(votedPlayerId);
            }
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
                message += $" (직업: {votedPlayer.Role})";

                AddSystemMessage(message);
                AddChatMessage("System", message);

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
        }

        /// <summary>
        /// 밤 결과 계산
        /// </summary>
        private void CalculateNightResult()
        {
            // 여기에 밤 동안의 행동 결과를 계산
            // 인랑의 습격, 점쟁이의 점, 사냥꾼의 보호 등

            // 예시: 인랑의 습격 결과 처리
            if (targetPlayerId != -1 && players.Any(p => p.Id == targetPlayerId))
            {
                // 사냥꾼 보호 등의 추가 로직이 필요하면 여기에 추가

                // 플레이어 사망 처리
                KillPlayer(targetPlayerId);
            }
        }

        /// <summary>
        /// 밤 결과 표시
        /// </summary>
        private void ShowNightResult()
        {
            // 밤 사이 사망자 알림
            if (targetPlayerId != -1 && deadPlayers.Contains(targetPlayerId))
            {
                Player killedPlayer = players.First(p => p.Id == targetPlayerId);
                string message = $"{killedPlayer.Name}이(가) 밤 사이 사망했습니다.";

                AddSystemMessage(message);
                AddChatMessage("System", message);

                // 사망자 표시 업데이트
                UpdatePlayerUI();
            }
            else
            {
                AddSystemMessage("밤 사이 아무 일도 일어나지 않았습니다.");
                AddChatMessage("System", "밤 사이 아무 일도 일어나지 않았습니다.");
            }

            // 타겟 초기화
            targetPlayerId = -1;
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
            int aliveWerewolves = players.Count(p => p.IsAlive && IsWerewolf(p.Id));
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

            // 모든 플레이어의 직업 공개
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

            if (myRole == "인랑")
            {
                // 인랑 UI - 습격 대상 선택
                selectionLabel.Text = "습격할 대상 선택";
                actionButton.Text = "kill";

                // 습격 가능한 대상 추가 (인랑이 아닌 생존자)
                foreach (var player in players)
                {
                    if (player.IsAlive && player.Role != "인랑" && player.Id != 0)
                    {
                        playerSelectionBox.Items.Add(player.Name);
                    }
                }
            }
            else if (myRole == "점쟁이")
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
            }
            else if (myRole == "사냥꾼")
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
            }
            else
            {
                // 능력이 없는 직업이거나 자신의 능력을 사용할 수 없는 상황
                selectionLabel.Visible = false;
                playerSelectionBox.Visible = false;
                actionButton.Visible = false;
                return;
            }

            // UI 표시
            selectionLabel.Visible = true;
            playerSelectionBox.Visible = true;
            actionButton.Visible = true;

            // 기본 선택
            if (playerSelectionBox.Items.Count > 0)
            {
                playerSelectionBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 밤 능력 수행
        /// </summary>
        private void PerformNightAction()
        {
            if (targetPlayerId == -1) return;

            string myRole = playerRole;

            if (myRole == "인랑")
            {
                // 인랑 - 습격
                AddChatMessage("System", $"{players[targetPlayerId].Name}을(를) 습격했습니다.");

                // AI 인랑들도 같은 플레이어 습격
                foreach (var player in players)
                {
                    if (player.IsAI && player.IsAlive && player.Role == "인랑")
                    {
                        // AI 인랑들은 같은 대상 습격에 동의 (간단한 구현)
                        AddChatMessage(player.Name, $"{players[targetPlayerId].Name}을(를) 습격하는데 동의합니다.");
                    }
                }
            }
            else if (myRole == "점쟁이")
            {
                // 점쟁이 - 점 보기
                Player target = players[targetPlayerId];
                bool isWerewolf = target.Role == "인랑";
                bool isFox = target.Role == "여우";

                if (isFox)
                {
                    // 여우는 점을 보면 사망
                    AddChatMessage("System", $"{target.Name}을(를) 점쳤습니다. 이 플레이어는 여우입니다. 여우는 점에 의해 사망합니다.");
                    KillPlayer(targetPlayerId);
                }
                else
                {
                    // 일반적인 점괘 결과
                    AddChatMessage("System", $"{target.Name}을(를) 점쳤습니다. 이 플레이어는 {(isWerewolf ? "인랑" : "인랑이 아닙니다")}.");
                }
            }
            else if (myRole == "사냥꾼")
            {
                // 사냥꾼 - 보호
                AddChatMessage("System", $"{players[targetPlayerId].Name}을(를) 보호했습니다.");

                // 실제 게임에서는 이 플레이어를 인랑 습격으로부터 보호하는 로직 추가
            }

            // 능력 사용 후 UI 숨기기
            selectionLabel.Visible = false;
            playerSelectionBox.Visible = false;
            actionButton.Visible = false;
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

                    // 네코마타 능력 (처형당하면 다른 플레이어도 사망)
                    if (player.Role == "네코마타" && currentPhase == GamePhase.DayResult)
                    {
                        TriggerNekomataAbility(playerId);
                    }

                    // 승리 조건 체크
                    CheckGameEndCondition();
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
            int aliveWerewolves = players.Count(p => p.IsAlive && IsWerewolf(p.Id));

            // 인랑 승리 조건: 인랑 수 >= 시민 수
            if (aliveWerewolves >= aliveVillagers && aliveWerewolves > 0)
            {
                // 다음 페이즈가 끝나면 게임 종료로 설정
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
            return players[playerId].Role == "인랑" || players[playerId].Role == "광인";
        }

        /// <summary>
        /// 해당 플레이어가 시민 진영인지 확인
        /// </summary>
        private bool IsVillager(int playerId)
        {
            string role = players[playerId].Role;
            return role == "시민" || role == "점쟁이" || role == "영매" ||
                   role == "사냥꾼" || role == "네코마타";
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

                        // AI 투표 시뮬레이션
                        SimulateAIVotes();

                        AddChatMessage("System", $"당신은 {selectedName}에게 투표했습니다.");
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
        /// AI 투표 시뮬레이션
        /// </summary>
        private void SimulateAIVotes()
        {
            Random random = new Random();

            // 각 AI 플레이어의 투표 설정
            foreach (var player in players)
            {
                if (player.IsAI && player.IsAlive)
                {
                    List<Player> possibleTargets = players.Where(p => p.IsAlive && p.Id != player.Id).ToList();

                    if (possibleTargets.Any())
                    {
                        // 간단한 AI 로직 - 랜덤 투표
                        // 더 복잡한 로직은 여기에 추가 가능
                        int targetIndex = random.Next(possibleTargets.Count);
                        int targetId = possibleTargets[targetIndex].Id;

                        votes[player.Id] = targetId;
                        AddChatMessage(player.Name, $"{players[targetId].Name}에게 투표합니다.");
                    }
                }
            }
        }

        /// <summary>
        /// 인랑 전용 채팅 전송
        /// </summary>
        private void SendWerewolfChat(string message)
        {
            // 인랑 플레이어만 볼 수 있는 채팅
            chatBox.SelectionColor = Color.Red;
            chatBox.AppendText("[인랑 채팅] ");
            chatBox.SelectionColor = Color.LightGray;
            chatBox.AppendText($"나: {message}\n");
            chatBox.ScrollToCaret();

            // AI 인랑 응답 시뮬레이션
            SimulateWerewolfAIResponse(message);
        }

        /// <summary>
        /// 인랑 AI 응답 시뮬레이션
        /// </summary>
        private void SimulateWerewolfAIResponse(string message)
        {
            string[] responses = {
                "좋은 생각이에요.",
                "오늘 밤 누구를 습격할까요?",
                "조심해야 합니다. 점쟁이가 있을 수 있어요.",
                "네, 저도 동의합니다.",
                "그 플레이어를 습격하는 것이 좋겠습니다."
            };

            Random random = new Random();

            // 인랑 AI의 응답
            foreach (var player in players)
            {
                if (player.IsAI && player.IsAlive && player.Role == "인랑")
                {
                    string response = responses[random.Next(responses.Length)];

                    chatBox.SelectionColor = Color.Red;
                    chatBox.AppendText("[인랑 채팅] ");
                    chatBox.SelectionColor = Color.LightGray;
                    chatBox.AppendText($"{player.Name}: {response}\n");
                    chatBox.ScrollToCaret();

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

                    // 투명도 설정 (사망 시 반투명)
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
                "인랑은 누구일까요?"
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

        /// <summary>
        /// AI 응답 시뮬레이션
        /// </summary>
        private void SimulateAIResponse(string playerMessage)
        {
            string[] responses =
            {
                "그럴 수도 있겠네요.",
                "저는 시민입니다.",
                "누가 의심스러운가요?",
                "인랑은 누구인지 찾아야 해요.",
                "처음 플레이해봐서 잘 모르겠어요.",
                "저도 그렇게 생각합니다."
            };

            Random random = new Random();

            // 1~3명의 AI가 응답
            int respondingAIs = random.Next(1, Math.Min(4, players.Count(p => p.IsAI && p.IsAlive)));

            // 응답할 AI 선택
            var livingAIs = players.Where(p => p.IsAI && p.IsAlive).ToList();
            if (livingAIs.Count > 0)
            {
                List<int> selectedAIs = new List<int>();

                while (selectedAIs.Count < respondingAIs && selectedAIs.Count < livingAIs.Count)
                {
                    int aiIndex = random.Next(livingAIs.Count);

                    if (!selectedAIs.Contains(aiIndex))
                    {
                        selectedAIs.Add(aiIndex);

                        Player ai = livingAIs[aiIndex];
                        string response = responses[random.Next(responses.Length)];

                        int delay = random.Next(1000, 3000);
                        Timer responseTimer = new Timer();
                        responseTimer.Interval = delay;
                        responseTimer.Tick += (s, e) =>
                        {
                            AddChatMessage(ai.Name, response);
                            responseTimer.Stop();
                            responseTimer.Dispose();
                        };
                        responseTimer.Start();
                    }
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
}
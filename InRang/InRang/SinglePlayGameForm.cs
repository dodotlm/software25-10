using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace InRang
{
    /// <summary>
    /// 싱글 플레이 게임 폼 - 직업 확인 후 게임 진행
    /// </summary>
    public partial class SinglePlayGameForm : Form
    {
        #region 필드

        // 직업 이미지 관련
        private System.Drawing.Image roleImage;
        private Dictionary<string, System.Drawing.Image> roleImages = new Dictionary<string, System.Drawing.Image>();

        // 플레이어 정보
        private int playerCount;
        private int aiCount;
        private bool yaminabeMode;
        private bool quantumMode;

        // 직업 정보
        private List<string> assignedRoles = new List<string>();
        private string playerRole = ""; // 현재 플레이어의 직업만 저장

        // 타이머 관련
        private Timer roleViewTimer;
        private int remainingTime = 8; // 8초 카운트다운

        // 글꼴 설정
        private Font titleFont;
        private Font roleFont;
        private Font timerFont;
        private Font descFont;

        // 게임 상태
        private bool gameStarted = false;

        // 게임 플레이 관련 컴포넌트
        private Panel gamePanel;
        private Panel chatPanel;
        private RichTextBox chatBox;
        private TextBox messageBox;
        private Button sendButton;
        private Button voteButton;
        private Label dayLabel;
        private Label timeLabel;

        // 게임 진행 정보
        private int currentDay = 1;
        private bool isNight = false;
        private List<Player> players = new List<Player>();

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

            // 이미지 로드
            LoadRoleImages();

            // 타이머 설정 - 8초 카운트다운
            roleViewTimer = new Timer
            {
                Interval = 1000 // 1초
            };
            roleViewTimer.Tick += RoleViewTimer_Tick;

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
                // 모든 직업 이미지 로드
                roleImages.Add("시민", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "civ1.jpg")));
                roleImages.Add("인랑", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "inrang.jpg")));
                roleImages.Add("점쟁이", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "fortuneTeller.jpg")));
                roleImages.Add("영매", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "medium.jpg")));
                roleImages.Add("사냥꾼", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "hunter.jpg")));
                roleImages.Add("네코마타", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "nekomata.jpg")));
                roleImages.Add("광인", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "madman.jpg")));
                roleImages.Add("여우", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "yoho.jpg")));
                roleImages.Add("배덕자", System.Drawing.Image.FromFile(Path.Combine(resourcePath, "immoral.jpg")));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로드 중 오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            dayLabel = new Label
            {
                Text = "day 1",
                Font = new Font("Noto Sans KR", 24, FontStyle.Bold),
                ForeColor = Color.BurlyWood,
                Location = new Point(50, 20),
                Size = new Size(150, 40),
                AutoSize = true
            };

            timeLabel = new Label
            {
                Text = "32",
                Font = new Font("Noto Sans KR", 24, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(350, 20),
                Size = new Size(100, 40),
                AutoSize = true
            };

            // 채팅 패널
            chatPanel = new Panel
            {
                Location = new Point(500, 70),
                Size = new Size(280, 450),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(20, 20, 20)
            };

            chatBox = new RichTextBox
            {
                Location = new Point(10, 10),
                Size = new Size(260, 400),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                ReadOnly = true,
                BorderStyle = BorderStyle.None
            };

            messageBox = new TextBox
            {
                Location = new Point(10, 420),
                Size = new Size(180, 20),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            sendButton = new Button
            {
                Text = "send",
                Location = new Point(200, 420),
                Size = new Size(70, 20),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat
            };

            voteButton = new Button
            {
                Text = "vote",
                Location = new Point(420, 520),
                Size = new Size(70, 30),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat
            };

            // 이벤트 연결
            sendButton.Click += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(messageBox.Text))
                {
                    // 채팅 메시지 추가
                    AddChatMessage("나", messageBox.Text);

                    // AI 응답 시뮬레이션
                    SimulateAIResponse(messageBox.Text);

                    messageBox.Clear();
                }
            };

            voteButton.Click += (sender, e) =>
            {
                MessageBox.Show("투표 기능은 아직 구현되지 않았습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 컨트롤 추가
            chatPanel.Controls.Add(chatBox);
            chatPanel.Controls.Add(messageBox);
            chatPanel.Controls.Add(sendButton);

            gamePanel.Controls.Add(dayLabel);
            gamePanel.Controls.Add(timeLabel);
            gamePanel.Controls.Add(chatPanel);
            gamePanel.Controls.Add(voteButton);

            this.Controls.Add(gamePanel);
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
                Name = "플레이어",
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
                    Name = $"AI 플레이어 {i}",
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

            // 시작 메시지 추가
            AddChatMessage("System", "게임이 시작되었습니다. 첫날 토론을 시작하세요.");
            AddChatMessage("System", $"당신의 역할은 {playerRole}입니다.");

            // AI 인사 메시지 시뮬레이션
            SimulateAIGreetings();

            // 화면 갱신
            this.Invalidate();
        }

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

            // AI 플레이어들의 인사 메시지
            for (int i = 1; i < players.Count; i++)
            {
                if (players[i].IsAI)
                {
                    string greeting = greetings[random.Next(greetings.Length)];
                    AddChatMessage(players[i].Name, greeting);

                    // 약간의 시간차를 두고 메시지가 표시된 것처럼 보이게 함
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(random.Next(300, 800));
                }
            }
        }

        /// <summary>
        /// AI 응답 시뮬레이션
        /// </summary>
        private void SimulateAIResponse(string playerMessage)
        {
            // 간단한 AI 응답 시뮬레이션
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
            int respondingAIs = random.Next(1, Math.Min(4, players.Count - 1));

            for (int i = 0; i < respondingAIs; i++)
            {
                // 살아있는 AI 중에서 랜덤 선택
                var livingAIs = players.Where(p => p.IsAI && p.IsAlive).ToList();
                if (livingAIs.Count > 0)
                {
                    var respondingAI = livingAIs[random.Next(livingAIs.Count)];
                    string response = responses[random.Next(responses.Length)];

                    // 잠시 후 응답하도록 타이머 설정
                    Timer responseTimer = new Timer();
                    responseTimer.Interval = random.Next(1000, 3000); // 1-3초 후 응답
                    responseTimer.Tick += (s, e) =>
                    {
                        AddChatMessage(respondingAI.Name, response);
                        responseTimer.Stop();
                        responseTimer.Dispose();
                    };
                    responseTimer.Start();
                }
            }
        }

        /// <summary>
        /// 채팅 메시지 추가
        /// </summary>
        private void AddChatMessage(string sender, string message)
        {
            chatBox.SelectionColor = sender == "System" ? Color.Yellow : Color.White;
            chatBox.AppendText($"{sender}: ");
            chatBox.SelectionColor = Color.LightGray;
            chatBox.AppendText($"{message}\n");
            chatBox.ScrollToCaret();
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
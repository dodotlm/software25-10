using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;

namespace InRang
{
    public partial class WaitingRoom : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread receiveThread;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        // 플레이어 관리
        private int maxPlayers;
        private int currentPlayerCount = 0;
        private List<string> playerList = new List<string>();
        private string roomName = "";

        // UI 컨트롤들
        private ListBox playerListBox;
        private Label playerCountLabel;
        private Button readyButton;
        private Button exitButton;
        private Label statusLabel;
        private bool isReady = false;
        private bool gameStarting = false;

        public WaitingRoom(TcpClient tcpClient, int playerCount = 8, int AICount = 4)
        {
            InitializeComponent();
            client = tcpClient;
            stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // GameSettings에서 최대 플레이어 수 가져오기
            maxPlayers = GameSettings.PlayerCount;

            InitializeUI();

            // 폼이 로드된 후 초기화 작업 수행
            this.Load += WaitingRoom_Load;
        }

        private void InitializeUI()
        {
            // 폼 설정
            this.Text = "대기실";
            this.Size = new Size(500, 450);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);

            // 플레이어 목록 표시
            Label titleLabel = new Label
            {
                Text = "플레이어 목록",
                Location = new Point(20, 20),
                Size = new Size(150, 25),
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                ForeColor = Color.White
            };

            playerListBox = new ListBox
            {
                Location = new Point(20, 50),
                Size = new Size(200, 220),
                Font = new Font("Noto Sans KR", 10),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 플레이어 수 표시
            playerCountLabel = new Label
            {
                Text = $"참가자: 0/{maxPlayers}",
                Location = new Point(20, 280),
                Size = new Size(200, 25),
                Font = new Font("Noto Sans KR", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(213, 176, 126)
            };

            // 상태 표시
            statusLabel = new Label
            {
                Text = "플레이어를 기다리는 중...",
                Location = new Point(250, 50),
                Size = new Size(220, 80),
                Font = new Font("Noto Sans KR", 10),
                ForeColor = Color.LightBlue,
                TextAlign = ContentAlignment.TopLeft
            };

            // 준비 버튼
            readyButton = new Button
            {
                Text = "준비",
                Location = new Point(250, 150),
                Size = new Size(100, 40),
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(213, 176, 126),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Enabled = true
            };
            readyButton.Click += ReadyButton_Click;

            // 나가기 버튼
            exitButton = new Button
            {
                Text = "나가기",
                Location = new Point(360, 150),
                Size = new Size(100, 40),
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                BackColor = Color.Gray,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            exitButton.Click += ExitButton_Click;

            // 게임 시작 조건 표시
            Label conditionLabel = new Label
            {
                Text = $"게임 시작 조건:\n• 최소 {GameSettings.PlayerCount}명 참가\n• 모든 플레이어 준비 완료\n\n현재 AI {GameSettings.AICount}명이\n자동으로 참가합니다.",
                Location = new Point(250, 210),
                Size = new Size(220, 120),
                Font = new Font("Noto Sans KR", 9),
                ForeColor = Color.LightGray,
                TextAlign = ContentAlignment.TopLeft
            };

            // 게임 정보 표시
            Label gameInfoLabel = new Label
            {
                Text = $"게임 모드: {(GameSettings.YaminabeMode ? "야미나베" : "일반")}\n양자인랑: {(GameSettings.QuantumMode ? "활성화" : "비활성화")}",
                Location = new Point(250, 340),
                Size = new Size(220, 40),
                Font = new Font("Noto Sans KR", 9),
                ForeColor = Color.FromArgb(213, 176, 126),
                TextAlign = ContentAlignment.TopLeft
            };

            // 컨트롤 추가
            this.Controls.AddRange(new Control[] {
                titleLabel, playerListBox, playerCountLabel,
                statusLabel, readyButton, exitButton, conditionLabel, gameInfoLabel
            });
        }

        private void WaitingRoom_Load(object sender, EventArgs e)
        {
            Console.WriteLine("[WaitingRoom] 대기실 로드 시작");

            // 게임 설정에서 사용자 이름 전송
            SendMessage("NAME:" + GameSettings.UserName);

            // 수신 스레드 시작
            StartReceiving();

            // 플레이어 목록 요청
            SendMessage("REQUEST_PLAYER_LIST");

            Console.WriteLine("[WaitingRoom] 초기화 완료");
        }

        private void ReadyButton_Click(object sender, EventArgs e)
        {
            if (gameStarting)
            {
                return; // 이미 게임이 시작 중이면 무시
            }

            if (!isReady)
            {
                Console.WriteLine("[WaitingRoom] 준비 버튼 클릭");

                // 준비 상태로 변경
                SendMessage("READY");
                isReady = true;

                readyButton.Enabled = false;
                readyButton.Text = "준비완료";
                readyButton.BackColor = Color.Gray;

                statusLabel.Text = "준비 완료!\n다른 플레이어를 기다리는 중...";
                statusLabel.ForeColor = Color.Green;

                // 모든 플레이어가 준비되었는지 확인
                CheckAllPlayersReady();
            }
        }

        private void CheckAllPlayersReady()
        {
            if (currentPlayerCount >= maxPlayers && !gameStarting)
            {
                Console.WriteLine("[WaitingRoom] 모든 플레이어 준비 완료 - 게임 시작");

                gameStarting = true;

                statusLabel.Text = "모든 플레이어 준비 완료!\n게임을 시작합니다...";
                statusLabel.ForeColor = Color.Gold;

                readyButton.Enabled = false;
                exitButton.Enabled = false;

                // 서버에 게임 시작 요청
                SendMessage("START_GAME_NOW");

                // 3초 후 게임 폼으로 전환
                System.Windows.Forms.Timer startTimer = new System.Windows.Forms.Timer();
                startTimer.Interval = 3000;
                startTimer.Tick += (s, args) => {
                    startTimer.Stop();
                    startTimer.Dispose();
                    TransitionToGameForm();
                };
                startTimer.Start();
            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            if (gameStarting)
            {
                MessageBox.Show("게임이 시작 중입니다. 잠시만 기다려주세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show("정말로 나가시겠습니까?", "확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                SendMessage("LEAVE_ROOM");
                this.Close();
            }
        }

        private void StartReceiving()
        {
            CancellationToken token = cancellationTokenSource.Token;

            receiveThread = new Thread(() =>
            {
                try
                {
                    while (!token.IsCancellationRequested && !gameStarting)
                    {
                        if (!client.Connected)
                        {
                            Console.WriteLine("[WaitingRoom] 서버 연결 끊김");
                            break;
                        }

                        string msg = reader.ReadLine();
                        if (msg == null) break;

                        if (this.IsHandleCreated)
                        {
                            this.BeginInvoke((MethodInvoker)(() => HandleServerMessage(msg)));
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
                {
                    Console.WriteLine("[WaitingRoom] 연결 종료: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WaitingRoom] 수신 오류: " + ex.Message);
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void HandleServerMessage(string msg)
        {
            Console.WriteLine("[WaitingRoom] 수신: " + msg);

            try
            {
                if (msg.StartsWith("PLAYER_LIST:"))
                {
                    HandlePlayerListUpdate(msg);
                }
                else if (msg.StartsWith("PLAYER_JOINED:"))
                {
                    string playerName = msg.Substring("PLAYER_JOINED:".Length);
                    statusLabel.Text = $"{playerName}님이 입장했습니다.";
                    statusLabel.ForeColor = Color.LightBlue;
                }
                else if (msg.StartsWith("PLAYER_LEFT:"))
                {
                    string playerName = msg.Substring("PLAYER_LEFT:".Length);
                    statusLabel.Text = $"{playerName}님이 퇴장했습니다.";
                    statusLabel.ForeColor = Color.Orange;
                }
                else if (msg.StartsWith("PLAYER_READY:"))
                {
                    string playerName = msg.Substring("PLAYER_READY:".Length);
                    statusLabel.Text = $"{playerName}님이 준비했습니다.";
                    statusLabel.ForeColor = Color.Green;
                }
                else if (msg.StartsWith("ROOM_JOINED:"))
                {
                    roomName = msg.Substring("ROOM_JOINED:".Length);
                    this.Text = $"대기실 - {roomName}";
                    Console.WriteLine($"[WaitingRoom] 방 참가 확인: {roomName}");
                }
                else if (msg.StartsWith("GAME_STARTING"))
                {
                    HandleGameStarting();
                }
                else if (msg.StartsWith("START_PHASE") || msg.StartsWith("ROLE:"))
                {
                    // 게임이 실제로 시작됨
                    if (!gameStarting)
                    {
                        gameStarting = true;
                        TransitionToGameForm();
                    }
                }
                else if (msg.StartsWith("ERROR:"))
                {
                    string errorMsg = msg.Substring("ERROR:".Length);
                    MessageBox.Show(errorMsg, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (msg == "ROOM_FULL")
                {
                    MessageBox.Show("방이 가득 찼습니다!", "입장 불가",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WaitingRoom] 메시지 처리 오류: " + ex.Message);
            }
        }

        private void HandlePlayerListUpdate(string msg)
        {
            string playerData = msg.Substring("PLAYER_LIST:".Length);
            Console.WriteLine($"[WaitingRoom] 플레이어 데이터: {playerData}");

            if (!string.IsNullOrEmpty(playerData))
            {
                string[] playerNames = playerData.Split(',');

                playerList.Clear();
                playerListBox.Items.Clear();

                foreach (string playerName in playerNames)
                {
                    if (!string.IsNullOrWhiteSpace(playerName))
                    {
                        string trimmedName = playerName.Trim();
                        playerList.Add(trimmedName);

                        // 준비 상태 표시
                        string displayName = trimmedName;
                        if (trimmedName.Contains("[준비]"))
                        {
                            displayName = trimmedName.Replace(" [준비]", " ✓");
                        }

                        playerListBox.Items.Add(displayName);
                    }
                }

                currentPlayerCount = playerList.Count;
                playerCountLabel.Text = $"참가자: {currentPlayerCount}/{maxPlayers}";

                Console.WriteLine($"[WaitingRoom] 플레이어 목록 업데이트: {currentPlayerCount}명");

                // 게임 시작 조건 체크
                UpdateGameStartStatus();
            }
        }

        private void UpdateGameStartStatus()
        {
            if (currentPlayerCount >= maxPlayers)
            {
                statusLabel.Text = $"방이 가득 찼습니다! ({currentPlayerCount}/{maxPlayers})\n준비 버튼을 눌러 게임을 시작하세요.";
                statusLabel.ForeColor = Color.FromArgb(213, 176, 126);

                readyButton.Enabled = !isReady && !gameStarting;

                if (isReady && !gameStarting)
                {
                    CheckAllPlayersReady();
                }
            }
            else if (!isReady && !gameStarting)
            {
                statusLabel.Text = $"플레이어를 기다리는 중...\n({currentPlayerCount}/{maxPlayers})";
                statusLabel.ForeColor = Color.LightBlue;
            }
        }

        private void HandleGameStarting()
        {
            Console.WriteLine("[WaitingRoom] 게임 시작 신호 수신");

            if (!gameStarting)
            {
                gameStarting = true;

                statusLabel.Text = "모든 플레이어 준비 완료!\n게임이 시작됩니다!";
                statusLabel.ForeColor = Color.Green;

                readyButton.Enabled = false;
                exitButton.Enabled = false;

                // 2초 후 게임 폼으로 전환
                System.Windows.Forms.Timer transitionTimer = new System.Windows.Forms.Timer();
                transitionTimer.Interval = 2000;
                transitionTimer.Tick += (s, e) => {
                    transitionTimer.Stop();
                    transitionTimer.Dispose();
                    TransitionToGameForm();
                };
                transitionTimer.Start();
            }
        }

        private void TransitionToGameForm()
        {
            try
            {
                Console.WriteLine("[WaitingRoom] 게임 폼으로 전환 시작");

                // 수신 스레드 안전하게 정리
                cancellationTokenSource.Cancel();

                // 게임 폼 생성 및 표시
                MultiPlayGameForm gameForm = new MultiPlayGameForm(client, reader, writer);

                // 게임 폼이 닫힐 때 이 폼도 닫히도록 설정
                gameForm.FormClosed += (s, e) => {
                    if (!this.IsDisposed)
                    {
                        this.Invoke((MethodInvoker)(() => {
                            this.Close();
                        }));
                    }
                };

                // UI 스레드에서 실행
                if (this.InvokeRequired)
                {
                    this.Invoke((MethodInvoker)(() => {
                        gameForm.Show();
                        this.Hide(); // 대기실 숨기기
                    }));
                }
                else
                {
                    gameForm.Show();
                    this.Hide(); // 대기실 숨기기
                }

                Console.WriteLine("[WaitingRoom] 게임 폼 전환 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WaitingRoom] 게임 폼 전환 실패: " + ex.Message);
                MessageBox.Show("게임 시작 중 오류가 발생했습니다: " + ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 오류 발생 시 상태 복구
                gameStarting = false;
                readyButton.Enabled = true;
                exitButton.Enabled = true;
            }
        }

        private void SendMessage(string message)
        {
            try
            {
                if (writer != null && client != null && client.Connected)
                {
                    writer.WriteLine(message);
                    Console.WriteLine("[WaitingRoom] 송신: " + message);
                }
                else
                {
                    Console.WriteLine("[WaitingRoom] 연결이 끊어진 상태에서 송신 시도: " + message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WaitingRoom] 메시지 전송 실패: " + ex.Message);
                MessageBox.Show("서버와의 연결에 문제가 발생했습니다.", "연결 오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            try
            {
                // 게임이 시작 중이 아닐 때만 방 나가기 메시지 전송
                if (!gameStarting)
                {
                    SendMessage("LEAVE_ROOM");
                }

                // 수신 스레드 안전하게 종료
                cancellationTokenSource.Cancel();

                if (receiveThread != null && receiveThread.IsAlive)
                {
                    receiveThread.Join(1000); // 1초 대기
                }

                Console.WriteLine("[WaitingRoom] 대기실 정상 종료");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WaitingRoom] 종료 중 예외 발생: " + ex.Message);
            }
        }
    }
}
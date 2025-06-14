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

        // UI 컨트롤들
        private ListBox playerListBox;
        private Label playerCountLabel;
        private Button readyButton;
        private Button exitButton;
        private Label statusLabel;
        private bool isReady = false;

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
            this.Size = new Size(500, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 플레이어 목록 표시
            Label titleLabel = new Label
            {
                Text = "플레이어 목록",
                Location = new Point(20, 20),
                Size = new Size(100, 20),
                Font = new Font("Noto Sans KR", 10, FontStyle.Bold)
            };

            playerListBox = new ListBox
            {
                Location = new Point(20, 50),
                Size = new Size(200, 200),
                Font = new Font("Noto Sans KR", 9)
            };

            // 플레이어 수 표시
            playerCountLabel = new Label
            {
                Text = $"참가자: 0/{maxPlayers}",
                Location = new Point(20, 260),
                Size = new Size(200, 20),
                Font = new Font("Noto Sans KR", 9, FontStyle.Bold)
            };

            // 상태 표시
            statusLabel = new Label
            {
                Text = "플레이어를 기다리는 중...",
                Location = new Point(250, 50),
                Size = new Size(200, 60),
                Font = new Font("Noto Sans KR", 9),
                ForeColor = Color.Blue
            };

            // 준비 버튼
            readyButton = new Button
            {
                Text = "준비",
                Location = new Point(250, 120),
                Size = new Size(100, 40),
                Font = new Font("Noto Sans KR", 10, FontStyle.Bold),
                BackColor = Color.LightGreen,
                Enabled = true
            };
            readyButton.Click += ReadyButton_Click;

            // 나가기 버튼
            exitButton = new Button
            {
                Text = "나가기",
                Location = new Point(250, 170),
                Size = new Size(100, 40),
                Font = new Font("Noto Sans KR", 10, FontStyle.Bold),
                BackColor = Color.LightCoral
            };
            exitButton.Click += ExitButton_Click;

            // 게임 시작 조건 표시
            Label conditionLabel = new Label
            {
                Text = $"게임 시작 조건:\n- 최소 {GameSettings.PlayerCount}명\n- 모든 플레이어 준비 완료",
                Location = new Point(250, 220),
                Size = new Size(200, 60),
                Font = new Font("Noto Sans KR", 8)
            };

            // 컨트롤 추가
            this.Controls.AddRange(new Control[] {
                titleLabel, playerListBox, playerCountLabel,
                statusLabel, readyButton, exitButton, conditionLabel
            });
        }

        private void WaitingRoom_Load(object sender, EventArgs e)
        {
            // 게임 설정에서 사용자 이름 전송
            SendMessage("NAME:" + GameSettings.UserName);

            // 수신 스레드 시작
            StartReceiving();

            Console.WriteLine("[WaitingRoom] 초기화 완료, 서버 연결 상태 확인");
        }

        private void ReadyButton_Click(object sender, EventArgs e)
        {
            if (!isReady)
            {
                SendMessage("READY");

                readyButton.Enabled = false;
                readyButton.Text = "준비 완료";
                readyButton.BackColor = Color.Gray;
                isReady = true;

                statusLabel.Text = "준비 완료!\n다른 플레이어를 기다리는 중...";
                statusLabel.ForeColor = Color.Green;

                Console.WriteLine("[WaitingRoom] 준비 완료 신호 전송");
            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
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
                    while (!token.IsCancellationRequested)
                    {
                        if (!client.Connected)
                        {
                            Console.WriteLine("[WaitingRoom] 서버 연결 끊김");
                            break;
                        }

                        string msg = reader.ReadLine();
                        if (msg == null) break;

                        this.Invoke((MethodInvoker)(() => HandleServerMessage(msg)));
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
                {
                    Console.WriteLine("[WaitingRoom] 리소스가 정리되는 동안 예외 발생: " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WaitingRoom] 알 수 없는 예외 발생: " + ex.Message);
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void HandleServerMessage(string msg)
        {
            Console.WriteLine("[WaitingRoom 메시지 처리] " + msg);

            if (msg.StartsWith("PLAYER_LIST:"))

            {
                // 플레이어 목록 업데이트
                string playerData = msg.Substring("PLAYER_LIST:".Length);
                Console.WriteLine("[WaitingRoom] 플레이어 데이터: " + playerData);

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
                            playerListBox.Items.Add(trimmedName);
                        }
                    }

                    currentPlayerCount = playerList.Count;
                    playerCountLabel.Text = $"참가자: {currentPlayerCount}/{maxPlayers}";

                    Console.WriteLine($"[WaitingRoom] 플레이어 목록 업데이트: {currentPlayerCount}명");

                    // 방이 꽉 찬 경우
                    if (currentPlayerCount >= maxPlayers)
                    {
                        statusLabel.Text = "방이 가득 찼습니다!\n모든 플레이어가 준비하면\n게임이 시작됩니다.";
                        statusLabel.ForeColor = Color.Red;
                    }
                    else if (!isReady)
                    {
                        statusLabel.Text = $"플레이어를 기다리는 중...\n({currentPlayerCount}/{maxPlayers})";
                        statusLabel.ForeColor = Color.Blue;
                    }
                }
            }
            else if (msg.StartsWith("PLAYER_JOINED:"))
            {
                string playerName = msg.Substring("PLAYER_JOINED:".Length);
                statusLabel.Text = $"{playerName}님이 입장했습니다.";
                statusLabel.ForeColor = Color.Blue;
                Console.WriteLine($"[WaitingRoom] 플레이어 입장: {playerName}");
            }
            else if (msg.StartsWith("PLAYER_LEFT:"))
            {
                string playerName = msg.Substring("PLAYER_LEFT:".Length);
                statusLabel.Text = $"{playerName}님이 퇴장했습니다.";
                statusLabel.ForeColor = Color.Orange;
                Console.WriteLine($"[WaitingRoom] 플레이어 퇴장: {playerName}");
            }
            else if (msg.StartsWith("ROOM_FULL"))
            {
                MessageBox.Show("방이 가득 찼습니다!", "입장 불가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.Close();
            }
            else if (msg.StartsWith("PLAYER_READY:"))
            {
                string playerName = msg.Substring("PLAYER_READY:".Length);
                statusLabel.Text = $"{playerName}님이 준비했습니다.";
                statusLabel.ForeColor = Color.Green;
                Console.WriteLine($"[WaitingRoom] 플레이어 준비: {playerName}");
            }
            else if (msg.StartsWith("GAME_STARTING"))
            {
                statusLabel.Text = "모든 플레이어 준비 완료!\n게임이 시작됩니다!";
                statusLabel.ForeColor = Color.Green;
                readyButton.Enabled = false;
                exitButton.Enabled = false;
                Console.WriteLine("[WaitingRoom] 게임 시작 준비");
            }
            else if (msg.StartsWith("START_PHASE"))
            {
                try
                {
                    Console.WriteLine("[WaitingRoom] 게임 폼으로 전환");

                    // 수신 스레드 정리
                    if (receiveThread != null && receiveThread.IsAlive)
                    {
                        receiveThread.Abort();
                        receiveThread = null;
                    }

                    // UI 스레드에서 실행
                    if (this.InvokeRequired)
                    {
                        this.Invoke((MethodInvoker)(() => {
                            var gameForm = new MultiPlayGameForm(client, reader, writer);
                            gameForm.FormClosed += (s, e) => this.Close();
                            gameForm.Show();
                            this.Close(); // 리소스 해제
                        }));
                    }
                    else
                    {
                        // StreamReader/Writer를 전달
                        MultiPlayGameForm gameForm = new MultiPlayGameForm(client, reader, writer);
                        gameForm.FormClosed += (s, e) => this.Close();
                        gameForm.Show();
                        this.Hide();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("게임 폼 생성 오류: " + ex.Message);
                    Console.WriteLine("[WaitingRoom] 게임 폼 생성 오류: " + ex.Message);
                }
            }
            else if (msg.StartsWith("ROLE:"))
            {
                // 역할 정보 수신 (필요시 게임 폼에서 처리)
                string role = msg.Substring("ROLE:".Length);
                Console.WriteLine("[WaitingRoom] 내 역할: " + role);
            }
            else if (msg.StartsWith("ROOM_JOINED:"))
            {
                string roomName = msg.Substring("ROOM_JOINED:".Length);
                Console.WriteLine($"[WaitingRoom] 방 참가 확인: {roomName}");
                this.Text = "대기실 - " + roomName;
            }
            else if (msg.StartsWith("ERROR:"))
            {
                string errorMsg = msg.Substring("ERROR:".Length);
                MessageBox.Show(errorMsg, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SendMessage(string message)
        {
            try
            {
                writer.WriteLine(message);
                Console.WriteLine("[WaitingRoom 송신] " + message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("메시지 전송 실패: " + ex.Message);
                Console.WriteLine("[WaitingRoom] 메시지 전송 실패: " + ex.Message);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            try
            {
                cancellationTokenSource.Cancel(); // 안전한 스레드 종료 요청
                receiveThread?.Join(500);         // 스레드가 종료되기를 기다림
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WaitingRoom] 종료 중 예외 발생: " + ex.Message);
            }
        }
    }
}
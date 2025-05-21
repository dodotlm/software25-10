using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    public partial class WaitingRoom : Form
    {
        private List<PlayerSlot> players = new List<PlayerSlot>();
        private Panel scrollPanel;
        private Button readyButton;
        private bool isReady = false;
        private Image playerImage;
        private Image computerImage;

        private Label timerLabel;
        private Timer countdownTimer;
        private int countdown = 10;

        public WaitingRoom()
        {
            this.Text = "대기실";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.Black;

            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\.."));
            string res = Path.Combine(root, "resources");
            try { playerImage = Image.FromFile(Path.Combine(res, "player.jpg")); } catch { }
            try { computerImage = Image.FromFile(Path.Combine(res, "computer.jpg")); } catch { }

            // 🔙 돌아가기
            LinkLabel backLabel = new LinkLabel()
            {
                Text = "← 돌아가기",
                LinkColor = Color.BurlyWood,
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            backLabel.Click += (s, e) =>
            {
                this.Close();  // 완전히 종료
                Application.OpenForms["MultiPlayForm"]?.Show(); // 기존 MultiPlayForm이 있다면 다시 표시
            };
            this.Controls.Add(backLabel);

            Label title = new Label()
            {
                Text = "대기실",
                Font = new Font("Noto Sans KR", 28, FontStyle.Bold),
                ForeColor = Color.BurlyWood,
                Location = new Point(50, 50),
                AutoSize = true
            };
            this.Controls.Add(title);

            scrollPanel = new Panel()
            {
                Location = new Point(50, 120),
                Size = new Size(720, 370),
                AutoScroll = true,
                BackColor = Color.Black
            };
            this.Controls.Add(scrollPanel);

            int totalCount = 20;
            int columns = 4;
            int spacingX = 170;
            int spacingY = 170;

            for (int i = 0; i < totalCount; i++)
            {
                bool isBot = i >= 5;
                PlayerSlot slot = new PlayerSlot(isBot ? computerImage : playerImage, isBot);
                if (!isBot)
                    slot.SetStatus(i == 0 ? "방장" : (i < 4 ? "준비완료" : "대기중"));

                int col = i % columns;
                int row = i / columns;
                slot.Location = new Point(col * spacingX + 10, row * spacingY);
                scrollPanel.Controls.Add(slot);
                players.Add(slot);
            }

            readyButton = new Button()
            {
                Text = "준비",
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                Size = new Size(100, 40),
                Location = new Point(680, 510),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat
            };
            readyButton.Click += ReadyButton_Click;
            this.Controls.Add(readyButton);

            timerLabel = new Label()
            {
                Text = "",
                Font = new Font("Noto Sans KR", 10, FontStyle.Bold),
                ForeColor = Color.Goldenrod,
                Location = new Point(20, 530),
                AutoSize = true,
                Visible = false
            };
            this.Controls.Add(timerLabel);

            countdownTimer = new Timer();
            countdownTimer.Interval = 1000;
            countdownTimer.Tick += CountdownTimer_Tick;
        }

        private void ReadyButton_Click(object sender, EventArgs e)
        {
            isReady = !isReady;
            readyButton.Text = isReady ? "취소" : "준비";
            readyButton.BackColor = isReady ? Color.White : Color.BurlyWood;

            if (isReady)
            {
                countdown = 10;
                timerLabel.Text = $"{countdown}초 후 게임 시작";
                timerLabel.Visible = true;
                countdownTimer.Start();
            }
            else
            {
                countdownTimer.Stop();
                timerLabel.Visible = false;
            }
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            countdown--;
            if (countdown <= 0)
            {
                countdownTimer.Stop();

                this.Hide();

                int playerCount = GameSettings.PlayerCount; // 기본 플레이어 수
                int aiCount = GameSettings.AICount;        // AI 플레이어 수
                bool yaminabeMode = GameSettings.YaminabeMode; // 야미나베 모드 설정
                bool quantumMode = GameSettings.QuantumMode;
                SinglePlayGameForm game = new SinglePlayGameForm(playerCount,
            aiCount,
            yaminabeMode,
            quantumMode);
                game.FormClosed += (s2, e2) => this.Close();
                game.Show();
            }
            else
            {
                timerLabel.Text = $"{countdown}초 후 게임 시작";
            }
        }
    }

    public class PlayerSlot : Panel
    {
        private PictureBox imageBox;
        private Label statusLabel;

        public PlayerSlot(Image img, bool isBot)
        {
            this.Size = new Size(160, 160);
            this.BackColor = Color.Black;

            imageBox = new PictureBox()
            {
                Image = img,
                Size = isBot ? new Size(120, 90) : new Size(100, 100),
                Location = new Point(20, 10),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            this.Controls.Add(imageBox);

            statusLabel = new Label()
            {
                Font = new Font("Noto Sans KR", 11, FontStyle.Bold),
                ForeColor = isBot ? Color.BurlyWood : Color.White,
                AutoSize = true,
                Text = isBot ? "봇" : "",
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(55, 120)
            };
            this.Controls.Add(statusLabel);
        }

        public void SetStatus(string status)
        {
            if (statusLabel.Text != "봇")
                statusLabel.Text = status;
        }
    }
}

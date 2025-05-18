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

            // 🟫 돌아가기 링크
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
                DialogResult result = MessageBox.Show("방을 나가시겠습니까?.", "경고", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    this.Close();
                }
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

            // ⬇ 스크롤 가능한 패널
            scrollPanel = new Panel()
            {
                Location = new Point(50, 120),
                Size = new Size(700, 370),
                AutoScroll = true,
                BackColor = Color.Black
            };
            this.Controls.Add(scrollPanel);

            // 예시 인원 수 (나중에 설정 가능)
            int totalCount = 10;

            for (int i = 0; i < totalCount; i++)
            {
                PlayerSlot slot = new PlayerSlot(i < 5 ? playerImage : computerImage);
                slot.SetStatus(i == 0 ? "방장" : (i < 4 ? "준비완료" : "대기중"));
                slot.SetBot(i >= 5);
                slot.Location = new Point(0, i * 120); // 아래로 정렬
                scrollPanel.Controls.Add(slot);
                players.Add(slot);
            }

            // 준비 버튼
            readyButton = new Button()
            {
                Text = "준비",
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                Size = new Size(100, 40),
                Location = new Point(680, 510),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat
            };
            readyButton.Click += (s, e) =>
            {
                isReady = !isReady;
                readyButton.Text = isReady ? "취소" : "준비";
                readyButton.BackColor = isReady ? Color.White : Color.BurlyWood;

                // 원하는 로직 연결 가능 (서버 전송 등)
            };
            this.Controls.Add(readyButton);
        }
    }

    // ✅ 개별 슬롯 클래스 (플레이어 1명 또는 봇 1대)
    public class PlayerSlot : Panel
    {
        private PictureBox imageBox;
        private Label statusLabel;
        private Label botLabel;

        public PlayerSlot(Image img)
        {
            this.Size = new Size(680, 110);
            this.BackColor = Color.Black;

            imageBox = new PictureBox()
            {
                Image = img,
                Size = new Size(80, 100),
                Location = new Point(20, 5),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            this.Controls.Add(imageBox);

            statusLabel = new Label()
            {
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(120, 20)
            };
            this.Controls.Add(statusLabel);

            botLabel = new Label()
            {
                Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                ForeColor = Color.BurlyWood,
                AutoSize = true,
                Location = new Point(120, 60)
            };
            this.Controls.Add(botLabel);
        }

        public void SetStatus(string status)
        {
            statusLabel.Text = status;
        }

        public void SetBot(bool isBot)
        {
            botLabel.Text = isBot ? "봇" : "";
        }
    }
}

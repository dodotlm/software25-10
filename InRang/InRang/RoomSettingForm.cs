using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    public partial class RoomSettingForm : Form
    {
        private string[] menuItems = { "인원 수", "AI 갯수", "야미나베 모드", "양자인랑 모드", "뒤로 가기" };
        private int hoveredIndex = -1;
        private int selectedIndex = 0;

        private System.Drawing.Image playerImage;
        private System.Drawing.Image computerImage;

        private Font titleFont;
        private Font menuFont;
        private Font contentFont;
        private Font largeFont;

        private int playerCount = 8;
        private int aiCount = 4;

        private bool quantumMode = false;  // 기본 비활성화
        private bool yaminabeMode = false;

        private TextBox inputBox;
        private Button enterButton;

        public RoomSettingForm()
        {
            this.Text = "방 생성 설정";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;

            titleFont = new Font("Noto Sans KR", 28, FontStyle.Bold);
            menuFont = new Font("Noto Sans KR", 16, FontStyle.Bold);
            contentFont = new Font("Noto Sans KR", 11, FontStyle.Bold);
            largeFont = new Font("Noto Sans KR", 22, FontStyle.Bold);

            // 초기 설정값 로드
            playerCount = GameSettings.PlayerCount;
            aiCount = GameSettings.AICount;
            yaminabeMode = GameSettings.YaminabeMode;
            quantumMode = GameSettings.QuantumMode;

            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\.."));
            string res = Path.Combine(root, "resources");
            try { playerImage = System.Drawing.Image.FromFile(Path.Combine(res, "player.jpg")); } catch { }
            try { computerImage = System.Drawing.Image.FromFile(Path.Combine(res, "computer.jpg")); } catch { }

            inputBox = new TextBox { Width = 100, Font = contentFont, Visible = true };
            enterButton = new Button
            {
                Text = "ENTER",
                Width = 70,
                Height = 25,
                Font = new Font("Noto Sans KR", 9, FontStyle.Bold),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat,
                Visible = true
            };

            enterButton.Click += EnterButton_Click;
            inputBox.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };

            Controls.Add(inputBox);
            Controls.Add(enterButton);
            UpdateControls();

            MouseMove += (s, e) =>
            {
                int h = GetMenuIndex(e.Location);
                if (h != hoveredIndex) { hoveredIndex = h; Invalidate(); }
            };
            MouseClick += (s, e) =>
            {
                int c = GetMenuIndex(e.Location);
                if (c >= 0)
                {
                    selectedIndex = c;
                    UpdateControls();
                    Invalidate();

                    if (selectedIndex == 4)
                    {
                        // 설정을 전역 설정에 저장한 후 화면 전환
                        SaveSettingsAndReturn();
                    }
                }
            };
        }

        private int GetMenuIndex(Point p)
        {
            int y = 200, h = 40;
            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle r = new Rectangle(60, y + i * 60, 200, h);
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        private void EnterButton_Click(object sender, EventArgs e)
        {
            if (int.TryParse(inputBox.Text, out int val))
            {
                if (selectedIndex == 0)
                {
                    // 플레이어 수는 최소 4명 이상
                    playerCount = val < 4 ? 4 : val;

                    // AI 수는 플레이어 수보다 작아야 함
                    if (aiCount >= playerCount)
                        aiCount = playerCount - 1;
                }
                if (selectedIndex == 1)
                {
                    // AI 수는 플레이어 수보다 작아야 함
                    aiCount = val >= playerCount ? playerCount - 1 : val;
                }
                Invalidate();
            }
        }

        private void UpdateControls()
        {
            bool inputVisible = selectedIndex == 0 || selectedIndex == 1;
            inputBox.Visible = inputVisible;
            enterButton.Visible = inputVisible;
        }

        /// <summary>
        /// 현재 설정을 전역 설정에 저장하고 이전 화면으로 돌아감
        /// </summary>
        private void SaveSettingsAndReturn()
        {
            // 전역 설정에 현재 값 저장
            GameSettings.PlayerCount = playerCount;
            GameSettings.AICount = aiCount;
            GameSettings.YaminabeMode = yaminabeMode;
            GameSettings.QuantumMode = quantumMode;

            // 이전 화면으로 돌아가기
            this.Hide();
            StartGameMenu startGameMenu = new StartGameMenu();
            startGameMenu.StartPosition = FormStartPosition.Manual;
            startGameMenu.Location = this.Location;
            startGameMenu.FormClosed += (s2, e2) => this.Close();
            startGameMenu.Show();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawString("방 생성 설정", titleFont, Brushes.BurlyWood, new PointF(60, 50));

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle r = new Rectangle(60, 200 + i * 60, 200, 40);
                Brush b = (i == hoveredIndex || i == selectedIndex) ? Brushes.Goldenrod : Brushes.BurlyWood;
                g.FillRectangle(b, r);
                g.DrawRectangle(Pens.Black, r);
                g.DrawString(menuItems[i], menuFont, Brushes.Black, r, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }

            Rectangle panel = new Rectangle(300, 150, 460, 400);
            g.DrawRectangle(new Pen(Color.BurlyWood, 2), panel);

            StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            StringFormat topCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

            if (selectedIndex == 0)
            {
                int imgWidth = 180;
                int imgHeight = 220;
                if (playerImage != null)
                {
                    g.DrawImage(playerImage, new Rectangle(panel.X + (panel.Width - imgWidth) / 2 + 18, panel.Y, imgWidth, imgHeight));
                }
                g.DrawString(playerCount + "명", largeFont, Brushes.White, new RectangleF(panel.X, panel.Y + 180, panel.Width, 40), center);
                g.DrawString("현재 인원 수는", contentFont, Brushes.White, new RectangleF(panel.X, panel.Y + 220, panel.Width, 30), center);
                g.DrawString("참여하는 모든 플레이어의 인원 수를 설정합니다.\nAI의 숫자 또한 포함해서 설정해 주십시오.",
                             contentFont, Brushes.White, new RectangleF(panel.X, panel.Y + 270, panel.Width, 60), topCenter);
                inputBox.Location = new Point(panel.X + (panel.Width - 200) / 2, panel.Y + 340);
                enterButton.Location = new Point(inputBox.Right + 10, inputBox.Top);
            }
            else if (selectedIndex == 1)
            {
                int imgWidth = 300;
                int imgHeight = 200;
                if (computerImage != null)
                {
                    g.DrawImage(computerImage, new Rectangle(panel.X + (panel.Width - imgWidth) / 2, panel.Y, imgWidth, imgHeight));
                }
                g.DrawString(aiCount + "대", largeFont, Brushes.White, new RectangleF(panel.X, panel.Y + 150, panel.Width, 40), center);
                g.DrawString("현재 AI 수는", contentFont, Brushes.White, new RectangleF(panel.X, panel.Y + 190, panel.Width, 30), center);
                g.DrawString("게임에 참여할 AI의 숫자를 설정해 주세요.", contentFont, Brushes.White, new RectangleF(panel.X, panel.Y + 240, panel.Width, 40), center);
                inputBox.Location = new Point(panel.X + (panel.Width - 200) / 2, panel.Y + 300);
                enterButton.Location = new Point(inputBox.Right + 10, inputBox.Top);
            }
            else if (selectedIndex == 2)
            {
                string yamiText = "야미나베는 일본어로 잡탕이라는 뜻으로\n직업의 종류와 수가 랜덤으로 결정됩니다.\n임의로 여러 직업이 배정되므로,\n매번 전혀 다른 게임 구성이 됩니다.";
                string statusText = yaminabeMode ? "활성화" : "비활성화";
                g.DrawString(yamiText, contentFont, Brushes.White, new RectangleF(panel.X, panel.Y + 50, panel.Width, 140), topCenter);
                g.DrawString($"현재 : {statusText}", contentFont, Brushes.White, new PointF(panel.X + 170, panel.Y + 210));

                Rectangle toggleRect = new Rectangle(panel.X + 200, panel.Y + 240, 50, 25);
                g.FillRectangle(Brushes.Gray, toggleRect);
                g.FillEllipse(yaminabeMode ? Brushes.SteelBlue : Brushes.LightGray,
                    yaminabeMode ? new Rectangle(toggleRect.Right - 25, toggleRect.Y, 25, 25) : new Rectangle(toggleRect.X, toggleRect.Y, 25, 25));

                this.MouseClick += (s, e2) =>
                {
                    if (toggleRect.Contains(e2.Location))
                    {
                        yaminabeMode = !yaminabeMode;
                        if (yaminabeMode)
                            quantumMode = false; // 야미나베 모드 자동 해제
                        GameSettings.YaminabeMode = yaminabeMode; // 서버에 반영됨
                        Console.WriteLine("🟢 야미나베 모드 UI에서 활성화됨");
                        Invalidate();
                    }
                };
            }
            else if (selectedIndex == 3)
            {
                string quantumText = "모든 플레이어가 자신의 직업이 무엇인지 확정하지 못한 상태에서\n게임을 진행합니다.\n각 플레이어의 직업 확률이 공개되며,\n매 턴마다 직업 확률이 변화합니다.";
                string statusText = quantumMode ? "활성화" : "비활성화";
                g.DrawString(quantumText, contentFont, Brushes.White, new RectangleF(panel.X, panel.Y + 50, panel.Width, 140), topCenter);
                g.DrawString($"현재 : {statusText}", contentFont, Brushes.White, new PointF(panel.X + 170, panel.Y + 210));

                Rectangle toggleRect = new Rectangle(panel.X + 200, panel.Y + 240, 50, 25);
                g.FillRectangle(Brushes.Gray, toggleRect);
                g.FillEllipse(quantumMode ? Brushes.SteelBlue : Brushes.LightGray,
                    quantumMode ? new Rectangle(toggleRect.Right - 25, toggleRect.Y, 25, 25) : new Rectangle(toggleRect.X, toggleRect.Y, 25, 25));

                this.MouseClick += (s, e2) =>
                {
                    if (toggleRect.Contains(e2.Location))
                    {
                        quantumMode = !quantumMode;
                        if (quantumMode)
                            yaminabeMode = false; // 야미나베 모드 자동 해제
                        Invalidate();
                    }
                };
            }
        }
    }
}
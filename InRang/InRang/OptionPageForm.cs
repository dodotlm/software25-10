using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    public partial class OptionPageForm : Form
    {
        private string ip_address = "100.100.100.100";  // 예시 ip주소


        private string[] menuItems = { "불륨 조절", "라이센스 확인", "네트워크 확인", "뒤로가기" };
        private int hoveredIndex = -1;
        private int selectedIndex = 0;

        private Font titleFont;
        private Font menuFont;
        private Font verFont;

        // 📌 패널 정의
        private Panel volumePanel;
        private Panel licensePanel;
        private Panel networkPanel;

        private PictureBox sliderBar;
        private PictureBox sliderHandle;
        private int sliderMin = 50;
        private int sliderMax = 325;
        private int sliderValue = 0;
        private int sliderStep = 60;

        public OptionPageForm()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Text = "옵션";
            this.BackColor = Color.Black;
            this.ForeColor = Color.Goldenrod;
            this.DoubleBuffered = true;

            titleFont = new Font("Noto Sans KR", 36, FontStyle.Bold);
            menuFont = new Font("Noto Sans KR", 13, FontStyle.Bold);
            verFont = new Font("Noto Sans KR", 8, FontStyle.Bold);

            Label labelTitle = new Label();
            labelTitle.Text = "옵션";
            labelTitle.Font = new Font("Noto Sans KR", 24, FontStyle.Bold);
            labelTitle.ForeColor = Color.BurlyWood;
            labelTitle.Location = new Point(150, 50);
            labelTitle.Size = new Size(100, 40);
            this.Controls.Add(labelTitle);

            InitializePanels();

            this.MouseMove += OptionPageForm_MouseMove;
            this.MouseClick += OptionPageForm_MouseClick;

            this.ResumeLayout(false);

            // 기본 화면을 "불륨 조절"로 세팅
            ShowPanel(volumePanel);
        }

        private void InitializePanels()
        {
            // 📌 프로젝트 최상위 경로 설정
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            // 🔸 볼륨 조절 패널 생성
            volumePanel = new Panel
            {
                Location = new Point(350, 100),
                Size = new Size(400, 400),
                BorderStyle = BorderStyle.FixedSingle,
            };

            // 🔸 슬라이더 바 생성 
            sliderBar = new PictureBox
            {
                Image = Image.FromFile(Path.Combine(resourcePath, "scrollbar_body.jpg")), 
                Location = new Point(70, 15),
                Size = new Size(100, 355),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent
            };
            volumePanel.Controls.Add(sliderBar);

            // 🔸 슬라이더 핸들 생성 
            sliderHandle = new PictureBox
            {
                Image = Image.FromFile(Path.Combine(resourcePath, "scrollbar_point.jpg")), 
                Location = new Point(115, sliderMax),
                Size = new Size(25, 25),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            volumePanel.Controls.Add(sliderHandle);

            // 🔹 핸들 드래그 이벤트 처리
            sliderHandle.MouseDown += (s, e) =>
            {
                sliderHandle.Capture = true; // 드래그 시작 시 캡처 시작
            };

            sliderHandle.MouseUp += (s, e) =>
            {
                sliderHandle.Capture = false; // 드래그 끝날 때 캡처 해제
                Slider_ValueChanged(s, e); // 🔸 드래그 끝났을 때 이벤트 핸들러 호출
            };

            sliderHandle.MouseMove += (s, e) =>
            {
                if (sliderHandle.Capture)
                {
                    int newY = sliderHandle.Top + e.Y;

                    // 🔸 범위 제한
                    if (newY < sliderMin) newY = sliderMin;
                    if (newY > sliderMax) newY = sliderMax;

                    // 🔸 6개의 구간으로 나누기
                    int totalHeight = sliderMax - sliderMin;
                    int stepSize = totalHeight / 5;

                    // 🔸 드래그한 위치에서 가까운 단계로 스냅(Snap)
                    int relativePosition = newY - sliderMin;
                    int stepIndex = (int)Math.Round((double)relativePosition / stepSize);

                    // 🔸 새 Y 좌표 계산 (각 단계의 중앙으로 스냅)
                    newY = sliderMin + (stepIndex * stepSize);
                    sliderHandle.Top = newY;

                }
            };




            // 🔸 텍스트 레이블들
            string[] labels = { "폭력적인", "파괴적인", "생동감 넘치는", "고조되는 긴장", "고요한 상태", "음소거" };
            int baseY = 45;      // 초기 Y 좌표
            int offsetY = 55;    

            for (int i = 0; i < labels.Length; i++)
            {
                Label label = new Label
                {
                    Text = labels[i],
                    Font = new Font("Noto Sans KR", 13, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(180, baseY + (offsetY * i)),
                    Size = new Size(200, 30)
                };
                volumePanel.Controls.Add(label);
            }

            // 🔸 텍스트 레이블을 슬라이더 바 위로 배치
            for (int i = 0; i < labels.Length; i++)
            {
                volumePanel.Controls.SetChildIndex(volumePanel.Controls[i], 0); // 텍스트 레이블을 맨 위로 배치
            }

            // 🔸 슬라이더 핸들 위로 이동
            volumePanel.Controls.SetChildIndex(sliderHandle, 1); // 슬라이더 핸들을 슬라이더 바 위로 배치



            // 🔸 라이센스 확인 패널 생성
            licensePanel = new Panel
            {
                Location = new Point(350, 100),
                Size = new Size(400, 400),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 🔸 라이센스 관련 텍스트 레이블 추가
            string[] licenseTexts =
            {
                "제품명: InRang",
                "버전: 1.0.0",
                "© 2025 InRang Corporation. All rights reserved."
            };

            int licenseBaseY = 100;
            int licenseOffsetY = 50;

            for (int i = 0; i < licenseTexts.Length; i++)
            {
                Label label = new Label
                {
                    Text = licenseTexts[i],
                    Font = new Font("Noto Sans KR", 12, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(30, licenseBaseY + (licenseOffsetY * i)),
                    Size = new Size(250, 30)
                };
                licensePanel.Controls.Add(label);
            }


            // 🔹 네트워크 확인 패널
            networkPanel = new Panel
            {
                Location = new Point(350, 100),
                Size = new Size(400, 400),
                BorderStyle = BorderStyle.FixedSingle
            };

            string[] networkTexts = { "현재 사용하고 계신 IP는", ip_address, "멀티 플레이어와 같은 IP를 사용해야 합니다." };

            // 🔹 첫 번째 레이블
            Label label1 = new Label
            {
                Text = "현재 사용하고 계신 IP는",
                Font = new Font("Noto Sans KR", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(25, 100),
                Size = new Size(350, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            networkPanel.Controls.Add(label1);

            // 🔹 두 번째 레이블 (IP 주소)
            Label label2 = new Label
            {
                Text = ip_address,
                Font = new Font("Noto Sans KR", 25, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(25, 180),
                Size = new Size(350, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            networkPanel.Controls.Add(label2);

            // 🔹 세 번째 레이블
            Label label3 = new Label
            {
                Text = "멀티 플레이어와 같은 IP를 사용해야 합니다.",
                Font = new Font("Noto Sans KR", 12, FontStyle.Regular),
                ForeColor = Color.White,
                Location = new Point(25, 260),
                Size = new Size(350, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            networkPanel.Controls.Add(label3);

            // 🔸 패널을 폼에 추가하고 기본적으로는 숨기기
            this.Controls.Add(volumePanel);
            this.Controls.Add(licensePanel);
            this.Controls.Add(networkPanel);


            volumePanel.Visible = false;
            licensePanel.Visible = false;
            networkPanel.Visible = false;

        }


        private void ShowPanel(Panel panel)
        {
            // 🔹 모든 패널을 숨김
            volumePanel.Visible = false;
            licensePanel.Visible = false;
            networkPanel.Visible = false;

            // 🔹 요청된 패널만 표시
            if (panel != null)
                panel.Visible = true;
        }


        private void OptionPageForm_MouseMove(object sender, MouseEventArgs e)
        {
            int newHovered = GetMenuIndexAtPoint(e.Location);
            if (newHovered != hoveredIndex)
            {
                hoveredIndex = newHovered;
                this.Invalidate();
            }
        }

        private void OptionPageForm_MouseClick(object sender, MouseEventArgs e)
        {
            int clickedIndex = GetMenuIndexAtPoint(e.Location);
            if (clickedIndex >= 0)
            {
                selectedIndex = clickedIndex;
                this.Invalidate();
                HandleMenuClick(menuItems[clickedIndex]);
            }
        }

        private int GetMenuIndexAtPoint(Point p)
        {
            int startY = 200;
            int spacing = 60;
            int itemHeight = 40;

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle rect = new Rectangle(100, startY + i * spacing, 200, itemHeight);
                if (rect.Contains(p))
                    return i;
            }
            return -1;
        }

        private void HandleMenuClick(string menu)
        {
            switch (menu)
            {
                case "불륨 조절":
                    ShowPanel(volumePanel);
                    break;
                case "라이센스 확인":
                    ShowPanel(licensePanel);
                    break;
                case "네트워크 확인":
                    ShowPanel(networkPanel);
                    break;
                case "뒤로가기":
                    StartPageForm mainMenu = new StartPageForm();
                    mainMenu.Show();
                    this.Close();
                    break;
            }
        }



        // 🔹 슬라이더 값이 변경될 때 호출되는 이벤트 핸들러
        private void Slider_ValueChanged(object sender, EventArgs e)
        {
            // 슬라이더의 현재 위치에 따른 값 계산
            int totalHeight = sliderMax - sliderMin;
            int stepSize = totalHeight / 5;

            // 현재 슬라이더의 Y 좌표에서 단계 계산
            int relativePosition = sliderHandle.Top - sliderMin;
            int stepIndex = (int)Math.Round((double)relativePosition / stepSize);

            // 🔸 슬라이더 값 계산 (0 ~ 5 단계)
            sliderValue = 5 - stepIndex;

            // 필요한 로직 처리 <= 추후에 볼륨 조절 기능 추가해야됨
            MessageBox.Show("볼륨 조정됨.");
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            int startY = 200;
            int spacing = 60;
            int buttonWidth = 200;
            int buttonHeight = 40;
            int startX = 100;

            int borderX = startX - 10;
            int borderY = startY - 10;
            int borderWidth = buttonWidth + 20;
            int borderHeight = 240;

            Rectangle borderRect = new Rectangle(borderX, borderY, borderWidth, borderHeight);
            g.DrawRectangle(new Pen(Color.BurlyWood, 3), borderRect);

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle buttonRect = new Rectangle(startX, startY + i * spacing, buttonWidth, buttonHeight);

                Brush buttonBrush = (i == selectedIndex || i == hoveredIndex) ? Brushes.Wheat : Brushes.BurlyWood;
                g.FillRectangle(buttonBrush, buttonRect);
                g.DrawRectangle(Pens.Black, buttonRect);

                StringFormat textFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(menuItems[i], menuFont, Brushes.Black, buttonRect, textFormat);
            }

            Rectangle borderRect_2 = new Rectangle(350, 100, 400, 400);
            g.DrawRectangle(new Pen(Color.BurlyWood, 5), borderRect_2);
        }
    }
}

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    public partial class StartGameMenu : Form
    {
        private string[] menuItems = { "싱글 플레이", "멀티 플레이", "방 생성 설정", "뒤로 가기" };
        private int hoveredIndex = -1;

        private Image leftCharacter;
        private Image rightCharacter;

        // 전역 폰트 (Noto Sans KR Bold)
        private Font titleFont;
        private Font menuFont;
        private Font verFont;

        public StartGameMenu()
        {
            // 폼 기본 속성 설정
            this.Text = "게임 시작";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;

            // 📌 글꼴 설정 (Noto Sans KR Bold)
            titleFont = new Font("Noto Sans KR", 36, FontStyle.Bold);
            menuFont = new Font("Noto Sans KR", 13, FontStyle.Bold);
            verFont = new Font("Noto Sans KR", 8, FontStyle.Bold);

            // 📌 resources 폴더 기준으로 이미지 경로 설정
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            string leftImageFile = Path.Combine(resourcePath, "civ1.jpg");
            string rightImageFile = Path.Combine(resourcePath, "civ2.jpg");

            // 이미지 로드 (예외 처리 포함)
            try { leftCharacter = Image.FromFile(leftImageFile); } catch { leftCharacter = null; }
            try { rightCharacter = Image.FromFile(rightImageFile); } catch { rightCharacter = null; }

            // 마우스 이벤트 등록
            this.MouseMove += StartGameMenu_MouseMove;
            this.MouseClick += StartGameMenu_MouseClick;
        }

        private void StartGameMenu_MouseMove(object sender, MouseEventArgs e)
        {
            int newHovered = GetMenuIndexAtPoint(e.Location);
            if (newHovered != hoveredIndex)
            {
                hoveredIndex = newHovered;
                this.Invalidate();
            }
        }

        private void StartGameMenu_MouseClick(object sender, MouseEventArgs e)
        {
            int clickedIndex = GetMenuIndexAtPoint(e.Location);
            if (clickedIndex >= 0)
            {
                HandleMenuClick(menuItems[clickedIndex]);
            }
        }

        private int GetMenuIndexAtPoint(Point p)
        {
            int startY = 200;
            int spacing = 60;
            int buttonWidth = 200;
            int buttonHeight = 40;
            int centerX = (this.ClientSize.Width - buttonWidth) / 2;

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle rect = new Rectangle(centerX, startY + i * spacing, buttonWidth, buttonHeight);
                if (rect.Contains(p)) return i;
            }
            return -1;
        }

        private void HandleMenuClick(string menu)
        {
            switch (menu)
            { 
                case "싱글 플레이":
                    WaitingRoom waitingRoom = new WaitingRoom();
                    waitingRoom.Show();
                    this.Hide();
                    break;
                case "멀티 플레이":
                    MultiPlayForm multiPlayForm = new MultiPlayForm();
                    multiPlayForm.Show();
                    this.Hide();
                    break;
                case "방 생성 설정":
                    // StartGameMenu 폼 열기
                    RoomSettingForm roomSet = new RoomSettingForm();
                    roomSet.Show();
                    // 현재 StartPageForm 닫기 (필요시)
                    this.Hide();  // 창 닫지 말고 숨김 (뒤로 가기 시 다시 보이게 가능)
                    break;
                case "뒤로 가기":
                    StartPageForm mainMenu = new StartPageForm();
                    mainMenu.Show();
                    this.Close();
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 1️⃣ 좌우 캐릭터 이미지 (높이 400px + 여백 늘림)
            int sideMargin = 60; // 기존 20px → 60px로 늘림
            int imgHeight = 400;

            if (leftCharacter != null)
            {
                int imgWidth = (int)((float)leftCharacter.Width / leftCharacter.Height * imgHeight);
                Rectangle leftRect = new Rectangle(sideMargin, 150, imgWidth, imgHeight);
                g.DrawImage(leftCharacter, leftRect);
            }

            if (rightCharacter != null)
            {
                int imgWidth = (int)((float)rightCharacter.Width / rightCharacter.Height * imgHeight);
                Rectangle rightRect = new Rectangle(this.ClientSize.Width - imgWidth - sideMargin, 150, imgWidth, imgHeight);
                g.DrawImage(rightCharacter, rightRect);
            }

            // 2️⃣ 제목 텍스트
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("게임 시작", titleFont, Brushes.BurlyWood, new RectangleF(0, 50, this.ClientSize.Width, 60), centerFormat);

            // 3️⃣ 메뉴 리스트 (버튼 배경 + 글자)
            int startY = 200;
            int spacing = 60;
            int buttonWidth = 200;
            int buttonHeight = 40;
            int centerX = (this.ClientSize.Width - buttonWidth) / 2;

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle buttonRect = new Rectangle(centerX, startY + i * spacing, buttonWidth, buttonHeight);
                Brush buttonBrush = (i == hoveredIndex) ? Brushes.Goldenrod : Brushes.BurlyWood;

                // 버튼 배경
                g.FillRectangle(buttonBrush, buttonRect);

                // 버튼 테두리 (선택사항, 넣으면 더 깔끔함)
                g.DrawRectangle(Pens.Black, buttonRect);

                // 글자 (검정색)
                StringFormat textFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(menuItems[i], menuFont, Brushes.Black, buttonRect, textFormat);
            }

            // 4️⃣ 버전 정보
            g.DrawString("ver.1.0.0", verFont, Brushes.BurlyWood, this.ClientSize.Width - 70, this.ClientSize.Height - 20);
        }
    }
}

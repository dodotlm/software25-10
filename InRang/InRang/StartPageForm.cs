using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    public partial class StartPageForm : Form
    {
        private string[] menuItems = { "시작하기", "옵션", "도움말", "나가기" };
        private int hoveredIndex = -1;

        private Image wolfImage;

        // 전역 폰트 (Noto Sans KR Bold)
        private Font titleFont;
        private Font menuFont;
        private Font verFont;

        public StartPageForm()
        {
            // 폼 기본 속성 설정
            this.Text = "人狼ゲーム";
            this.ClientSize = new Size(800, 600);  // 창 크기 고정 (800x600)
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // 크기 변경 불가
            this.MaximizeBox = false;  // 최대화 버튼 비활성화
            this.DoubleBuffered = true;  // 깜빡임 방지
            this.BackColor = Color.Black;

            // 📌 글꼴 설정 (Noto Sans KR Bold)
            titleFont = new Font("Noto Sans KR", 36, FontStyle.Bold);
            menuFont = new Font("Noto Sans KR", 20, FontStyle.Bold);
            verFont = new Font("Noto Sans KR", 8, FontStyle.Bold);

            // 📌 resources 폴더 기준으로 이미지 경로 설정
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            string[] imageFiles = {
                Path.Combine(resourcePath, "inrang.jpg"),
                Path.Combine(resourcePath, "yoho.jpg"),
                Path.Combine(resourcePath, "hunter.jpg"),
                Path.Combine(resourcePath, "immoral.jpg")
            };

            // 랜덤으로 이미지 선택 후 로드 (예외 처리 포함)
            Random rand = new Random();
            int index = rand.Next(imageFiles.Length);
            string selectedImageFile = imageFiles[index];

            try
            {
                wolfImage = Image.FromFile(selectedImageFile);
            }
            catch (Exception)
            {
                wolfImage = null;
            }

            // 마우스 이벤트 등록
            this.MouseMove += StartPageForm_MouseMove;
            this.MouseClick += StartPageForm_MouseClick;
        }

        private void StartPageForm_MouseMove(object sender, MouseEventArgs e)
        {
            int newHovered = GetMenuIndexAtPoint(e.Location);
            if (newHovered != hoveredIndex)
            {
                hoveredIndex = newHovered;
                this.Invalidate();  // 다시 그리기 요청
            }
        }

        private void StartPageForm_MouseClick(object sender, MouseEventArgs e)
        {
            int clickedIndex = GetMenuIndexAtPoint(e.Location);
            if (clickedIndex >= 0)
            {
                HandleMenuClick(menuItems[clickedIndex]);
            }
        }

        private int GetMenuIndexAtPoint(Point p)
        {
            int startY = 300;
            int spacing = 50;
            int itemHeight = 40;

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle rect = new Rectangle(220, startY + i * spacing, 200, itemHeight);
                if (rect.Contains(p)) return i;
            }
            return -1;
        }

        private void HandleMenuClick(string menu)
        {
            switch (menu)
            {
                case "시작하기":
                    // StartGameMenu 폼 열기
                    StartGameMenu startMenu = new StartGameMenu();
                    startMenu.Show();

                    // 현재 StartPageForm 닫기 (필요시)
                    this.Hide();  // 창 닫지 말고 숨김 (뒤로 가기 시 다시 보이게 가능)
                    break;
                case "옵션":
                    OptionPageForm optionPageForm = new OptionPageForm();
                    optionPageForm.Show();
                    this.Hide();
                    break;
                case "도움말":
                    HelpForm helpForm = new HelpForm();
                    helpForm.Show();
                    this.Hide();
                    break;
                case "나가기":
                    this.Close();
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 1️⃣ 랜덤 선택된 배경 이미지 (가운데 정렬 + 투명도 20%)
            if (wolfImage != null)
            {
                int imgX = (this.ClientSize.Width - wolfImage.Width) / 2;
                int imgY = (this.ClientSize.Height - wolfImage.Height) / 2;
                Rectangle destRect = new Rectangle(imgX, imgY, wolfImage.Width, wolfImage.Height);

                System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix(
                    new float[][]
                    {
                        new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 0.2f, 0},  // Alpha = 0.2 (20% 불투명)
                        new float[] {0, 0, 0, 0, 1}
                    });

                System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetColorMatrix(cm);

                g.DrawImage(wolfImage, destRect, 0, 0, wolfImage.Width, wolfImage.Height, GraphicsUnit.Pixel, ia);
            }

            // 2️⃣ 게임명 텍스트
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("人狼ゲーム", titleFont, Brushes.BurlyWood, new RectangleF(0, 30, this.ClientSize.Width, 60), centerFormat);

            // 3️⃣ 메뉴 리스트
            int startY = 300;
            int spacing = 50;
            for (int i = 0; i < menuItems.Length; i++)
            {
                Brush brush = (i == hoveredIndex) ? Brushes.Goldenrod : Brushes.BurlyWood;
                g.DrawString(menuItems[i], menuFont, brush, new RectangleF(0, startY + i * spacing, this.ClientSize.Width, 40), centerFormat);
            }

            // 4️⃣ 버전 정보
            g.DrawString("ver.1.0.0", verFont, Brushes.BurlyWood, this.ClientSize.Width - 70, this.ClientSize.Height - 20);
        }
    }
}

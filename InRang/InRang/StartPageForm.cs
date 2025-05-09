using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace InRang
{
    public partial class StartPageForm : Form
    {
        private string[] menuItems = { "시작하기", "옵션", "도움말", "나가기" };
        private int hoveredIndex = -1;
        private Image wolfImage;

        public StartPageForm()
        {
            InitializeComponent();

            // 폼 초기화
            this.Text = "人狼ゲーム";
            this.ClientSize = new Size(800, 600);  // 크기 고정 (800x600)
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // 크기 변경 불가
            this.MaximizeBox = false;  // 최대화 버튼 비활성화
            this.DoubleBuffered = true;  // 깜빡임 방지
            this.BackColor = Color.Black;

            // 이미지 로드
            wolfImage = Image.FromFile("inrang.jpg");

            // 마우스 이벤트
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

        // 이 부분에서 이제 다른 페이지 들어가는 로직 짜면 된다.
        private void HandleMenuClick(string menu)
        {
            switch (menu)
            {
                case "시작하기":
                    MessageBox.Show("게임 시작!");
                    break;
                case "옵션":
                    MessageBox.Show("옵션 열기");
                    break;
                case "도움말":
                    MessageBox.Show("도움말 표시");
                    break;
                case "나가기":
                    this.Close();
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 1️⃣ 배경 이미지 (크기 조정 및 투명도 적용)
            float scaleFactor = 1.1f;  // 1.1배 크기 조정
            int newWidth = (int)(wolfImage.Width / scaleFactor);  // 1.3배로 크기 조정
            int newHeight = (int)(wolfImage.Height / scaleFactor);  // 1.3배로 크기 조정
            int imgX = (this.ClientSize.Width - newWidth) / 2;
            int imgY = (this.ClientSize.Height - newHeight) / 2;

            // 이미지에 투명도 적용
            using (ImageAttributes imageAttributes = new ImageAttributes())
            {
                // 투명도 설정 (50% 투명도)
                float[][] colorMatrixElements = {
        new float[] {1, 0, 0, 0, 0},
        new float[] {0, 1, 0, 0, 0},
        new float[] {0, 0, 1, 0, 0},
        new float[] {0, 0, 0, 0.5f, 0},  // 50% 투명도 적용
        new float[] {0, 0, 0, 0, 1}
    };
                ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);
                imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                // 이미지를 그릴 때, 크기 조정 및 투명도 적용
                g.DrawImage(wolfImage, new Rectangle(imgX, imgY, newWidth, newHeight), 0, 0, wolfImage.Width, wolfImage.Height, GraphicsUnit.Pixel, imageAttributes);
            }

            // 2️⃣ 게임명 텍스트
            Font titleFont = new Font("맑은 고딕", 36, FontStyle.Bold);
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("人狼ゲーム", titleFont, Brushes.BurlyWood, new RectangleF(0, 75, this.ClientSize.Width, 60), centerFormat);

            // 3️⃣ 메뉴 리스트
            Font menuFont = new Font("맑은 고딕", 20, FontStyle.Regular);
            int startY = 300;
            int spacing = 50;

            for (int i = 0; i < menuItems.Length; i++)
            {
                Brush brush = (i == hoveredIndex) ? Brushes.Goldenrod : Brushes.BurlyWood;
                g.DrawString(menuItems[i], menuFont, brush, new RectangleF(0, startY + i * spacing, this.ClientSize.Width, 40), centerFormat);
            }

            // 4️⃣ 버전 정보
            Font verFont = new Font("맑은 고딕", 8);
            g.DrawString("ver.1.0.0", verFont, Brushes.BurlyWood, this.ClientSize.Width - 70, this.ClientSize.Height - 20);
        }
    }
}

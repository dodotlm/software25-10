using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace InRang
{
    public partial class OptionPageForm : Form
    {
        // 메뉴 항목 리스트
        private string[] menuItems = { "불륨 조절", "라이센스 확인", "네트워크 확인" };
        private int hoveredIndex = -1; // 마우스가 호버 중인 인덱스

        // 전역 폰트 (Noto Sans KR Bold)
        private Font titleFont;
        private Font menuFont;
        private Font verFont;

        public OptionPageForm()
        {
            this.SuspendLayout();

            // Form 설정
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Text = "옵션";
            this.BackColor = Color.Black;
            this.ForeColor = Color.Goldenrod;
            this.DoubleBuffered = true; // 깜빡임 방지

            // 📌 글꼴 설정 (Noto Sans KR Bold)
            titleFont = new Font("Noto Sans KR", 36, FontStyle.Bold);
            menuFont = new Font("Noto Sans KR", 13, FontStyle.Bold);
            verFont = new Font("Noto Sans KR", 8, FontStyle.Bold);

            // '옵션' 라벨
            Label labelTitle = new Label();
            labelTitle.Text = "옵션";
            labelTitle.Font = new Font("Noto Sans KR", 24, FontStyle.Bold);
            labelTitle.ForeColor = Color.BurlyWood;
            labelTitle.Location = new Point(150, 50);
            labelTitle.Size = new Size(100, 40);
            this.Controls.Add(labelTitle);

            // 슬라이더 (세로 방향)
            TrackBar trackBar = new TrackBar();
            trackBar.Location = new Point(450, 100); // 위치 조정
            trackBar.Size = new Size(50, 400);       // 높이를 늘리고 너비를 줄임
            trackBar.Minimum = 0;
            trackBar.Maximum = 5;
            trackBar.TickStyle = TickStyle.None;
            trackBar.Orientation = Orientation.Vertical; // 세로 방향 설정
            this.Controls.Add(trackBar);

            // 📌 단계별 텍스트와 위치 설정
            string[] labels = { "폭력적인", "파괴적인", "생동감 넘치는", "고조되는 긴장", "고요한 상태", "음소거" };
            int baseY = 95;
            int offsetY = 75;

            for (int i = 0; i < labels.Length; i++)
            {
                Label label = new Label
                {
                    Text = labels[i],
                    Font = new Font("Noto Sans KR", 13, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(510, baseY + (offsetY * i)),
                    Size = new Size(200, 30)
                };
                this.Controls.Add(label);
            }


            // 마우스 이벤트 등록
            this.MouseMove += OptionPageForm_MouseMove;
            this.MouseClick += OptionPageForm_MouseClick;

            // 폼 종료 시 처리
            this.FormClosing += (sender, e) => { Application.Exit(); };
            this.ResumeLayout(false);
        }

        // 마우스 이동 시 호버 처리
        private void OptionPageForm_MouseMove(object sender, MouseEventArgs e)
        {
            int newHovered = GetMenuIndexAtPoint(e.Location);
            if (newHovered != hoveredIndex)
            {
                hoveredIndex = newHovered;
                this.Invalidate(); // 다시 그리기 요청
            }
        }

        // 마우스 클릭 시 메뉴 실행
        private void OptionPageForm_MouseClick(object sender, MouseEventArgs e)
        {
            int clickedIndex = GetMenuIndexAtPoint(e.Location);
            if (clickedIndex >= 0)
            {
                HandleMenuClick(menuItems[clickedIndex]);
            }
        }

        // 클릭 위치가 메뉴에 해당하는지 확인
        private int GetMenuIndexAtPoint(Point p)
        {
            int startY = 200; // 첫 번째 메뉴의 Y 좌표
            int spacing = 60; // 각 메뉴 간격
            int itemHeight = 40;

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle rect = new Rectangle(100, startY + i * spacing, 200, itemHeight);
                if (rect.Contains(p))
                    return i;
            }
            return -1;
        }

        // 메뉴 클릭 시 처리
        private void HandleMenuClick(string menu)
        {
            switch (menu)
            {
                case "불륨 조절":
                    MessageBox.Show("불륨 조절이 클릭되었습니다.");
                    break;
                case "라이센스 확인":
                    MessageBox.Show("라이센스 확인이 클릭되었습니다.");
                    break;
                case "네트워크 확인":
                    MessageBox.Show("네트워크 확인이 클릭되었습니다.");
                    break;
            }
        }

        // 메뉴 그리기
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            int startY = 200;
            int spacing = 60;
            int buttonWidth = 200;
            int buttonHeight = 40;
            int startX = 100;

            for (int i = 0; i < menuItems.Length; i++)
            {
                Rectangle buttonRect = new Rectangle(startX, startY + i * spacing, buttonWidth, buttonHeight);
                Brush buttonBrush = (i == hoveredIndex) ? Brushes.Goldenrod : Brushes.BurlyWood;

                // 버튼 배경
                g.FillRectangle(buttonBrush, buttonRect);

                // 버튼 테두리 (선택사항, 넣으면 더 깔끔함)
                g.DrawRectangle(Pens.Black, buttonRect);

                // 글자 (검정색)
                StringFormat textFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(menuItems[i], menuFont, Brushes.Black, buttonRect, textFormat);
            }
        }
    }
}

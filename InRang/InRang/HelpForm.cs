using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    public partial class HelpForm : Form
    {
        // 왼쪽 메뉴 관련
        private string[] leftMenuItems = { "게임 규칙", "승리 조건" };
        private int hoveredLeftMenuIndex = -1;
        private int selectedLeftMenuIndex = -1;

        // 직업 버튼 관련 - 9개 직업 (마을진영 5, 인랑진영 2, 제3세력 2)
        private string[] jobButtons = { "시민", "점쟁이", "영매", "사냥꾼", "네코마타", "인랑", "광인", "여우", "배덕자" };
        private int hoveredJobIndex = -1;
        private int selectedJobIndex = -1;

        private Image helpImage;
        private Image selectedJobImage;

        // 전역 폰트 (Noto Sans KR Bold)
        private Font titleFont;
        private Font verFont;
        private Font descFont;
        private Font jobButtonFont;
        private Font sectionTitleFont;
        private Font introFont;

        public HelpForm()
        {
            // 폼 기본 속성 설정
            this.Text = "도움말";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;

            // 📌 글꼴 설정 (Noto Sans KR Bold)
            titleFont = new Font("Noto Sans KR", 36, FontStyle.Bold);
            verFont = new Font("Noto Sans KR", 8, FontStyle.Bold);
            descFont = new Font("Noto Sans KR", 12, FontStyle.Regular);
            jobButtonFont = new Font("Noto Sans KR", 11, FontStyle.Bold);
            sectionTitleFont = new Font("Noto Sans KR", 16, FontStyle.Bold);
            introFont = new Font("Noto Sans KR", 14, FontStyle.Regular);

            // 📌 resources 폴더 기준으로 이미지 경로 설정
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            // 도움말 기본 이미지 로드 (예외 처리 포함)
            string helpImageFile = Path.Combine(resourcePath, "help.png");
            try { helpImage = Image.FromFile(helpImageFile); } catch { helpImage = null; }

            // 마우스 이벤트 등록
            this.MouseMove += HelpForm_MouseMove;
            this.MouseClick += HelpForm_MouseClick;
        }

        private void HelpForm_MouseMove(object sender, MouseEventArgs e)
        {
            // 왼쪽 메뉴 호버 체크
            int newLeftMenuHovered = GetLeftMenuIndexAtPoint(e.Location);
            if (newLeftMenuHovered != hoveredLeftMenuIndex)
            {
                hoveredLeftMenuIndex = newLeftMenuHovered;
                this.Invalidate();
            }

            // 오른쪽 직업 버튼 호버 체크
            int newJobHovered = GetJobButtonIndexAtPoint(e.Location);
            if (newJobHovered != hoveredJobIndex)
            {
                hoveredJobIndex = newJobHovered;
                this.Invalidate();
            }
        }

        private void HelpForm_MouseClick(object sender, MouseEventArgs e)
        {
            // 왼쪽 메뉴 클릭 처리
            int clickedLeftMenuIndex = GetLeftMenuIndexAtPoint(e.Location);
            if (clickedLeftMenuIndex >= 0)
            {
                selectedLeftMenuIndex = clickedLeftMenuIndex;
                selectedJobIndex = -1;  // 직업 선택 해제
                this.Invalidate();
            }

            // 오른쪽 직업 버튼 클릭 처리
            int clickedJobIndex = GetJobButtonIndexAtPoint(e.Location);
            if (clickedJobIndex >= 0)
            {
                // 같은 직업을 다시 클릭하면 선택 해제
                if (selectedJobIndex == clickedJobIndex)
                {
                    selectedJobIndex = -1;
                    selectedJobImage = null;
                }
                else
                {
                    selectedJobIndex = clickedJobIndex;
                    selectedLeftMenuIndex = -1;  // 왼쪽 메뉴 선택 해제
                    ShowJobDetail(jobButtons[clickedJobIndex]);
                }
                this.Invalidate();
            }

            // 뒤로 가기 버튼 영역 (왼쪽 하단)
            Rectangle backButtonRect = new Rectangle(40, this.ClientSize.Height - 60, 120, 40);
            if (backButtonRect.Contains(e.Location))
            {
                StartPageForm mainMenu = new StartPageForm();
                mainMenu.Location = this.Location;
                mainMenu.StartPosition = FormStartPosition.Manual;
                mainMenu.Show();
                this.Close();
            }
        }

        private int GetLeftMenuIndexAtPoint(Point p)
        {
            int startY = 100;
            int buttonHeight = 50;
            int buttonWidth = 120;
            int leftMargin = 40;

            for (int i = 0; i < leftMenuItems.Length; i++)
            {
                Rectangle buttonRect = new Rectangle(leftMargin, startY + i * buttonHeight, buttonWidth, buttonHeight);
                if (buttonRect.Contains(p)) return i;
            }
            return -1;
        }

        private int GetJobButtonIndexAtPoint(Point p)
        {
            int startY = 80;
            int buttonHeight = 50;
            int buttonWidth = 140;
            int rightMargin = this.ClientSize.Width - buttonWidth;
            int leftSlant = 20;

            for (int i = 0; i < jobButtons.Length; i++)
            {
                Point[] buttonPoints = new Point[]
                {
                    new Point(rightMargin + leftSlant, startY + i * buttonHeight),
                    new Point(rightMargin + buttonWidth, startY + i * buttonHeight),
                    new Point(rightMargin + buttonWidth, startY + (i + 1) * buttonHeight),
                    new Point(rightMargin, startY + (i + 1) * buttonHeight)
                };

                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddPolygon(buttonPoints);
                    if (path.IsVisible(p)) return i;
                }
            }
            return -1;
        }

        private void ShowJobDetail(string job)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            switch (job)
            {
                case "시민":
                    LoadJobImage(Path.Combine(resourcePath, "civ1.jpg"));
                    break;
                case "점쟁이":
                    LoadJobImage(Path.Combine(resourcePath, "fortuneTeller.jpg"));
                    break;
                case "영매":
                    LoadJobImage(Path.Combine(resourcePath, "medium.jpg"));
                    break;
                case "사냥꾼":
                    LoadJobImage(Path.Combine(resourcePath, "hunter.jpg"));
                    break;
                case "네코마타":
                    LoadJobImage(Path.Combine(resourcePath, "nekomata.jpg"));
                    break;
                case "인랑":
                    LoadJobImage(Path.Combine(resourcePath, "inrang.jpg"));
                    break;
                case "광인":
                    LoadJobImage(Path.Combine(resourcePath, "madman.jpg"));
                    break;
                case "여우":
                    LoadJobImage(Path.Combine(resourcePath, "fox.jpg"));
                    break;
                case "배덕자":
                    LoadJobImage(Path.Combine(resourcePath, "immoral.jpg"));
                    break;
            }
        }

        private void LoadJobImage(string imagePath)
        {
            try
            {
                selectedJobImage = Image.FromFile(imagePath);
            }
            catch
            {
                selectedJobImage = null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 1️⃣ 상단 제목
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };
            string displayTitle = "도움말";
            if (selectedJobIndex >= 0)
                displayTitle = jobButtons[selectedJobIndex];
            else if (selectedLeftMenuIndex >= 0)
                displayTitle = leftMenuItems[selectedLeftMenuIndex];

            g.DrawString(displayTitle, titleFont, Brushes.BurlyWood, new RectangleF(0, 20, this.ClientSize.Width, 60), centerFormat);

            // 2️⃣ 왼쪽 메뉴 (게임 규칙, 승리 조건)
            int leftMenuStartY = 100;
            int leftMenuButtonHeight = 50;
            int leftMenuButtonWidth = 120;
            int leftMargin = 40;

            for (int i = 0; i < leftMenuItems.Length; i++)
            {
                Rectangle buttonRect = new Rectangle(leftMargin, leftMenuStartY + i * leftMenuButtonHeight,
                                                   leftMenuButtonWidth, leftMenuButtonHeight);

                // 배경색 설정
                if (i == selectedLeftMenuIndex)
                    g.FillRectangle(new SolidBrush(Color.FromArgb(180, 218, 165, 32)), buttonRect);
                else if (i == hoveredLeftMenuIndex)
                    g.FillRectangle(new SolidBrush(Color.FromArgb(120, 218, 165, 32)), buttonRect);
                else
                    g.FillRectangle(new SolidBrush(Color.FromArgb(80, 0, 0, 0)), buttonRect);

                // 테두리
                g.DrawRectangle(new Pen(Color.FromArgb(150, 222, 184, 135), 1), buttonRect);

                // 텍스트
                StringFormat leftMenuFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                Brush textBrush = i == selectedLeftMenuIndex ? Brushes.BurlyWood : Brushes.White;
                g.DrawString(leftMenuItems[i], jobButtonFont, textBrush, buttonRect, leftMenuFormat);
            }

            // 3️⃣ 오른쪽 직업 메뉴 (사다리꼴 모양, 글씨만 황색으로 변경)
            int jobStartY = 80;
            int jobButtonHeight = 50;
            int jobButtonWidth = 140;
            int rightMargin = this.ClientSize.Width - jobButtonWidth;
            int leftSlant = 20;

            for (int i = 0; i < jobButtons.Length; i++)
            {
                Point[] buttonPoints = new Point[]
                {
                    new Point(rightMargin + leftSlant, jobStartY + i * jobButtonHeight),
                    new Point(rightMargin + jobButtonWidth, jobStartY + i * jobButtonHeight),
                    new Point(rightMargin + jobButtonWidth, jobStartY + (i + 1) * jobButtonHeight),
                    new Point(rightMargin, jobStartY + (i + 1) * jobButtonHeight)
                };

                // 배경색은 모두 반투명 검은색으로 통일
                Brush buttonBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
                if (i == hoveredJobIndex)
                    buttonBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));

                g.FillPolygon(buttonBrush, buttonPoints);

                using (Pen borderPen = new Pen(Color.FromArgb(150, 222, 184, 135), 1))
                {
                    g.DrawPolygon(borderPen, buttonPoints);
                }

                Rectangle textRect = new Rectangle(rightMargin + 20, jobStartY + i * jobButtonHeight + 15, jobButtonWidth - 25, jobButtonHeight - 20);
                StringFormat jobTextFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                // 선택된 항목만 글씨를 황색으로, 나머지는 흰색
                Brush textBrush = i == selectedJobIndex ? Brushes.BurlyWood : Brushes.White;
                g.DrawString(jobButtons[i], jobButtonFont, textBrush, textRect, jobTextFormat);
            }

            // 4️⃣ 내용 표시
            if (selectedJobIndex >= 0)
            {
                DrawJobDetail(g);
            }
            else if (selectedLeftMenuIndex >= 0)
            {
                if (selectedLeftMenuIndex == 0)
                    DrawGameRules(g);
                else if (selectedLeftMenuIndex == 1)
                    DrawWinConditions(g);
            }
            else
            {
                DrawIntroScreen(g);
            }

            // 5️⃣ 뒤로 가기 버튼 (왼쪽 하단, 황색 배경에 검은 글씨)
            Rectangle backButton = new Rectangle(40, this.ClientSize.Height - 60, 120, 40);
            g.FillRectangle(new SolidBrush(Color.BurlyWood), backButton);
            g.DrawRectangle(new Pen(Color.FromArgb(200, 139, 69, 19), 2), backButton);
            g.DrawString("뒤로 가기", jobButtonFont, Brushes.Black, backButton, centerFormat);

            // 6️⃣ 버전 정보
            g.DrawString("ver.1.0.0", verFont, Brushes.BurlyWood, this.ClientSize.Width - 70, this.ClientSize.Height - 20);
        }

        private void DrawIntroScreen(Graphics g)
        {
            int centerX = this.ClientSize.Width / 2;
            int centerY = this.ClientSize.Height / 2;
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            // 인랑 게임 소개
            string intro = "인랑은 마을에 숨어든 늑대인간을 찾아내는 추리 게임입니다.";
            Rectangle introRect = new Rectangle(centerX - 300, centerY - 80, 600, 50);
            g.DrawString(intro, introFont, Brushes.White, introRect, centerFormat);

            // 안내 문구
            string guidance = "왼쪽 메뉴에서 게임 규칙과 승리 조건을,\n오른쪽 메뉴에서 각 직업의 상세 정보를 확인하세요.";
            Rectangle guidanceRect = new Rectangle(centerX - 300, centerY - 20, 600, 80);
            g.DrawString(guidance, descFont, Brushes.BurlyWood, guidanceRect, centerFormat);
        }

        private void DrawGameRules(Graphics g)
        {
            int contentX = 200;
            int contentY = 100;
            int lineHeight = 28;
            int sectionSpacing = 40;
            int maxWidth = this.ClientSize.Width - 400; // 양쪽 메뉴 공간 고려

            // 기본 규칙
            DrawSectionHeader(g, "◆ 기본 규칙", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            string[] basicRules = {
                "• 게임은 낮과 밤을 반복합니다",
                "• 낮: 토론 후 의심스러운 플레이어를 투표로 처형",
                "• 밤: 인랑이 마을 사람을 습격, 특수 직업 능력 사용",
                "• 정체를 숨기고 추리하여 상대 진영을 제거하세요"
            };

            foreach (string rule in basicRules)
            {
                g.DrawString(rule, descFont, Brushes.White, contentX + 20, contentY);
                contentY += lineHeight;
            }

            contentY += sectionSpacing + 40; // 팁 섹션은 더 아래에 배치

            // Tip 섹션 (부가적인 느낌으로)
            Font tipHeaderFont = new Font("Noto Sans KR", 14, FontStyle.Bold);
            Brush tipBrush = new SolidBrush(Color.FromArgb(200, 218, 165, 32));

            // ! 아이콘과 Tip 텍스트
            g.DrawString("!", new Font("Noto Sans KR", 16, FontStyle.Bold), tipBrush, contentX, contentY - 2);
            g.DrawString("Tip", tipHeaderFont, tipBrush, contentX + 20, contentY);
            contentY += lineHeight + 8;

            string[] tips = {
                "• 발언과 행동을 관찰하세요",
                "• 거짓말을 할 때는 일관성을 유지하세요",
                "• 침묵도 하나의 전략입니다",
                "• 다른 플레이어의 주장을 검증하세요"
            };

            Font tipFont = new Font("Noto Sans KR", 11, FontStyle.Regular);
            foreach (string tip in tips)
            {
                g.DrawString(tip, tipFont, new SolidBrush(Color.FromArgb(220, 255, 255, 255)), contentX + 20, contentY);
                contentY += lineHeight - 3;
            }
        }

        private void DrawWinConditions(Graphics g)
        {
            int contentX = 200;
            int contentY = 100;
            int lineHeight = 28;
            int sectionSpacing = 40;
            int maxWidth = this.ClientSize.Width - 400;

            // 마을 진영
            DrawSectionHeader(g, "◆ 마을 진영 승리 조건", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;
            g.DrawString("모든 인랑과 제3세력을 제거하면 승리합니다.", descFont, Brushes.White, contentX + 20, contentY);
            contentY += lineHeight;
            g.DrawString("시민, 점쟁이, 영매, 사냥꾼, 네코마타가 마을 진영입니다.", descFont, Brushes.BurlyWood, contentX + 20, contentY);
            contentY += lineHeight + sectionSpacing;

            // 인랑 진영
            DrawSectionHeader(g, "◆ 인랑 진영 승리 조건", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;
            g.DrawString("마을 사람과 동수 이상이 되면 승리합니다.", descFont, Brushes.White, contentX + 20, contentY);
            contentY += lineHeight;
            g.DrawString("인랑, 광인이 인랑 진영입니다.", descFont, Brushes.BurlyWood, contentX + 20, contentY);
            contentY += lineHeight + sectionSpacing;

            // 제3세력
            DrawSectionHeader(g, "◆ 제3세력 승리 조건", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            DrawSubSection(g, "▶ 여우", contentX + 20, contentY);
            contentY += lineHeight;
            g.DrawString("게임 종료 시까지 생존하면 단독 승리합니다.", descFont, Brushes.White, contentX + 40, contentY);
            contentY += lineHeight + 15;

            DrawSubSection(g, "▶ 배덕자", contentX + 20, contentY);
            contentY += lineHeight;
            g.DrawString("여우가 승리하면 함께 승리합니다.", descFont, Brushes.White, contentX + 40, contentY);
        }

        private void DrawJobDetail(Graphics g)
        {
            // 직업 이미지 표시
            if (selectedJobImage != null)
            {
                int imgSize = 150;
                int imgX = 200;
                int imgY = 90;
                Rectangle imgRect = new Rectangle(imgX, imgY, imgSize, imgSize);
                g.DrawImage(selectedJobImage, imgRect);
            }

            // 직업 설명
            int contentX = 200;
            int contentY = 260;
            int lineHeight = 25;
            int sectionSpacing = 30;
            int maxWidth = this.ClientSize.Width - 400;

            string jobName = jobButtons[selectedJobIndex];

            // 직업 소개
            string intro = GetJobIntro(jobName);
            g.DrawString(intro, descFont, Brushes.White, new RectangleF(contentX, contentY, maxWidth, 50));
            contentY += 50;

            // 소속 진영
            string faction = GetJobFaction(jobName);
            DrawSectionHeader(g, "▶ 소속", contentX, contentY, new Font("Noto Sans KR", 13, FontStyle.Bold));
            g.DrawString(faction, descFont, Brushes.BurlyWood, contentX + 70, contentY);
            contentY += lineHeight + 10;

            // 능력 섹션
            DrawSectionHeader(g, "▶ 능력", contentX, contentY, new Font("Noto Sans KR", 13, FontStyle.Bold));
            contentY += lineHeight;
            string ability = GetJobAbility(jobName);
            g.DrawString(ability, descFont, Brushes.White, new RectangleF(contentX + 20, contentY, maxWidth - 20, 100));
            contentY += GetLineCount(ability) * lineHeight + sectionSpacing;

            // 전략 섹션
            DrawSectionHeader(g, "▶ 전략", contentX, contentY, new Font("Noto Sans KR", 13, FontStyle.Bold));
            contentY += lineHeight;
            DrawStrategyPoints(g, GetJobStrategy(jobName), contentX + 20, contentY);
        }

        private void DrawSectionHeader(Graphics g, string header, int x, int y, Font font)
        {
            g.DrawString(header, font, Brushes.BurlyWood, x, y);
        }

        private void DrawSubSection(Graphics g, string header, int x, int y)
        {
            g.DrawString(header, new Font("Noto Sans KR", 13, FontStyle.Bold), Brushes.BurlyWood, x, y);
        }

        private void DrawStrategyPoints(Graphics g, string[] strategies, int x, int y)
        {
            int lineHeight = 25;
            for (int i = 0; i < strategies.Length; i++)
            {
                g.DrawString(strategies[i], descFont, Brushes.White, x, y + i * lineHeight);
            }
        }

        private int GetLineCount(string text)
        {
            return text.Split('\n').Length;
        }

        private string GetJobFaction(string jobName)
        {
            switch (jobName)
            {
                case "시민":
                case "점쟁이":
                case "영매":
                case "사냥꾼":
                case "네코마타":
                    return "마을 진영";
                case "인랑":
                case "광인":
                    return "인랑 진영";
                case "여우":
                case "배덕자":
                    return "제3세력";
                default:
                    return "";
            }
        }

        private string GetJobIntro(string jobName)
        {
            switch (jobName)
            {
                case "시민":
                    return "시민은 마을의 평범한 주민입니다. 특수 능력은 없지만 추리와 토론으로 마을을 지킵니다.";
                case "점쟁이":
                    return "점쟁이는 신비한 힘으로 정체를 알아내는 마을의 현자입니다.";
                case "영매":
                    return "영매는 죽은 자의 영혼과 소통하는 능력자입니다.";
                case "사냥꾼":
                    return "사냥꾼은 마을을 지키는 수호자입니다.";
                case "네코마타":
                    return "네코마타는 복수의 힘을 가진 고양이 요괴입니다.";
                case "인랑":
                    return "인랑은 마을에 숨어든 무서운 늑대입니다. 밤마다 마을 사람을 습격합니다.";
                case "광인":
                    return "광인은 인랑을 숭배하는 미친 인간입니다. 인랑 진영의 승리를 돕습니다.";
                case "여우":
                    return "여우는 교활하고 영리한 제3세력입니다. 끝까지 살아남는 것이 목표입니다.";
                case "배덕자":
                    return "배덕자는 여우를 따르는 타락한 인간입니다. 여우의 승리가 곧 자신의 승리입니다.";
                default:
                    return "";
            }
        }

        private string GetJobAbility(string jobName)
        {
            switch (jobName)
            {
                case "시민":
                    return "특수 능력은 없지만, 추리와 투표로 마을을 지킵니다.";
                case "점쟁이":
                    return "매일 밤 한 명을 지목하여 그 사람이 인랑인지 아닌지 확인할 수 있습니다.";
                case "영매":
                    return "처형된 사람의 정체를 확인할 수 있습니다.";
                case "사냥꾼":
                    return "매일 밤 한 명을 선택하여 인랑의 습격으로부터 보호할 수 있습니다.\n단, 자기 자신은 보호할 수 없습니다.";
                case "네코마타":
                    return "인랑에게 습격당하면 습격한 인랑도 같이 죽습니다.\n처형당하면 랜덤으로 누군가와 같이 죽습니다.";
                case "인랑":
                    return "매일 밤 한 명을 습격하여 제거할 수 있습니다.\n다른 인랑을 알아볼 수 있습니다.";
                case "광인":
                    return "특수 능력은 없지만, 점쟁이에게는 시민으로 보입니다.";
                case "여우":
                    return "특수 능력은 없지만, 게임 종료 시 생존하면 단독 승리합니다.\n점쟁이에게 점 대상이 되면 사망합니다.";
                case "배덕자":
                    return "게임 시작 시 여우가 누구인지 알 수 있습니다.\n여우가 승리하면 함께 승리합니다.";
                default:
                    return "";
            }
        }

        private string[] GetJobStrategy(string jobName)
        {
            switch (jobName)
            {
                case "시민":
                    return new string[] {
                        "• 적극적인 토론 참여",
                        "• 다른 플레이어의 행동 관찰",
                        "• 논리적인 추리로 인랑 찾기"
                    };
                case "점쟁이":
                    return new string[] {
                        "• 의심스러운 사람 우선 조사",
                        "• 조사 결과를 신중하게 공개",
                        "• 가짜 점쟁이 주의"
                    };
                case "영매":
                    return new string[] {
                        "• 처형된 사람의 정체 공개 시기 조절",
                        "• 가짜 영매 주의",
                        "• 정보를 적절히 활용"
                    };
                case "사냥꾼":
                    return new string[] {
                        "• 중요한 역할 보호 우선",
                        "• 보호 패턴 숨기기",
                        "• 자신을 보호할 수 없음 주의"
                    };
                case "네코마타":
                    return new string[] {
                        "• 인랑의 습격을 유도하여 복수",
                        "• 처형 시 영향력 있는 플레이어 도련",
                        "• 정체를 적절히 숨기기"
                    };
                case "인랑":
                    return new string[] {
                        "• 낮에는 시민인 척 연기",
                        "• 특수 직업 우선 제거",
                        "• 동료와 은밀한 협력"
                    };
                case "광인":
                    return new string[] {
                        "• 인랑 편에서 활동",
                        "• 가짜 점쟁이 연기",
                        "• 혼란 조성으로 인랑 돕기"
                    };
                case "여우":
                    return new string[] {
                        "• 양 진영 사이에서 균형 유지",
                        "• 점쟁이를 피하며 생존",
                        "• 의심받지 않도록 조심"
                    };
                case "배덕자":
                    return new string[] {
                        "• 여우를 은밀히 보호",
                        "• 여우의 정체 숨기기",
                        "• 양 진영 사이에서 균형 유지"
                    };
                default:
                    return new string[0];
            }
        }
    }
}
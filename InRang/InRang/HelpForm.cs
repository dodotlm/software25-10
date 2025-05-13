using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace InRang
{
    public partial class HelpForm : Form
    {
        private string[] helpMenuItems = { "게임 규칙", "직업 설명", "인터페이스", "뒤로 가기" };
        private int hoveredIndex = -1;

        // 직업 버튼 관련 - 9개 직업 (마을진영 5, 인랑진영 2, 제3세력 2)
        private string[] jobButtons = { "시민", "점쟁이", "영매", "사냥꾼", "네코마타", "인랑", "광인", "여우", "배덕자" };
        private int hoveredJobIndex = -1;
        private int selectedJobIndex = -1;

        private Image helpImage;
        private Image selectedJobImage;

        // 전역 폰트 (Noto Sans KR Bold)
        private Font titleFont;
        private Font menuFont;
        private Font verFont;
        private Font descFont;
        private Font jobButtonFont;
        private Font sectionTitleFont;

        // 현재 표시 중인 도움말 내용
        private string currentHelpContent = "";
        private string currentHelpTitle = "";
        private bool showJobMenu = false;

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
            menuFont = new Font("Noto Sans KR", 16, FontStyle.Bold);
            verFont = new Font("Noto Sans KR", 8, FontStyle.Bold);
            descFont = new Font("Noto Sans KR", 12, FontStyle.Regular);
            jobButtonFont = new Font("Noto Sans KR", 11, FontStyle.Bold);
            sectionTitleFont = new Font("Noto Sans KR", 16, FontStyle.Bold);

            // 📌 resources 폴더 기준으로 이미지 경로 설정
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            // 도움말 기본 이미지 로드 (예외 처리 포함)
            string helpImageFile = Path.Combine(resourcePath, "help.png");
            try { helpImage = Image.FromFile(helpImageFile); } catch { helpImage = null; }

            // 마우스 이벤트 등록
            this.MouseMove += HelpForm_MouseMove;
            this.MouseClick += HelpForm_MouseClick;

            // 기본 도움말 내용 설정
            ShowGeneralHelp();
        }

        private void HelpForm_MouseMove(object sender, MouseEventArgs e)
        {
            int newHovered = GetMenuIndexAtPoint(e.Location);
            int newJobHovered = showJobMenu ? GetJobButtonIndexAtPoint(e.Location) : -1;

            if (newHovered != hoveredIndex || newJobHovered != hoveredJobIndex)
            {
                hoveredIndex = newHovered;
                hoveredJobIndex = newJobHovered;
                this.Invalidate();
            }
        }

        private void HelpForm_MouseClick(object sender, MouseEventArgs e)
        {
            int clickedIndex = GetMenuIndexAtPoint(e.Location);
            if (clickedIndex >= 0)
            {
                HandleMenuClick(helpMenuItems[clickedIndex]);
            }

            if (showJobMenu)
            {
                int clickedJobIndex = GetJobButtonIndexAtPoint(e.Location);
                if (clickedJobIndex >= 0)
                {
                    selectedJobIndex = clickedJobIndex;
                    ShowJobDetail(jobButtons[clickedJobIndex]);
                }
            }
        }

        private int GetMenuIndexAtPoint(Point p)
        {
            // 왼쪽 메뉴 영역 - 오른쪽만 사선
            int startY = 120;
            int spacing = 55;
            int menuWidth = 180;
            int menuHeight = 40;
            int leftMargin = 0;
            int rightSlant = 30;

            for (int i = 0; i < helpMenuItems.Length; i++)
            {
                Point[] menuPoints = new Point[]
                {
                    new Point(leftMargin, startY + i * spacing),
                    new Point(leftMargin + menuWidth, startY + i * spacing),
                    new Point(leftMargin + menuWidth - rightSlant, startY + i * spacing + menuHeight),
                    new Point(leftMargin, startY + i * spacing + menuHeight)
                };

                using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddPolygon(menuPoints);
                    if (path.IsVisible(p)) return i;
                }
            }
            return -1;
        }

        private int GetJobButtonIndexAtPoint(Point p)
        {
            int startY = 120;
            int spacing = 40;
            int buttonWidth = 120;
            int buttonHeight = 30;
            int rightMargin = this.ClientSize.Width - buttonWidth - 30;

            for (int i = 0; i < jobButtons.Length; i++)
            {
                Rectangle rect = new Rectangle(rightMargin, startY + i * spacing, buttonWidth, buttonHeight);
                if (rect.Contains(p)) return i;
            }
            return -1;
        }

        private void HandleMenuClick(string menu)
        {
            showJobMenu = false;
            selectedJobIndex = -1;

            switch (menu)
            {
                case "게임 규칙":
                    ShowGameRules();
                    break;
                case "직업 설명":
                    ShowJobDescriptions();
                    showJobMenu = true;
                    break;
                case "인터페이스":
                    ShowInterfaceHelp();
                    break;
                case "뒤로 가기":
                    StartPageForm mainMenu = new StartPageForm();
                    mainMenu.Show();
                    this.Close();
                    break;
            }
        }

        private void ShowGeneralHelp()
        {
            currentHelpTitle = "";  // 제목 표시하지 않음
            currentHelpContent = "";  // DrawGeneralHelp에서 직접 그리기
            this.Invalidate();
        }

        private void ShowGameRules()
        {
            currentHelpTitle = "게임 규칙";
            currentHelpContent = "";
            this.Invalidate();
        }

        private void ShowJobDescriptions()
        {
            currentHelpTitle = "직업 설명";
            currentHelpContent = "";
            this.Invalidate();
        }

        private void ShowInterfaceHelp()
        {
            currentHelpTitle = "인터페이스";
            currentHelpContent = "";
            this.Invalidate();
        }

        private void ShowJobDetail(string job)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            currentHelpTitle = job;
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
                case "의사":
                    LoadJobImage(Path.Combine(resourcePath, "doctor.jpg"));
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
            this.Invalidate();
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
            string displayTitle = currentHelpTitle;
            if (showJobMenu && selectedJobIndex == -1) displayTitle = "직업 설명";
            else if (showJobMenu && selectedJobIndex >= 0) displayTitle = "";
            else if (string.IsNullOrEmpty(currentHelpTitle)) displayTitle = "";  // 일반 도움말일 때 제목 없음

            if (!string.IsNullOrEmpty(displayTitle))
            {
                g.DrawString(displayTitle, titleFont, Brushes.BurlyWood, new RectangleF(0, 20, this.ClientSize.Width, 60), centerFormat);
            }

            // 2️⃣ 왼쪽 메뉴 리스트
            int startY = 120;
            int spacing = 55;
            int menuWidth = 180;
            int menuHeight = 40;
            int leftMargin = 0;
            int rightSlant = 30;

            for (int i = 0; i < helpMenuItems.Length; i++)
            {
                Point[] menuPoints = new Point[]
                {
                    new Point(leftMargin, startY + i * spacing),
                    new Point(leftMargin + menuWidth, startY + i * spacing),
                    new Point(leftMargin + menuWidth - rightSlant, startY + i * spacing + menuHeight),
                    new Point(leftMargin, startY + i * spacing + menuHeight)
                };

                bool isCurrentMenu = false;
                if (helpMenuItems[i] == "게임 규칙" && currentHelpTitle == "게임 규칙") isCurrentMenu = true;
                if (helpMenuItems[i] == "직업 설명" && showJobMenu) isCurrentMenu = true;
                if (helpMenuItems[i] == "인터페이스" && currentHelpTitle == "인터페이스") isCurrentMenu = true;

                Brush menuBrush;
                if (i == hoveredIndex)
                    menuBrush = new SolidBrush(Color.FromArgb(120, 218, 165, 32));
                else
                    menuBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));

                g.FillPolygon(menuBrush, menuPoints);

                using (Pen borderPen = new Pen(Color.FromArgb(150, 222, 184, 135), 1))
                {
                    g.DrawPolygon(borderPen, menuPoints);
                }

                Rectangle textRect = new Rectangle(leftMargin + 20, startY + i * spacing + 5, menuWidth - 40, menuHeight - 10);
                StringFormat leftFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                Brush textBrush = isCurrentMenu ? Brushes.BurlyWood : Brushes.White;

                g.DrawString(helpMenuItems[i], menuFont, textBrush, textRect, leftFormat);
            }

            // 3️⃣ 직업 메뉴가 활성화된 경우 오른쪽에 직업 버튼 표시
            if (showJobMenu)
            {
                int jobStartY = 120;
                int jobSpacing = 40;
                int jobButtonWidth = 120;
                int jobButtonHeight = 30;
                int rightMargin = this.ClientSize.Width - jobButtonWidth - 30;

                for (int i = 0; i < jobButtons.Length; i++)
                {
                    Rectangle jobButtonRect = new Rectangle(rightMargin, jobStartY + i * jobSpacing, jobButtonWidth, jobButtonHeight);

                    Brush jobButtonBrush;
                    if (i == selectedJobIndex)
                        jobButtonBrush = Brushes.DarkGoldenrod;
                    else if (i == hoveredJobIndex)
                        jobButtonBrush = Brushes.Goldenrod;
                    else
                        jobButtonBrush = Brushes.BurlyWood;

                    g.FillRectangle(jobButtonBrush, jobButtonRect);
                    g.DrawRectangle(Pens.Black, jobButtonRect);

                    StringFormat jobTextFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(jobButtons[i], jobButtonFont, Brushes.Black, jobButtonRect, jobTextFormat);
                }
            }

            // 4️⃣ 이미지와 내용 표시
            if (showJobMenu && selectedJobIndex >= 0)
            {
                DrawJobDetailNew(g);
            }
            else if (currentHelpTitle == "게임 규칙")
            {
                DrawGameRules(g);
            }
            else if (currentHelpTitle == "인터페이스")
            {
                DrawInterface(g);
            }
            else if (showJobMenu)
            {
                DrawJobList(g);
            }
            else
            {
                DrawGeneralHelp(g);
            }

            // 5️⃣ 버전 정보
            g.DrawString("ver.1.0.0", verFont, Brushes.BurlyWood, this.ClientSize.Width - 70, this.ClientSize.Height - 20);
        }

        private void DrawGameRules(Graphics g)
        {
            int contentX = 250;
            int contentY = 120;
            int lineHeight = 28;
            int sectionSpacing = 40;

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

            contentY += sectionSpacing;

            // 승리 조건
            DrawSectionHeader(g, "◆ 승리 조건", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            DrawSubSection(g, "▶ 마을 진영", contentX + 20, contentY);
            contentY += lineHeight;
            g.DrawString("모든 인랑과 제3세력을 제거", descFont, Brushes.White, contentX + 40, contentY);
            contentY += lineHeight + 10;

            DrawSubSection(g, "▶ 인랑 진영", contentX + 20, contentY);
            contentY += lineHeight;
            g.DrawString("마을 사람과 동수 이상이 되기", descFont, Brushes.White, contentX + 40, contentY);
            contentY += lineHeight + 10;

            DrawSubSection(g, "▶ 제3세력", contentX + 20, contentY);
            contentY += lineHeight;
            g.DrawString("각 직업별 특수 승리 조건 달성", descFont, Brushes.White, contentX + 40, contentY);
        }

        private void DrawInterface(Graphics g)
        {
            int contentX = 250;
            int contentY = 120;
            int lineHeight = 28;
            int sectionSpacing = 40;

            // 게임 화면 구성
            DrawSectionHeader(g, "◆ 게임 화면 구성", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            string[] screenLayout = {
                "• 상단: 현재 페이즈(낮/밤) 및 시간 표시",
                "• 중앙: 채팅창 및 게임 진행 상황",
                "• 하단: 플레이어 리스트 및 투표 버튼",
                "• 우측: 개인 메모 및 능력 사용 버튼"
            };

            foreach (string layout in screenLayout)
            {
                g.DrawString(layout, descFont, Brushes.White, contentX + 20, contentY);
                contentY += lineHeight;
            }

            contentY += sectionSpacing;

            // 단축키
            DrawSectionHeader(g, "◆ 단축키", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            string[] shortcuts = {
                "• Enter: 채팅 입력",
                "• Tab: 플레이어 순환 선택",
                "• Space: 투표/능력 확정",
                "• Esc: 메뉴 열기",
                "• F1: 도움말 표시"
            };

            foreach (string shortcut in shortcuts)
            {
                g.DrawString(shortcut, descFont, Brushes.White, contentX + 20, contentY);
                contentY += lineHeight;
            }
        }

        private void DrawJobList(Graphics g)
        {
            int contentX = 250;
            int contentY = 120;
            int lineHeight = 28;
            int sectionSpacing = 40;

            // 마을 진영 (5개)
            DrawSectionHeader(g, "◆ 마을 진영", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            string[] townJobs = {
                "• 시민: 특수 능력 없음",
                "• 점쟁이: 한 명이 인랑인지 확인",
                "• 영매: 처형된 사람의 정체 확인",
                "• 사냥꾼: 밤에 한 명을 보호",
                "• 네코마타: 습격/처형 시 복수"
            };

            foreach (string job in townJobs)
            {
                g.DrawString(job, descFont, Brushes.White, contentX + 20, contentY);
                contentY += lineHeight;
            }

            contentY += sectionSpacing;

            // 인랑 진영 (2개)
            DrawSectionHeader(g, "◆ 인랑 진영", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            string[] wolfJobs = {
                "• 인랑: 매일 밤 한 명을 습격",
                "• 광인: 마을측 판정, 인랑측 편"
            };

            foreach (string job in wolfJobs)
            {
                g.DrawString(job, descFont, Brushes.White, contentX + 20, contentY);
                contentY += lineHeight;
            }

            contentY += sectionSpacing;

            // 제3세력 (2개)
            DrawSectionHeader(g, "◆ 제3세력", contentX, contentY, sectionTitleFont);
            contentY += lineHeight + 10;

            string[] neutralJobs = {
                "• 여우: 끝까지 생존 시 단독 승리",
                "• 배덕자: 여우가 생존하면 승리"
            };

            foreach (string job in neutralJobs)
            {
                g.DrawString(job, descFont, Brushes.White, contentX + 20, contentY);
                contentY += lineHeight;
            }
        }

        private void DrawGeneralHelp(Graphics g)
        {
            // 세련된 도움말 초기 화면
            int contentX = 250;
            int contentWidth = this.ClientSize.Width - 260;
            int centerX = contentX + contentWidth / 2;

            // 중앙 정렬용 StringFormat
            StringFormat centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // 환영 메시지
            int welcomeY = 180;
            Font welcomeFont = new Font("Noto Sans KR", 16, FontStyle.Regular);
            string welcomeText = "人狼ゲーム에 오신 것을 환영합니다!";
            g.DrawString(welcomeText, welcomeFont, Brushes.BurlyWood, new RectangleF(contentX, welcomeY, contentWidth, 30), centerFormat);

            // 게임 설명
            int descY = welcomeY + 60;
            Font descFont = new Font("Noto Sans KR", 13, FontStyle.Regular);
            string[] descriptions = {
                "이 게임은 마을 사람과 인랑의 숨막히는 심리전을 그린 추리 게임입니다.",
                "낮과 밤을 반복하며 서로의 정체를 숨기고 추리해야 합니다.",
                "당신은 마을을 지킬 것인가, 아니면 파괴할 것인가?"
            };

            for (int i = 0; i < descriptions.Length; i++)
            {
                g.DrawString(descriptions[i], descFont, Brushes.White, new RectangleF(contentX, descY + i * 35, contentWidth, 30), centerFormat);
            }

            // 하단 안내
            int guideY = descY + 150;
            Font guideFont = new Font("Noto Sans KR", 14, FontStyle.Bold);
            string guideText = "왼쪽 메뉴에서 원하는 도움말 항목을 선택하세요";
            g.DrawString(guideText, guideFont, Brushes.BurlyWood, new RectangleF(contentX, guideY, contentWidth, 30), centerFormat);
        }

        private void DrawJobDetailNew(Graphics g)
        {
            // 이미지 먼저 표시
            if (selectedJobImage != null)
            {
                int imgSize = 150;
                int imgX = 280;  // 왼쪽으로 이동
                int imgY = 90;
                Rectangle imgRect = new Rectangle(imgX, imgY, imgSize, imgSize);
                g.DrawImage(selectedJobImage, imgRect);
            }

            // 내용은 이미지 아래에 표시
            int contentX = 220;  // 250에서 220으로 변경
            int contentY = 260;
            int lineHeight = 25;
            int sectionSpacing = 30;
            int maxWidth = 380;  // 최대 너비 제한

            // 직업 소개
            string jobName = currentHelpTitle;
            string intro = GetJobIntro(jobName);

            string[] introParts = intro.Split(new[] { jobName }, StringSplitOptions.None);
            int currentX = contentX;

            for (int i = 0; i < introParts.Length; i++)
            {
                g.DrawString(introParts[i], descFont, Brushes.White, currentX, contentY);
                currentX += (int)g.MeasureString(introParts[i], descFont).Width;

                if (i < introParts.Length - 1)
                {
                    g.DrawString(jobName, new Font("Noto Sans KR", 12, FontStyle.Bold), Brushes.BurlyWood, currentX, contentY);
                    currentX += (int)g.MeasureString(jobName, new Font("Noto Sans KR", 12, FontStyle.Bold)).Width;
                }
            }

            contentY += sectionSpacing;

            // 능력 섹션
            DrawSectionHeader(g, "▶ 능력", contentX, contentY, new Font("Noto Sans KR", 13, FontStyle.Bold));
            contentY += lineHeight;
            DrawSectionContent(g, GetJobAbility(jobName), contentX + 20, contentY, maxWidth);
            contentY += GetLineCount(GetJobAbility(jobName)) * lineHeight + sectionSpacing;

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

        private void DrawSectionContent(Graphics g, string content, int x, int y, int maxWidth = 400)
        {
            g.DrawString(content, descFont, Brushes.White, new Rectangle(x, y, maxWidth, 100));
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

        private string GetJobIntro(string jobName)
        {
            switch (jobName)
            {
                case "시민":
                    return "시민은 마을의 평범한 주민입니다.";
                case "점쟁이":
                    return "점쟁이는 신비한 힘으로 정체를 알아내는 마을의 현자입니다.";
                case "영매":
                    return "영매는 죽은 자의 영혼과 소통하는 능력자입니다.";
                case "사냥꾼":
                    return "사냥꾼은 마을을 지키는 수호자입니다.";
                case "네코마타":
                    return "네코마타는 복수의 힘을 가진 고양이 요괴입니다.";
                case "인랑":
                    return "인랑은 마을에 숨어든 무서운 늑대입니다.";
                case "광인":
                    return "광인은 인랑을 숭배하는 미친 인간입니다.";
                case "여우":
                    return "여우는 교활하고 영리한 제3세력입니다.";
                case "배덕자":
                    return "배덕자는 여우를 따르는 타락한 인간입니다.";
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
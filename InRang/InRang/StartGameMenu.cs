﻿using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace InRang
{
    public partial class StartGameMenu : Form
    {
        private TcpClient client;   // 서버 클라이언트
        public static StreamWriter Writer;
        public static StreamReader Reader;

        private string[] menuItems = { "싱글 플레이", "멀티 플레이", "방 생성 설정", "뒤로 가기" };
        private int hoveredIndex = -1;

        // 명시적으로 System.Drawing.Image 사용
        private System.Drawing.Image leftCharacter;
        private System.Drawing.Image rightCharacter;

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
            try { leftCharacter = System.Drawing.Image.FromFile(leftImageFile); } catch { leftCharacter = null; }
            try { rightCharacter = System.Drawing.Image.FromFile(rightImageFile); } catch { rightCharacter = null; }

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

                    // 싱글 플레이 모드 시작 - SinglePlayGameForm으로 이동
                    StartSinglePlayerMode();
                    break;
                case "멀티 플레이":
                    try
                    {
                        // 서버 생성
                        try
                        {
                            Server server = new Server();
                            server.Start(9000);  // 포트 9000에서 시작 시도
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                            {
                                MessageBox.Show("서버가 이미 실행 중입니다. 실행 중인 서버에 연결합니다.");
                                // 필요하면 클라이언트로서 해당 서버에 연결할 수 있음
                            }
                            else
                            {
                                MessageBox.Show("서버 시작 실패: " + ex.Message);
                            }
                        }

                        // 서버에 연결 시도
                        client = new TcpClient();
                        client.Connect(GameSettings.ServerIP, 9000); // 서버 IP 및 포트

                        MessageBox.Show("서버에 연결되었습니다.");

                        // 연결 성공 시 다음 폼으로 이동
                        this.Hide();
                        MultiPlayForm multiPlayForm = new MultiPlayForm(client);
                        multiPlayForm.StartPosition = FormStartPosition.Manual;
                        multiPlayForm.Location = this.Location;
                        multiPlayForm.Show();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("서버 연결 실패: " + ex.Message);
                    }
                    break;
                case "방 생성 설정":
                    // StartGameMenu 폼 열기
                    RoomSettingForm roomSet = new RoomSettingForm();
                    roomSet.StartPosition = FormStartPosition.Manual;
                    roomSet.Location = this.Location;
                    roomSet.Show();
                    // 현재 StartPageForm 닫기 (필요시)
                    this.Hide();  // 창 닫지 말고 숨김 (뒤로 가기 시 다시 보이게 가능)
                    break;
                case "뒤로 가기":
                    StartPageForm startPageForm = new StartPageForm();
                    startPageForm.StartPosition = FormStartPosition.Manual;
                    startPageForm.Location = this.Location;
                    startPageForm.Show();
                    this.Close();
                    break;
            }
        }

        /// <summary>
        /// 싱글 플레이 모드 시작 - SinglePlayGameForm으로 전환
        /// </summary>
        private void StartSinglePlayerMode()
        {
            try
            {
                // SinglePlayGameForm 생성 (기존 JobAssignmentForm 대신)
                InRang.SinglePlayGameForm gameForm = new InRang.SinglePlayGameForm(
                    GameSettings.PlayerCount,
                    GameSettings.AICount,
                    GameSettings.YaminabeMode,
                    GameSettings.QuantumMode);
                gameForm.StartPosition = FormStartPosition.Manual;
                gameForm.Location = this.Location;
                // 현재 폼 숨기기
                this.Hide();

                // SinglePlayGameForm 표시(모달 대신 일반 표시로 변경)
                gameForm.FormClosed += (s, e) =>
                {
                    // 게임 종료 후 다시 이 화면 표시
                    this.Show();
                };
                gameForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"싱글 플레이 모드 시작 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
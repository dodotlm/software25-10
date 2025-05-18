using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace InRang
{
    public partial class MultiPlayForm : Form
    {
        // 일단 방 생성 시 List에 추가되도록 해놓았음
        private List<string> roomTitleList = new List<string> { "예시 게임방" };
        private List<Button> roomButtons = new List<Button>(); // 생성된 버튼들을 담는 리스트
        private Button selectedRoom = null;


        private string[] menuItems = { "싱글 플레이", "멀티 플레이" };

        // 패널 정의
        private Panel mainMenuPanel;
        private Panel roomCreatePanel;
        private Panel roomJoinPanel;

        private Image leftCharacter;
        private Image rightCharacter;

        private int hoveredIndex = -1;

        private string mainTitle = "멀티 플레이"; // 기본 제목

        // 전역 폰트 (Noto Sans KR Bold)
        private Font titleFont;
        private Font menuFont;
        private Font verFont;
        private Font contentFont;

        private Font buttonFont;

        public MultiPlayForm()
        {
            // 폼 기본 속성 설정
            this.Text = "멀티 플레이";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;

            // 📌 글꼴 설정 (Noto Sans KR Bold)
            titleFont = new Font("Noto Sans KR", 36, FontStyle.Bold);
            menuFont = new Font("Noto Sans KR", 13, FontStyle.Bold);
            verFont = new Font("Noto Sans KR", 8, FontStyle.Bold);
            buttonFont = new Font("Noto Sans KR", 11, FontStyle.Bold);
            contentFont = new Font("Noto Sans KR", 11, FontStyle.Bold);


            // 📌 resources 폴더 기준으로 이미지 경로 설정
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\"));
            string resourcePath = Path.Combine(projectRoot, "resources");

            string leftImageFile = Path.Combine(resourcePath, "civ1.jpg");
            string rightImageFile = Path.Combine(resourcePath, "civ2.jpg");

            // 이미지 로드 (예외 처리 포함)
            try { leftCharacter = Image.FromFile(leftImageFile); } catch { leftCharacter = null; }
            try { rightCharacter = Image.FromFile(rightImageFile); } catch { rightCharacter = null; }

            // 패널 생성
            InitializeMainMenuPanel();
            InitializeRoomCreatePanel();
            InitializeRoomJoinPanel();

            // 메인 메뉴 패널만 보이기
            roomCreatePanel.Visible = false;
            roomJoinPanel.Visible = false;

            this.Controls.Add(mainMenuPanel);
            this.Controls.Add(roomCreatePanel);
            this.Controls.Add(roomJoinPanel);
        }

        private void InitializeMainMenuPanel()
        {
            mainMenuPanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };

            // 방 생성 버튼
            Button createRoomButton = new Button
            {
                Text = "방 생성",
                Font = menuFont,
                Size = new Size(200, 100),
                Location = new Point(150, 300),
                BackColor = Color.Tan,
                FlatStyle = FlatStyle.Flat

            };
            createRoomButton.FlatAppearance.BorderColor = Color.BurlyWood;
            createRoomButton.FlatAppearance.BorderSize = 10; // 테두리 두께
            createRoomButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            createRoomButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;


            createRoomButton.Click += (s, e) =>
            {
                mainTitle = "방 생성하기";
                Invalidate();                // 화면 새로고침
                mainMenuPanel.Visible = false;
                roomCreatePanel.Visible = true;
            };

            // 방 참가 버튼
            Button joinRoomButton = new Button
            {
                Text = "방 참가",
                Font = menuFont,
                Size = new Size(200, 100),
                Location = new Point(450, 300),
                BackColor = Color.Tan,
                FlatStyle = FlatStyle.Flat
            };
            joinRoomButton.FlatAppearance.BorderColor = Color.BurlyWood;
            joinRoomButton.FlatAppearance.BorderSize = 10; // 테두리 두께
            joinRoomButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            joinRoomButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            joinRoomButton.Click += (s, e) =>
            {
                mainTitle = "참가 하기";      // 제목 변경
                Invalidate();                // 화면 새로고침
                mainMenuPanel.Visible = false;
                roomJoinPanel.Visible = true;
            };


            // 뒤로가기 버튼
            Button exitButton = new Button
            {
                Text = "뒤로 가기",
                Font = menuFont,
                Size = new Size(250, 40),
                Location = new Point(275, 450),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat
            };
            exitButton.FlatAppearance.BorderColor = Color.Black;
            exitButton.FlatAppearance.BorderSize = 1; // 테두리 두께
            exitButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            exitButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            exitButton.Click += (s, e) =>
            {
                StartGameMenu startGameMenu = new StartGameMenu();
                startGameMenu.Show();
                this.Close();
            };

            mainMenuPanel.Controls.Add(exitButton);
            mainMenuPanel.Controls.Add(createRoomButton);
            mainMenuPanel.Controls.Add(joinRoomButton);
        }

        private void InitializeRoomCreatePanel()
        {
            roomCreatePanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };


            // 레이블 생성
            Label roomTitleLabel = new Label
            {
                Text = "방 제목 입력",
                Location = new Point(350, 260), // 패널 내 위치
                Font = menuFont,
                AutoSize = true,
                ForeColor = Color.BurlyWood,
            };

            // 텍스트 박스 생성
            TextBox roomTitleTextBox = new TextBox
            {
                Size = new Size(200, 30),
                Location = new Point(300, 300),  // 패널 내 위치
                Font = contentFont,
                Text = ""
            };

            Button exitRoomCreateButton = new Button
            {
                Text = "뒤로 가기",
                Font = buttonFont,
                Size = new Size(150, 40),
                Location = new Point(150, 450),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat // Flat 스타일로 설정
            };
            // 테두리 색상 설정
            exitRoomCreateButton.FlatAppearance.BorderColor = Color.Black;
            exitRoomCreateButton.FlatAppearance.BorderSize = 1; // 테두리 두께
            exitRoomCreateButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            exitRoomCreateButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            exitRoomCreateButton.Click += (s, e) =>
            {
                roomCreatePanel.Visible = false;
                mainMenuPanel.Visible = true;
            };

            Button roomCreateButton = new Button
            {
                Text = "생성 하기",
                Font = buttonFont,
                Size = new Size(150, 40),
                Location = new Point(525, 450),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat // Flat 스타일로 설정
            };
            // 테두리 색상 설정
            roomCreateButton.FlatAppearance.BorderColor = Color.Black;
            roomCreateButton.FlatAppearance.BorderSize = 1; // 테두리 두께
            roomCreateButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            roomCreateButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            roomCreateButton.Click += (s, e) =>
            {
                if (roomTitleTextBox.Text == "") { MessageBox.Show("방 제목을 입력해주세요"); }
                else
                {
                    roomTitleList.Add(roomTitleTextBox.Text);
                    roomTitleTextBox.Text = "";
                    MessageBox.Show("방 생성!");
                    GenerateRoomButtons();
                }
            };

            roomCreatePanel.Controls.Add(roomCreateButton);
            roomCreatePanel.Controls.Add(exitRoomCreateButton);
            roomCreatePanel.Controls.Add(roomTitleTextBox);
            roomCreatePanel.Controls.Add(roomTitleLabel);


        }



        private void InitializeRoomJoinPanel()
        {
            roomJoinPanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            };

            // 📌 Paint 이벤트 등록
            roomJoinPanel.Paint += RoomJoinPanel_Paint;

            // ➡ 초기 Room 리스트의 버튼 생성
            GenerateRoomButtons();

            Button joinButton = new Button
            {
                Text = "참가하기",
                Font = buttonFont,
                Size = new Size(100, 40),
                Location = new Point(125, 500),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat // Flat 스타일로 설정
            };
            // 테두리 색상 설정
            joinButton.FlatAppearance.BorderColor = Color.Black;
            joinButton.FlatAppearance.BorderSize = 1; // 테두리 두께
            joinButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            joinButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            joinButton.Click += (s, e) =>
            {
                if (selectedRoom == null)
                {
                    MessageBox.Show("참가할 방을 선택해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // "예", "아니오" 메시지 박스 표시
                DialogResult result = MessageBox.Show($"{selectedRoom.Text} 방에 참가하시겠습니까?", "방 참가 확인",
                                                      MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // 여기에서 실제 참가 로직을 추가하면 됩니다.
                    WaitingRoom waitingRoom = new WaitingRoom();
                    this.Hide();
                    waitingRoom.ShowDialog();   // 모달창으로 실행
                    this.Show();
                }
               
            };

            Button modeButton = new Button
            {
                Text = "모드 보기",
                Font = buttonFont,
                Size = new Size(100, 40),
                Location = new Point(275, 500),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat // Flat 스타일로 설정
            };
            // 테두리 색상 설정
            modeButton.FlatAppearance.BorderColor = Color.Black;
            modeButton.FlatAppearance.BorderSize = 1; // 테두리 두께
            modeButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            modeButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            modeButton.Click += (s, e) =>
            {
                MessageBox.Show($"{selectedRoom.Text} 모드보기!");

            };

            Button IPButton = new Button
            {
                Text = "방장 IP 확인",
                Font = buttonFont,
                Size = new Size(100, 40),
                Location = new Point(425, 500),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat // Flat 스타일로 설정
            };
            // 테두리 색상 설정
            IPButton.FlatAppearance.BorderColor = Color.Black;
            IPButton.FlatAppearance.BorderSize = 1; // 테두리 두께
            IPButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            IPButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            IPButton.Click += (s, e) =>
            {
                MessageBox.Show($"{selectedRoom.Text} 방장 IP 확인!");

            };

            Button backButton = new Button
            {
                Text = "뒤로가기",
                Font = buttonFont,
                Size = new Size(100, 40),
                Location = new Point(575, 500),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat // Flat 스타일로 설정
            };
            // 테두리 색상 설정
            backButton.FlatAppearance.BorderColor = Color.Black;
            backButton.FlatAppearance.BorderSize = 1; // 테두리 두께
            backButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
            backButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;



            backButton.Click += (s, e) =>
            {
                mainTitle = "멀티 플레이";      // 제목 변경
                Invalidate();                // 화면 새로고침
                roomJoinPanel.Visible = false;
                mainMenuPanel.Visible = true;
            };


            roomJoinPanel.Controls.Add(joinButton);
            roomJoinPanel.Controls.Add(modeButton);
            roomJoinPanel.Controls.Add(IPButton);
            roomJoinPanel.Controls.Add(backButton);
        }

        private void RoomJoinPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 펜 색상 및 두께 설정
            using (Pen pen = new Pen(Color.BurlyWood, 10))
            {
                int boxStartX = 100;
                int boxStartY = 150;
                int boxwidth = 600;
                int boxheight = 325;

                // 사각형 위치와 크기 설정
                Rectangle rect = new Rectangle(boxStartX, boxStartY, boxwidth, boxheight);

                // 사각형 테두리 그리기
                g.DrawRectangle(pen, rect);

                g.FillRectangle(Brushes.Wheat, new Rectangle(boxStartX, boxStartY, boxwidth, boxheight));


            }
        }

        private void GenerateRoomButtons()
        {
            // ➡ 기존에 생성된 버튼 삭제
            foreach (var btn in roomButtons)
            {
                roomJoinPanel.Controls.Remove(btn);
            }
            roomButtons.Clear();

            // ➡ 새롭게 버튼 생성
            int startX = 120;
            int startY = 160;
            int gapY = 50;

            for (int i = 0; i < roomTitleList.Count; i++)
            {
                Button roomButton = new Button
                {
                    Text = roomTitleList[i],
                    Font = buttonFont,
                    Size = new Size(560, 40),
                    Location = new Point(startX, startY + (i * gapY)),
                    BackColor = Color.BurlyWood,
                    FlatStyle = FlatStyle.Flat
                };

                roomButton.FlatAppearance.BorderColor = Color.BurlyWood;
                roomButton.FlatAppearance.BorderSize = 1;
                roomButton.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
                roomButton.FlatAppearance.MouseDownBackColor = Color.Goldenrod;

                // 클릭 이벤트
                roomButton.Click += (s, e) =>
                {
                    if (selectedRoom != null && selectedRoom != roomButton)
                    {
                        // 이전에 선택된 버튼의 색상 초기화
                        selectedRoom.BackColor = Color.BurlyWood;
                    }

                    // 현재 선택된 버튼 갱신 및 색상 변경
                    selectedRoom = roomButton;
                    selectedRoom.BackColor = Color.Goldenrod;
                };

                roomButtons.Add(roomButton);
                roomJoinPanel.Controls.Add(roomButton);
            }
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 1️⃣ 제목 텍스트
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(mainTitle, titleFont, Brushes.BurlyWood, new RectangleF(0, 50, this.ClientSize.Width, 60), centerFormat);

            // 2️⃣ 좌우 캐릭터 이미지
            int sideMargin = 60;
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
        }
    }
}

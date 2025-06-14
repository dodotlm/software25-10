
// MultiPlayGameForm.cs (C# 7.3 호환, 오류 없음)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace InRang
{
    public partial class MultiPlayGameForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;

        private List<NetPlayer> players = new List<NetPlayer>();
        private Dictionary<int, Label> nameLabels = new Dictionary<int, Label>();
        private Dictionary<int, PictureBox> playerBoxes = new Dictionary<int, PictureBox>();

        private string myRole = "";
        private int myId = -1;
        private int playerCount = 0;

        private GamePhase currentPhase = GamePhase.Introduction;
        private System.Windows.Forms.Timer gameTimer;
        private int timeRemaining;

        private Panel gamePanel;
        private RichTextBox chatBox;
        private TextBox messageBox;
        private Button sendButton;
        private Label phaseLabel;
        private Label timeLabel;
        private ComboBox targetSelector;
        private Button actionButton;

        private Font font = new Font("Noto Sans KR", 12, FontStyle.Bold);
        private Image playerImage;
        private Image deadOverlay;
        private Image voteOverlay;

        public MultiPlayGameForm(TcpClient client)
        {
            this.client = client;
            this.stream = client.GetStream();
            InitializeComponent();

            string res = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\resources");
            try { playerImage = Image.FromFile(Path.Combine(res, "player.jpg")); } catch { }
            try { deadOverlay = Image.FromFile(Path.Combine(res, "x.jpg")); } catch { }
            try { voteOverlay = Image.FromFile(Path.Combine(res, "vote.jpg")); } catch { }

            InitializeForm();
            StartReceiving();
        }

        private void InitializeForm()
        {
            this.Text = "멀티플레이 인랑 게임";
            this.ClientSize = new Size(800, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;

            gamePanel = new Panel
            {
                Size = this.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.Black
            };
            this.Controls.Add(gamePanel);

            phaseLabel = new Label
            {
                Text = "",
                Font = new Font("Noto Sans KR", 20, FontStyle.Bold),
                ForeColor = Color.BurlyWood,
                Location = new Point(50, 20),
                AutoSize = true
            };
            gamePanel.Controls.Add(phaseLabel);

            timeLabel = new Label
            {
                Text = "",
                Font = new Font("Noto Sans KR", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(300, 20),
                AutoSize = true
            };
            gamePanel.Controls.Add(timeLabel);

            chatBox = new RichTextBox
            {
                Location = new Point(20, 80),
                Size = new Size(300, 300),
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            gamePanel.Controls.Add(chatBox);

            messageBox = new TextBox
            {
                Location = new Point(20, 390),
                Size = new Size(220, 25),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            gamePanel.Controls.Add(messageBox);

            sendButton = new Button
            {
                Text = "Send",
                Location = new Point(250, 390),
                Size = new Size(70, 25),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat
            };
            sendButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(messageBox.Text))
                {
                    SendToServer("CHAT:" + messageBox.Text);
                    messageBox.Clear();
                }
            };
            gamePanel.Controls.Add(sendButton);

            targetSelector = new ComboBox
            {
                Location = new Point(400, 400),
                Size = new Size(150, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            gamePanel.Controls.Add(targetSelector);

            actionButton = new Button
            {
                Text = "Action",
                Location = new Point(560, 400),
                Size = new Size(70, 30),
                BackColor = Color.BurlyWood,
                FlatStyle = FlatStyle.Flat,
                Visible = false
            };
            actionButton.Click += (s, e) =>
            {
                if (targetSelector.SelectedItem != null)
                {
                    string target = targetSelector.SelectedItem.ToString();
                    SendToServer("ACTION:" + target);
                    targetSelector.Visible = false;
                    actionButton.Visible = false;
                }
            };
            gamePanel.Controls.Add(actionButton);

            gameTimer = new System.Windows.Forms.Timer();
            gameTimer.Interval = 1000;
            gameTimer.Tick += (s, e) =>
            {
                timeRemaining--;
                timeLabel.Text = timeRemaining + "초";
                if (timeRemaining <= 0)
                {
                    gameTimer.Stop();
                    SendToServer("TIME_UP");
                }
            };
        }

        private void StartReceiving()
        {
            receiveThread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        byte[] buffer = new byte[1024];
                        int length = stream.Read(buffer, 0, buffer.Length);
                        if (length == 0) break;

                        string msg = Encoding.UTF8.GetString(buffer, 0, length);
                        this.Invoke((MethodInvoker)(() => HandleServerMessage(msg)));
                    }
                }
                catch
                {
                    MessageBox.Show("서버 연결 종료");
                    this.Invoke((MethodInvoker)(() => this.Close()));
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void HandleServerMessage(string msg)
        {
            if (msg.StartsWith("ROLE:"))
            {
                myRole = msg.Substring(5);
                chatBox.AppendText("[시스템] 당신의 직업은 " + myRole + "");
            }
            else if (msg.StartsWith("ID:"))
            {
                myId = int.Parse(msg.Substring(3));
            }
            else if (msg.StartsWith("START_PHASE:"))
            {
                string phase = msg.Substring(13);
                phaseLabel.Text = phase;
                currentPhase = (GamePhase)Enum.Parse(typeof(GamePhase), phase);
                timeRemaining = (phase == "Day") ? 50 : 30;
                gameTimer.Start();
            }
            else if (msg.StartsWith("CHAT:"))
            {
                chatBox.AppendText(msg.Substring(5) + "");
            }
            else if (msg.StartsWith("PLAYER_LIST:"))
            {
                string[] names = msg.Substring(13).Split(',');
                players.Clear();
                playerBoxes.Clear();
                nameLabels.Clear();
                targetSelector.Items.Clear();

                int startX = 350, y = 100, w = 60, h = 60, spacing = 90;

                for (int i = 0; i < names.Length; i++)
                {
                    players.Add(new NetPlayer { Id = i, Name = names[i], IsAlive = true });

                    PictureBox pb = new PictureBox
                    {
                        Size = new Size(w, h),
                        Location = new Point(startX + (i % 4) * spacing, y + (i / 4) * spacing),
                        Image = playerImage,
                        SizeMode = PictureBoxSizeMode.StretchImage
                    };
                    Label lb = new Label
                    {
                        Text = names[i],
                        Location = new Point(pb.Left, pb.Bottom + 2),
                        ForeColor = Color.White,
                        AutoSize = true
                    };
                    gamePanel.Controls.Add(pb);
                    gamePanel.Controls.Add(lb);

                    playerBoxes[i] = pb;
                    nameLabels[i] = lb;
                    targetSelector.Items.Add(names[i]);
                }
                playerCount = names.Length;
            }
            else if (msg.StartsWith("SHOW_ACTION"))
            {
                targetSelector.Visible = true;
                actionButton.Visible = true;
            }
            else if (msg.StartsWith("VOTE_RESULT:"))
            {
                chatBox.AppendText("[투표결과] " + msg.Substring(12) + "");
            }
            else if (msg.StartsWith("DIED:"))
            {
                int id = int.Parse(msg.Substring(5));
                if (playerBoxes.ContainsKey(id))
                {
                    playerBoxes[id].Image = deadOverlay;
                }
            }
        }

        private void SendToServer(string msg)
        {
            if (stream != null && stream.CanWrite)
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                stream.Write(data, 0, data.Length);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try { stream?.Close(); client?.Close(); receiveThread?.Abort(); } catch { }
        }
    }

    public enum GamePhase
    {
        Introduction,
        Day,
        DayResult,
        Night,
        NightResult,
        GameEnd
    }

    public class NetPlayer
    {
        public int Id;
        public string Name;
        public string Role;
        public bool IsAlive;
    }
}

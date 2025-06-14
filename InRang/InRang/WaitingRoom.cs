using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace InRang
{
    public partial class WaitingRoom : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread receiveThread;

        public WaitingRoom(TcpClient tcpClient)
        {
            InitializeComponent();
            client = tcpClient;
            stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        }

        private void WaitingRoom_Load(object sender, EventArgs e)
        {
            // 게임 설정에서 사용자 이름 전송
            SendMessage("NAME:" + GameSettings.LocalIP); // 또는 따로 사용자 이름 저장 필드 추가
            StartReceiving();
        }

        private void readyButton_Click(object sender, EventArgs e)
        {
            readyButton.Enabled = false;
            readyButton.Text = "준비 완료";
            SendMessage("READY");
        }

        private void StartReceiving()
        {
            receiveThread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        string msg = reader.ReadLine();
                        if (msg == null) break;

                        this.Invoke((MethodInvoker)(() => HandleServerMessage(msg)));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("서버 연결 종료: " + ex.Message);
                    this.Invoke((MethodInvoker)(() => this.Close()));
                }
            });
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }

        private void HandleServerMessage(string msg)
        {
            if (msg.StartsWith("START_PHASE:"))
            {
                MultiPlayGameForm gameForm = new MultiPlayGameForm(client);
                gameForm.FormClosed += (s, e) => this.Close();
                gameForm.Show();
                this.Hide();
            }
            // 추가적으로 필요한 메시지 핸들링은 여기에
        }

        private void SendMessage(string message)
        {
            try
            {
                writer.WriteLine(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("메시지 전송 실패: " + ex.Message);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            try
            {
                receiveThread?.Abort();
                stream?.Close();
                client?.Close();
            }
            catch { }
        }
    }
}

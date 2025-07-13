using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using SharpDX.DirectInput;
using Newtonsoft.Json;
using System.Drawing; // Für Button-Farben

namespace TCPRC_Client
{
    public partial class Form1 : Form
    {
        private DirectInput directInput;
        private Joystick[] availableControllers;
        private Joystick gamepad;
        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private string lastSentData = "";
        private bool isConnected = false;

        private int leftStickX, leftStickY, rightStickX, rightStickY;
        private bool buttonA, buttonB, buttonX, buttonY, buttonL, buttonR, buttonZL, buttonZR, buttonMinus, buttonPlus, buttonBX, buttonBRX, buttonHome, buttonSquare;
        private bool CH5State = false;
        private bool CH6State = false;

        public Form1()
        {
            InitializeComponent();
            directInput = new DirectInput();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var allDevices = directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices)
                                        .Concat(directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                                        .ToList();

            availableControllers = allDevices.Select(d => new Joystick(directInput, d.InstanceGuid)).ToArray();

            cb_controller.Items.Clear();
            foreach (var device in allDevices)
            {
                cb_controller.Items.Add(device.InstanceName);
            }

            if (cb_controller.Items.Count > 0)
                cb_controller.SelectedIndex = 0;

            timer1.Stop();
            timer1.Interval = 100;

            // Initialbuttonstatus
            btn_start.Enabled = false;
        }

        private void btn_controller_Click(object sender, EventArgs e)
        {
            if (cb_controller.SelectedIndex < 0)
            {
                MessageBox.Show("Bitte einen Controller auswählen.");
                return;
            }

            try
            {
                gamepad = availableControllers[cb_controller.SelectedIndex];
                gamepad.Acquire();
                // MessageBox.Show("Controller erfolgreich verbunden.");
                timer1.Start();

                // Statusanzeige(n) aktualisieren
                btn_controller.BackColor = Color.Green;
                btn_start.Enabled = true;
                UpdateCHStateDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Verbinden des Controllers: {ex.Message}");
                btn_controller.BackColor = Color.Red;
                btn_start.Enabled = false;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (gamepad == null) return;

            try
            {
                gamepad.Poll();
                var state = gamepad.GetCurrentState();

                // 0-100
                //leftStickX = (int)(state.X / 655.35);
                //leftStickY = 100 - (int)(state.Y / 655.35);
                //rightStickX = (int)(state.RotationX / 655.35);
                //rightStickY = 100 - (int)(state.RotationY / 655.35);

                // 1000-2000
                leftStickX = (int)(1000 + (state.X / 65535.0) * (2000 - 1000));
                leftStickY = (int)(3000 - (1000 + (state.Y / 65535.0) * (2000 - 1000))); // Invertiert Y1
                rightStickX = (int)(1000 + (state.RotationX / 65535.0) * (2000 - 1000));
                rightStickY = (int)(3000 - (1000 + (state.RotationY / 65535.0) * (2000 - 1000))); // Invertiert Y2

                int trimX1 = trb_X1.Value - 250;
                int trimY1 = trb_Y1.Value - 250;
                int trimX2 = trb_X2.Value - 250;
                int trimY2 = trb_Y2.Value - 250;

                leftStickX = Math.Min(2000, Math.Max(1000, leftStickX + trimX1));
                leftStickY = Math.Min(2000, Math.Max(1000, leftStickY + trimY1));
                rightStickX = Math.Min(2000, Math.Max(1000, rightStickX + trimX2));
                rightStickY = Math.Min(2000, Math.Max(1000, rightStickY + trimY2));

                // Buttons
                buttonB = state.Buttons.Length > 0 && state.Buttons[0];
                buttonA = state.Buttons.Length > 1 && state.Buttons[1];
                buttonY = state.Buttons.Length > 2 && state.Buttons[2];
                buttonX = state.Buttons.Length > 3 && state.Buttons[3];
                buttonL = state.Buttons.Length > 4 && state.Buttons[4];
                buttonR = state.Buttons.Length > 5 && state.Buttons[5];
                buttonZL = state.Buttons.Length > 6 && state.Buttons[6];
                buttonZR = state.Buttons.Length > 7 && state.Buttons[7];
                buttonMinus = state.Buttons.Length > 8 && state.Buttons[8];
                buttonPlus = state.Buttons.Length > 9 && state.Buttons[9];
                buttonBX = state.Buttons.Length > 10 && state.Buttons[10];
                buttonBRX = state.Buttons.Length > 11 && state.Buttons[11];
                buttonHome = state.Buttons.Length > 12 && state.Buttons[12];
                buttonSquare = state.Buttons.Length > 13 && state.Buttons[13];


                tb_X1.Text = leftStickX.ToString();
                tb_Y1.Text = leftStickY.ToString();
                tb_X2.Text = rightStickX.ToString();
                tb_Y2.Text = rightStickY.ToString();
                tb_A.Text = buttonA ? "True" : "False";
                tb_B.Text = buttonB ? "True" : "False";
                tb_X.Text = buttonX ? "True" : "False";
                tb_Y.Text = buttonY ? "True" : "False";
                tb_L.Text = buttonL ? "True" : "False";
                tb_R.Text = buttonR ? "True" : "False";
                tb_ZL.Text = buttonZL ? "True" : "False";
                tb_ZR.Text = buttonZR ? "True" : "False";
                tb_Minus.Text = buttonMinus ? "True" : "False";
                tb_Plus.Text = buttonPlus ? "True" : "False";
                tb_BX.Text = buttonBX ? "True" : "False";
                tb_BRX.Text = buttonBRX ? "True" : "False";
                tb_Home.Text = buttonHome ? "True" : "False";
                tb_Square.Text = buttonSquare ? "True" : "False";

                // CH5State toggeln über buttonPlus / buttonMinus
                if (buttonPlus)
                {
                    if (!CH5State) // Nur wenn vorher false
                    {
                        CH5State = true;
                        UpdateCHStateDisplay();
                    }
                }
                if (buttonMinus)
                {
                    if (CH5State) // Nur wenn vorher true
                    {
                        CH5State = false;
                        UpdateCHStateDisplay();
                    }
                }

                // CH5State toggeln über Home / Square (Geschickt für Return-to-home)
                if (buttonHome)
                {
                    if (!CH6State)
                    {
                        CH6State = true;
                        UpdateCHStateDisplay();
                    }
                }
                if (buttonSquare)
                {
                    if (CH6State)
                    {
                        CH6State = false;
                        UpdateCHStateDisplay();
                    }
                }

                // TextBox-Farben aktualisieren basierend auf Button-Status
                tb_A.BackColor = buttonA ? Color.LightGreen : SystemColors.Window;
                tb_B.BackColor = buttonB ? Color.LightGreen : SystemColors.Window;
                tb_X.BackColor = buttonX ? Color.LightGreen : SystemColors.Window;
                tb_Y.BackColor = buttonY ? Color.LightGreen : SystemColors.Window;
                tb_L.BackColor = buttonL ? Color.LightGreen : SystemColors.Window;
                tb_R.BackColor = buttonR ? Color.LightGreen : SystemColors.Window;
                tb_ZL.BackColor = buttonZL ? Color.LightGreen : SystemColors.Window;
                tb_ZR.BackColor = buttonZR ? Color.LightGreen : SystemColors.Window;
                tb_Minus.BackColor = buttonMinus ? Color.LightGreen : SystemColors.Window;
                tb_Plus.BackColor = buttonPlus ? Color.LightGreen : SystemColors.Window;
                tb_BX.BackColor = buttonBX ? Color.LightGreen : SystemColors.Window;
                tb_BRX.BackColor = buttonBRX ? Color.LightGreen : SystemColors.Window;
                tb_Home.BackColor = buttonHome ? Color.LightGreen : SystemColors.Window;
                tb_Square.BackColor = buttonSquare ? Color.LightGreen : SystemColors.Window;


                pb_X1.Value = Math.Min(Math.Max(leftStickX, pb_X1.Minimum), pb_X1.Maximum);
                pb_Y1.Value = Math.Min(Math.Max(leftStickY, pb_Y1.Minimum), pb_Y1.Maximum);
                pb_X2.Value = Math.Min(Math.Max(rightStickX, pb_X2.Minimum), pb_X2.Maximum);
                pb_Y2.Value = Math.Min(Math.Max(rightStickY, pb_Y2.Minimum), pb_Y2.Maximum);
            }
            catch
            {
                // Fehler ignorieren, falls Controller getrennt wurde
                btn_controller.BackColor = Color.Red;
                btn_start.Enabled = false;
            }
        }

        private void UpdateCHStateDisplay()
        {
            tb_CH5State.Text = CH5State ? "True" : "False";
            tb_CH5State.BackColor = CH5State ? Color.LightGreen : SystemColors.Window;

            tb_CH6State.Text = CH6State ? "True" : "False";
            tb_CH6State.BackColor = CH6State ? Color.LightGreen : SystemColors.Window;
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            var joystickData = new
            {
                LeftStickX = leftStickX,
                LeftStickY = leftStickY,
                RightStickX = rightStickX,
                RightStickY = rightStickY,
                ButtonA = buttonA,
                ButtonB = buttonB,
                ButtonX = buttonX,
                ButtonY = buttonY,
                ButtonL = buttonL,
                ButtonR = buttonR,
                ButtonZL = buttonZL,
                ButtonZR = buttonZR,
                ButtonMinus = buttonMinus,
                ButtonPlus = buttonPlus,
                ButtonBX = buttonBX,
                ButtonBRX = buttonBRX,
                ButtonHome = buttonHome,
                ButtonSquare = buttonSquare,
                CH5State = CH5State,
                CH6State = CH6State,
            };

            string currentData = JsonConvert.SerializeObject(joystickData);

            tb_jsonPreview.Text = currentData; // <-- Anzeige im UI

            if (currentData != lastSentData)
            {
                lastSentData = currentData;
                SendDataToServer(currentData);
            }
        }

        private void SendDataToServer(string data)
        {
            try
            {
                byte[] byteData = Encoding.UTF8.GetBytes(data);
                networkStream.Write(byteData, 0, byteData.Length);
                networkStream.Flush();
            }
            catch (Exception ex)
            {
                // Dieser Fehler kommt wenn die Verbindung zur Laufzeit getrennt wurde
                MessageBox.Show($"Fehler beim Senden der Daten: {ex.Message}");
            }
        }

        private void btn_start_Click(object sender, EventArgs e)
        {
            string ipAddress = tb_ip.Text;
            if (!int.TryParse(tb_port.Text, out int port) || string.IsNullOrWhiteSpace(ipAddress))
            {
                MessageBox.Show("Ungültige IP-Adresse oder Port.");
                return;
            }

            ConnectToServer(ipAddress, port);
        }

        private void ConnectToServer(string ipAddress, int port)
        {
            try
            {
                if (isConnected)
                {
                    MessageBox.Show("Bereits mit dem Server verbunden.");
                    return;
                }

                tcpClient = new TcpClient(ipAddress, port);
                networkStream = tcpClient.GetStream();
                isConnected = true;

                //MessageBox.Show("Verbindung zum Server erfolgreich.");
                timer2.Start();

                btn_start.BackColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verbindung zum Server fehlgeschlagen: {ex.Message}");
                btn_start.BackColor = Color.Red;
            }
        }

        private void btn_stop_Click(object sender, EventArgs e)
        {
            DisconnectFromServer();
        }

        private void DisconnectFromServer()
        {
            try
            {
                if (!isConnected)
                {
                    MessageBox.Show("Keine Verbindung zum Server.");
                    return;
                }

                timer1.Stop();
                timer2.Stop();
                networkStream?.Close();
                tcpClient?.Close();
                isConnected = false;

                btn_start.BackColor = Color.Red;

                MessageBox.Show("Verbindung vom Server getrennt.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Trennen der Verbindung: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            //DisconnectFromServer();
            gamepad?.Unacquire();
            timer1.Stop();
            timer2.Stop();
            networkStream?.Close();
            tcpClient?.Close();
            base.OnFormClosing(e);
        }
    }
}

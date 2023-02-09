using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public partial class Form1 : Form
    {
        private bool button_clicked = false;
        bool waiting = false;
        bool terminate = false;
        bool connected = false;
        string name;
        private readonly object send_receive = new object();
        Socket clientSocket;


        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        private void button_connect_Click(object sender, EventArgs e)
        {
            Thread connectThread = new Thread(Connect);
            connectThread.Start();
        }


        private void Connect()
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = textBox_ip.Text;

            int portNum;
            if (Int32.TryParse(textBox_port.Text, out portNum))
            {
                try
                {
                    clientSocket.Connect(IP, portNum);

                    this.name = textBox_name.Text;
                    if (this.name != "" && this.name.Length <= 64)
                    {
                        Byte[] buffer = Encoding.Default.GetBytes(this.name);
                        clientSocket.Send(buffer);
                    }
                    else
                    {
                        logs.AppendText("Enter a valid name\n");
                        button_connect.Enabled = true;
                        clientSocket.Close();
                        return;
                    }

                    Byte[] name_buffer = new Byte[1024];
                    clientSocket.Receive(name_buffer);
                    string message = Encoding.Default.GetString(name_buffer);
                    message = message.Substring(0, message.IndexOf("\0"));

                    if (message == "Please choose another name")
                    {
                        logs.AppendText(message + "\n");
                        button_connect.Enabled = true;
                        clientSocket.Close();
                        return;
                    }
                    else if (message == "Connected, Waiting in the lobby...")
                        waiting = true;

                    logs.AppendText(message + "\n");
                    button_connect.Enabled = false;
                    connected = true;

                }
            catch
            {
                logs.AppendText("Connection couldn't establish!\n");
            }
        }
            else
            {
                logs.AppendText("Port must be an integer value.\n");
            }


            while (waiting)
            {
                try
                {
                    Byte[] buffer_waiting = new Byte[1024];
                    clientSocket.Receive(buffer_waiting);
                    string msg_waiting = Encoding.Default.GetString(buffer_waiting);
                    msg_waiting = msg_waiting.Substring(0, msg_waiting.IndexOf("\0"));
                    if (msg_waiting == "New Game")
                        waiting = false;
                }
                catch
                {
                    continue;
                }
            }

            if (connected)
            {
                Thread receiveThread = new Thread(Receive);
                Thread sendThread = new Thread(Send);
                receiveThread.Start();
                sendThread.Start();
            }

        }


        private void Send()
        {
            textBox_message.Enabled = true;
            button_send.Enabled = true;
            button_connect.Enabled = false;
            while (connected)
            {
                try
                {
                    if (button_clicked)
                    {
                        string s = textBox_message.Text;
                        Byte[] buffer = Encoding.Default.GetBytes(s);
                        clientSocket.Send(buffer);
                        button_clicked = false;
                        textBox_message.Clear();
                    }
                }
                catch
                {
                    if (!terminate)
                    {
                        continue;
                    }
                    else
                        logs.AppendText("Closing...");

                }
            }
        }


        private void Receive()
        {
            while (connected)
            {
                try
                {
                    Byte[] buffer = new Byte[1024];
                    clientSocket.Receive(buffer);

                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                    logs.AppendText("Server: " + incomingMessage + "\n");
                    if (incomingMessage == "Server Closing")
                    {
                        logs.AppendText("The server closed.\n\nGame Over!\n");
                        clientSocket.Close();
                        connected = false;
                        button_connect.Enabled = true;
                        textBox_message.Enabled = false;
                        button_send.Enabled = false;
                    }
                    textBox_message.Enabled = true;
                    button_send.Enabled = true;
                }
                catch
                {
                    continue;
                }

            }
        }


        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            connected = false;
            terminate = true;
            Environment.Exit(0);
        }

        private void button_send_Click(object sender, EventArgs e)
        {
            button_clicked = true;
        }
    }
}

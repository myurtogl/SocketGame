using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace server
{
    public partial class Form1 : Form
    {
        int no_of_players = 0;
        int no_of_answers = 0;
        int q_index = 0;
        List<string> names = new List<string>();
        IDictionary<string, int> buffer_names_values = new Dictionary<string, int>();
        IDictionary<string, int> question_answers = new Dictionary<string, int>();
        IDictionary<string, double> scores = new Dictionary<string, double>();

        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<Socket> clientSockets = new List<Socket>();
        IDictionary<string, Socket> names_clients = new Dictionary<string, Socket>();

        IDictionary<string, Socket> waiting_clients = new Dictionary<string, Socket>();

        bool game_started = false;
        bool terminating = false;
        bool listening = false;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        void start_game()
        {
            q_index = 0;

            foreach (string name in waiting_clients.Keys)
            {
                no_of_players += 1;
                names.Add(name);
                names_clients.Add(name, waiting_clients[name]);
                clientSockets.Add(waiting_clients[name]);
            }


            question_answers = new Dictionary<string, int>();
            scores = new Dictionary<string, double>();
            foreach (string name in names)
                scores.Add(name, 0.0);

            buffer_names_values = new Dictionary<string, int>();


            read_questions_answers();
            while (clientSockets.Count != no_of_players)
            {
                // spin
            };

            int no_of_questions = 0;
            if(textBox_question.Text != "")
                no_of_questions = Int32.Parse(textBox_question.Text);
            else
            {
                logs.AppendText("At least 1 questions is required\n");
                button_start.Enabled = true;
                return;
            }
            while (q_index < no_of_questions)
            {
                if (no_of_answers == 0)
                {
                    string q = "Question" + (q_index + 1).ToString() + "\n" + question_answers.ElementAt(q_index).Key;
                    Byte[] send_buffer = Encoding.Default.GetBytes(q);
                    foreach (Socket client in clientSockets)
                    {
                        client.Send(send_buffer);
                    }

                }

                logs.AppendText("Question " + (q_index + 1).ToString() + "\n");

                while (no_of_answers != no_of_players)
                {
                    // spin
                };


                if (no_of_answers == no_of_players)
                {
                    evaluate_answers(buffer_names_values);
                    buffer_names_values = new Dictionary<string, int>();
                    q_index += 1;
                    no_of_answers = 0;
                    string message = "\n\n";
                    foreach (KeyValuePair<string, double> kv in scores)
                        message = message + kv.Key + ":" + kv.Value.ToString() + "\n";
                    Byte[] buffer = Encoding.Default.GetBytes(message);
                    foreach (Socket s in clientSockets)
                    {
                        s.Send(buffer);
                    }
                }
            }
            string msg;
            double max = 0.0;
            for(int i=0; i<scores.Count; i++)
            {
                string name = scores.ElementAt(i).Key;
                double score = scores.ElementAt(i).Value;
                if (score > max)
                    max = score;
            }

            List<string> winners = new List<string>();
            for(int i=0; i<scores.Count; i++)
            {
                string name = scores.ElementAt(i).Key;
                double score = scores.ElementAt(i).Value;
                if (score == max)
                    winners.Add(name);
            }

            if (winners.Count == no_of_players)
                msg = "Draw";
            else if(winners.Count == 1)
                msg = "Player " + winners[0] + " Won";
            else
            {
                msg = "Players ";
                foreach (string name in winners)
                    msg += name + " ";
                msg += "Won";
            }

            logs.AppendText(msg+ "\n");

            Byte[] buffer_2 = Encoding.Default.GetBytes(msg);
            foreach (Socket s in clientSockets)
            {
                s.Send(buffer_2);
            }

            msg = "New Game";
            buffer_2 = Encoding.Default.GetBytes(msg);
            logs.AppendText("\n\nGame Over\n\n");

            foreach (Socket s in clientSockets)
                s.Send(buffer_2);

            foreach (Socket s in waiting_clients.Values)
                s.Send(buffer_2);

            button_start.Enabled = true;
            game_started = false;
        }

        void read_questions_answers()
        {
            int index = 0;
            List<string> lines = new List<string>();
            foreach(string line in File.ReadLines("questions.txt"))
            {
                lines.Add(line);
            }
            
            for (index=0;index<lines.Count; index+=2)
            {
                string q = lines[index];
                string a = lines[index + 1];
                this.question_answers.Add(q, Int32.Parse(a));
            }
        }

        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;

            if(Int32.TryParse(textBox_port.Text, out serverPort))
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                serverSocket.Bind(endPoint);
                serverSocket.Listen(3);

                listening = true;
                button_listen.Enabled = false;

                Thread acceptThread = new Thread(Accept);
                acceptThread.Start();

                logs.AppendText("Started listening on port: " + serverPort + "\n");
            }
            else
            {
                logs.AppendText("Please check port number \n");
            }
        }

        private void Accept()
        {
            while(listening)
            {
                try
                {
                    Socket newClient = serverSocket.Accept();

                    Thread connectThread = new Thread(() => Connection(newClient));
                    connectThread.Start();

                }
                catch
                {

                    if (!terminating)
                    {
                        continue;
                    }
                    else
                        return;
   
                }
            }
        }

        private void Connection(Socket thisClient)
        {
            string name="";
            try
            {
                Byte[] name_buffer = new Byte[64];
                thisClient.Receive(name_buffer);
                name = Encoding.Default.GetString(name_buffer);
                name = name.Substring(0, name.IndexOf("\0"));
                if (names.Contains(name))
                {
                    string message = "Please choose another name";
                    Byte[] buffer = Encoding.Default.GetBytes(message);
                    thisClient.Send(buffer);
                    thisClient.Close();
                    return;
                }

                if (!game_started)
                {
                    no_of_players += 1;

                    string message2 = "You are connected to the server";
                    Byte[] buffer2 = Encoding.Default.GetBytes(message2);
                    thisClient.Send(buffer2);

                    logs.AppendText("Player " + name + " is connected.\n");
                    names.Add(name);
                    names_clients.Add(name, thisClient);
                    clientSockets.Add(thisClient);
                }
                else
                {
                    waiting_clients.Add(name, thisClient);

                    string message3 = "Connected, Waiting in the lobby...";
                    Byte[] buffer3 = Encoding.Default.GetBytes(message3);
                    thisClient.Send(buffer3);

                    logs.AppendText("Player " + name + " is connected.\n");
                }
                
            }
            catch
            {
                if (!terminating)
                {
                    logs.AppendText("Player " + name + " disconnected\n");
                }
            }

            Receive(thisClient, name);
        }



        private void Receive(Socket thisClient, string name)
        {
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    Byte[] buffer = new Byte[64];
                    thisClient.Receive(buffer);


                    string incomingMessage = Encoding.Default.GetString(buffer);
                    incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                    logs.AppendText(name + ": " + incomingMessage + "\n");

                    KeyValuePair<string, int> nv = new KeyValuePair<string, int>(name, Int32.Parse(incomingMessage));
                    buffer_names_values.Add(nv);
                    no_of_answers += 1;
                }
                catch
                {
                    if(!terminating)                                        // Buradaki disconnect i düzelt
                    {
                        logs.AppendText("Player " + name + " has disconnected\n");
                        thisClient.Close();
                        clientSockets.Remove(thisClient);
                        connected = false;
                        names.Remove(name);
                    }
                }

            }
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            string msg = "Server Closing";
            Byte[] buffer = Encoding.Default.GetBytes(msg);
            foreach (Socket s in clientSockets)
                s.Send(buffer);
            Environment.Exit(0);
        }



        private void evaluate_answers(IDictionary<string, int> answers)
        {
            IDictionary<string, int> closeness = new Dictionary<string,int>();
            foreach (KeyValuePair<string, int> na in answers)
            {
                closeness.Add(na.Key, Math.Abs(na.Value - question_answers.ElementAt(q_index).Value));
            }

            int min_value = Int16.MaxValue;
            foreach(KeyValuePair<string, int> c in closeness)
            {
                if (min_value > c.Value)
                    min_value = c.Value;
            }

            int k = 0;
            foreach (KeyValuePair<string, int> c in closeness)
            {
                if (min_value == c.Value)
                    k += 1;
            }

            for(int i=0; i<closeness.Count; i++)
            {
                string name = closeness.ElementAt(i).Key;
                int value = closeness.ElementAt(i).Value;
                if (value == min_value)
                    scores[name] += 1.0/k;
            }

        }

        private void button_start_Click(object sender, EventArgs e)
        {
            if (no_of_players > 1)
            {
                Thread startThread = new Thread(start_game);
                game_started = true;
                button_start.Enabled = false;
                startThread.Start();
            }
            else
            {
                logs.AppendText("At least 2 players needed to start\n");
                return;
            }   
        }
    }
}

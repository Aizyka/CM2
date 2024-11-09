using Microsoft.VisualBasic.Logging;
using System.IO.Ports;
using System.Security.Cryptography;
using System.Security.Policy;

namespace ComPortsApp
{
    public partial class Form1 : Form
    {
        private int station;
        private Com communication;
        string[] ports = Com.GetPorts();
        public Form1(int station)
        {
            this.station = station;
            InitializeComponent();
            communication = new Com(station, this);
            labelChange();
            Text = "Station " + station;
            for (int i = 1; i <= 3; i++)
                if (i != station)
                    comboBox4.Items.Add(i.ToString());
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox3.Items.AddRange(ports);
            comboBox2.Items.AddRange(ports);
            comboBox1.Items.Add("None");
            comboBox1.Items.Add("Odd");
            comboBox1.Items.Add("Even");
            comboBox1.Items.Add("Mark");
            comboBox1.Items.Add("Space");
            comboBox1.SelectedIndex = 0;
            switch(station)
            {
                case 1:
                    SelectDefaultPort("COM1", comboBox3);
                    SelectDefaultPort("COM13", comboBox2);
                    break;
                case 2:
                    SelectDefaultPort("COM10", comboBox3);
                    SelectDefaultPort("COM2", comboBox2);
                    break;
                case 3:
                    SelectDefaultPort("COM12", comboBox3);
                    SelectDefaultPort("COM11", comboBox2);
                    break;
            }
        }

        void SelectDefaultPort(string portname, ComboBox cb)
        {
            int selected = 0;
            for(int i = 0; i < ports.Length; i++)
            {
                if(portname == ports[i])
                {
                    selected = i;
                    break;
                }
            }
            cb.SelectedIndex = selected;
        }


        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                string inputText = textBox1.Text;
                communication.SendData(inputText);
                textBox1.Clear();
                e.SuppressKeyPress = true;
                labelChange();
            }
        }

        private void labelChange()
        {
            label1.Text = $"Скорость порта: {communication.returnBaudRate} бит/сек\n" +
                $"Отправлено байт: {communication.returnBytesCount}\n" +
                $"Паритет: {communication.getParity()}";
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.SelectedItem == null)
                return;
            if (comboBox2.SelectedItem.ToString() == comboBox3.SelectedItem.ToString())
                return;
            communication.OpenPorts(comboBox3.SelectedItem.ToString(), comboBox2.SelectedItem.ToString());
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox3.SelectedItem == null)
                return;
            if (comboBox2.SelectedItem.ToString() == comboBox3.SelectedItem.ToString())
                return;
            communication.OpenPorts(comboBox3.SelectedItem.ToString(), comboBox2.SelectedItem.ToString());
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedParity = comboBox1.SelectedItem.ToString();
            if (selectedParity != null)
                communication.changeParity(selectedParity);
        }

        public void Clear()
        {
            richTextBox1.Clear();
        }

        public void Invoke(Delegate method)
        {
            richTextBox1.Invoke(method);
        }

        public void Log2(string msg)
        {
            Log2(msg, Color.Black);
        }

        public void Log2(string msg, Color color)
        {
            richTextBox1.Invoke(() =>
            {
                richTextBox1.SelectionColor = color;
                richTextBox1.AppendText($"{msg}");
            });
        }

        public void Log(string msg)
        {
            Log(msg, Color.Black);
        }

        public void Log(string msg, Color color)
        {
            richTextBox1.Invoke(() =>
            {
                richTextBox1.SelectionColor = color;
                richTextBox1.AppendText($"{msg}\r\n");
            });
        }

        public bool Monitor()
        {
            return checkBox2.Checked;
        }

        public bool HighPriority()
        {
            return checkBox1.Checked;
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox4.SelectedItem == null)
                return;
            communication.changeStation(int.Parse(comboBox4.SelectedItem.ToString()));
        }
    }
}

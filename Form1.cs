using System.IO.Ports;
using System.Security.Cryptography;
using System.Security.Policy;

namespace ComPortsApp
{
    public partial class Form1 : Form
    {
        private Com communication;
        string[] ports = Com.GetPorts();
        public static RichTextBox richText;
        public Form1()
        {
            InitializeComponent();
            communication = new Com();
            labelChange();
            richText = richTextBox1;
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
    }
}

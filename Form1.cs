using System.IO.Ports;
using System.Security.Cryptography;
using System.Security.Policy;

namespace ComPortsApp
{
    public partial class Form1 : Form
    {
        private Com communication;
        string[] ports = Com.ChoosePorts();
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
    }
}

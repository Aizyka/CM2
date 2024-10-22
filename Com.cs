using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
namespace ComPortsApp
{
    class Package
    {
        public byte[] originalData;
        public int curr;

        public Package(int packageSize)
        {
            originalData = new byte[packageSize];
            curr = 0;
        }
    }

    public class Com : IDisposable
    {
        const int PackageSize = 29; // pos in group
        byte[] flag;
        byte destAddress = 0x00;
        byte fcs = 0x00;

        private SerialPort[] comPorts;
        private int sentBytesCount = 0;
        private Package currentPackage;


        public Com()
        {
            flag = new byte[2] { (byte)'$', (byte)('a' + (byte)PackageSize) };
            string[] ports = ChoosePorts();
            comPorts = new SerialPort[2];
            for(int i = 0; i < 2; i++)
                comPorts[i] = new SerialPort(ports[i], 9600, Parity.None, 8, StopBits.One);

            comPorts[1].DataReceived += new SerialDataReceivedEventHandler(ComPort2_DataReceived); // COMx+1 <- COMy

            for (int i = 0; i < 2; i++)
                comPorts[i].Open();

        }

        private void ComPort2_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] buffer = new byte[PackageSize + 6];
            int count = comPorts[1].Read(buffer, 0, buffer.Length);
            for(int i = 0; i < count; i++)
            {
                if (buffer[i] == 0x00 && currentPackage != null) //end of package
                {
                    GetResult(currentPackage);
                    currentPackage = null;
                }
                else if (buffer[i] != 0x00) //fill package data
                {
                    if (currentPackage == null)
                    {
                        currentPackage = new Package(PackageSize + 1);
                        Form1.richText.Invoke(() =>
                        {
                            Form1.richText.SelectionColor = Color.Black;
                            Form1.richText.AppendText("\r\nGetting data with size: " + (buffer[i + 1] - 'a') + "\r\n")
                        });
                        i += 3;
                        continue;
                    }
                    currentPackage.originalData[currentPackage.curr] = buffer[i];
                    currentPackage.curr++;
                }
            }
        }

        private void GetResult(Package package)
        {
            int packageLength = package.curr;
            
            byte[] encoded = package.originalData.Take(packageLength).ToArray();
            byte[] decoded = COBS.Decode(encoded).ToArray();

            Form1.richText.Invoke(() =>
            {
                Form1.richText.SelectionColor = Color.Black;

                Form1.richText.AppendText("Got package:\r\nEncoded Data:\r\n");
                Form1.richText.SelectionColor = Color.Green;
                Form1.richText.AppendText("0x" + encoded[0].ToString("X2") + " ");

                for (int i = 1; i < encoded.Length; i++)
                {
                    if (encoded[i] != decoded[i - 1])
                    {
                        Form1.richText.SelectionColor = Color.Red;
                    }
                    else
                    {
                        Form1.richText.SelectionColor = Color.Black;
                    }
                    Form1.richText.AppendText("0x" + encoded[i].ToString("X2") + " ");
                }
                Form1.richText.SelectionColor = Color.Black;
                Form1.richText.AppendText("\r\nDecoded Data:\r\n");

                for (int i = 0; i < decoded.Length; i++)
                {
                    if (decoded[i] != encoded[i + 1])
                    {
                        Form1.richText.SelectionColor = Color.Red;
                    }
                    else
                    {
                        Form1.richText.SelectionColor = Color.Black;
                    }
                    Form1.richText.AppendText("0x" + decoded[i].ToString("X2") + " ");

                }
            });
        }

        public static byte[] HexStringToByteArray(string hex)
        {
            string[] hexValues = hex.Split(' ');
            byte[] byteArray = new byte[hexValues.Length];

            for (int i = 0; i < hexValues.Length; i++)
            {
                byteArray[i] = Convert.ToByte(hexValues[i], 16);
            }
            return byteArray;
        }

        public void SendData(string data)
        {
            byte[] hex;
            if(data.StartsWith("h"))
            {
                hex = HexStringToByteArray(data.Substring(1));
            }
            else
            {
                hex = Encoding.ASCII.GetBytes(data);
            }
            int toSend = hex.Length / PackageSize;
            if (hex.Length % PackageSize > 0)
                toSend++;
            for(int i = 0; i < toSend; i++)
            {
                byte[] encoded = COBS.Encode(hex.Skip(i * PackageSize).Take(Math.Min(PackageSize, hex.Length - (i * PackageSize)))).ToArray();
                byte[] buffer = new byte[PackageSize + 6]; // 5 header + 1 COBS header
                Buffer.BlockCopy(flag, 0, buffer, 0, 2);
                buffer[2] = destAddress;
                buffer[3] = 0x01;
                Buffer.BlockCopy(encoded, 0, buffer, 4, encoded.Length);
                for(int j = 4 + encoded.Length; j < PackageSize + 5; j++)
                {
                    buffer[j] = 0x00;
                }
                buffer[buffer.Length - 1] = fcs;

                comPorts[0].Write(buffer, 0, buffer.Length);
                sentBytesCount += buffer.Length;
            }
        }

        public int returnBaudRate => comPorts[0].BaudRate;
        public int returnBytesCount => sentBytesCount;

        public static string[] ChoosePorts()
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length >= 2)
            {
                return new string[] { ports[0], ports[1] };
            }
            else
            {
                throw new Exception("Недостаточно доступных COM-портов.");
            }
        }

        public string getParity()
        {
            Parity parity = comPorts[0].Parity;
            return parity.ToString();
        }

        public void Dispose()
        {
            for (int i = 0; i < 2; i++)
                comPorts[i]?.Close();
        }
    }
}

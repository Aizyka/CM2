using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
namespace ComPortsApp
{
    class Package
    {
        public byte[] originalData;
        public int curr;
        public byte fcs;

        public Package(int packageSize)
        {
            originalData = new byte[packageSize];
            curr = 0;
        }
    }

    public class Com : IDisposable
    {
        const int PackageSize = 29; // pos in group
        byte specialSymbol;
        byte[] flag;
        byte destAddress = 0x00;

        private SerialPort[] comPorts;
        private SerialPort sendPort;
        private SerialPort receivePort;
        private int sentBytesCount = 0;
        private Package currentPackage;

        private Random random = new Random();


        public Com()
        {
            specialSymbol = (byte)('a' + (byte)PackageSize);
            flag = new byte[2] { (byte)'$', specialSymbol };
            string[] ports = GetPorts();
            comPorts = new SerialPort[ports.Length];
            for(int i = 0; i < ports.Length; i++)
                comPorts[i] = new SerialPort(ports[i], 9600, Parity.None, 8, StopBits.One);
        }

        private void ComPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] buffer = new byte[PackageSize + 6];
            int count = receivePort.Read(buffer, 0, buffer.Length);
            if (count != buffer.Length)
                return;
            currentPackage = new Package(PackageSize + 6);
            for(int i = 4; i < buffer.Length - 1; i++)
            {
                if (buffer[i] == 0x00)
                    break;
                currentPackage.originalData[currentPackage.curr] = buffer[i];
                currentPackage.curr++;
            }
            currentPackage.fcs = buffer[buffer.Length - 1];
            GetResult(currentPackage);
            currentPackage = null;
        }

        private void GetResult(Package package)
        {
            int packageLength = package.curr;
            
            byte[] encoded = package.originalData.Take(packageLength).ToArray();

            Form1.Invoke(() =>
            {
                Form1.Log("Got Package:");
                Form1.Log("Original Data:");
                for (int i = 0; i < encoded.Length; i++)
                {
                    Form1.Log2("0x" + encoded[i].ToString("X2") + " ");
                }
                Form1.Log("");
                byte actualFcs = FCS.CreateFcs(encoded);
                if (package.fcs != actualFcs)
                {
                    Form1.Log("FCS mismatch!", Color.Red);
                    byte[] fixedData = FCS.CheckAndCorrectHamming(encoded, package.fcs);
                    Array.Copy(fixedData, encoded, encoded.Length);
                    Form1.Log("Fixed Data:");
                    for (int i = 0; i < encoded.Length; i++)
                    {
                        Form1.Log2("0x" + encoded[i].ToString("X2") + " ");
                    }
                    Form1.Log("");
                }

                byte[] decoded = COBS.Decode(encoded, specialSymbol).ToArray();

                Form1.Log("Encoded Data:");
                Form1.Log2("0x" + encoded[0].ToString("X2") + " ", Color.Green);

                for (int i = 1; i < encoded.Length; i++)
                {
                    if (encoded[i] != decoded[i - 1])
                    {
                        Form1.Log2("0x" + encoded[i].ToString("X2") + " ", Color.Red);
                    }
                    else
                    {
                        Form1.Log2("0x" + encoded[i].ToString("X2") + " ");
                    }   
                }
                Form1.Log("Decoded Data:");
                for (int i = 0; i < decoded.Length; i++)
                {
                    if (decoded[i] != encoded[i + 1])
                    {
                        Form1.Log2("0x" + decoded[i].ToString("X2") + " ", Color.Red);
                    }
                    else
                    {
                        Form1.Log2("0x" + decoded[i].ToString("X2") + " ");
                    }
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

        public void OpenPorts(string fromPort, string toPort)
        {
            if(receivePort != null)
            {
                sendPort.Close();
                receivePort.Close();
            }

            for(int i = 0; i < comPorts.Length; i++)
            {
                if (comPorts[i].PortName == fromPort)
                {
                    comPorts[i].Open();
                    sendPort = comPorts[i];
                }
                else if(comPorts[i].PortName == toPort)
                {
                    comPorts[i].Open();
                    receivePort = comPorts[i];
                }
            }
            receivePort.DataReceived += ComPort_DataReceived;
        }

        public void SendData(string data)
        {
            if (sendPort == null)
                return;
            Form1.Clear();
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
                byte[] encoded = COBS.Encode(hex.Skip(i * PackageSize).Take(Math.Min(PackageSize, hex.Length - (i * PackageSize))), specialSymbol).ToArray();
                byte fcs = FCS.CreateFcs(encoded);
                int rand = random.Next(0, 100);
                if (rand <= 60 && encoded.Length > 1)
                {
                    Form1.Log("Simulating one error", Color.Purple);
                    encoded = SimulateErrors(encoded, new int[] { 15 });
                }
                else if (rand > 60 && rand < 85 && data.Length > 4)
                {
                    Form1.Log("Simulating two errors", Color.Purple);
                    encoded = SimulateErrors(encoded, new int[] { 15, 30 });
                }
                byte[] buffer = new byte[PackageSize + 6]; // 5 header + 1 COBS header
                Buffer.BlockCopy(flag, 0, buffer, 0, 2);
                buffer[2] = destAddress;
                buffer[3] = byte.Parse(sendPort.PortName.Substring(3));
                Buffer.BlockCopy(encoded, 0, buffer, 4, encoded.Length);
                for (int j = 4 + encoded.Length; j < PackageSize + 5; j++)
                {
                    buffer[j] = 0x00;
                }
                buffer[buffer.Length - 1] = fcs;
                while(ListenChannel())
                {
                    Form1.Log("Channel is busy, waiting", Color.Pink);
                    Thread.Sleep(CalculateRandomDelay());
                }
                foreach(var b in buffer)
                {
                    while(EmulateCollision())
                    {
                        Form1.Log("Colision detected, retrying", Color.Red);
                        Thread.Sleep(CalculateRandomDelay());
                    }
                    sendPort.Write(new byte[] { b }, 0, 1);
                    Form1.Log($"Send byte - {b}");
                }

                sendPort.Write(buffer, 0, buffer.Length);
                sentBytesCount += buffer.Length;
            }
        }

        byte[] SimulateErrors(byte[] data, int[] errorPositions)
        {
            char[] dataBits = string.Join("", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))).ToCharArray();
            foreach (int pos in errorPositions)
            {
                if (pos <= dataBits.Length)
                {
                    dataBits[pos - 1] = dataBits[pos - 1] == '0' ? '1' : '0';
                }
            }
            return Enumerable.Range(0, dataBits.Length / 8).Select(i => Convert.ToByte(new string(dataBits, i * 8, 8), 2)).ToArray();
        }

        bool ListenChannel() => random.NextSingle() < 0.5f;
        bool EmulateCollision() => random.NextSingle() < 0.3f;
        int CalculateRandomDelay()
        {
            int k = random.Next(0, 10);
            int r = random.Next(0, (int)Math.Pow(2, k) - 1);
            return r;
        }

        public int returnBaudRate => comPorts[0].BaudRate;
        public int returnBytesCount => sentBytesCount;

        public static string[] GetPorts()
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length >= 2)
            {
                return ports;
            }
            else
            {
                throw new Exception("Недостаточно доступных COM-портов.");
            }
        }

        public void changeParity(string selectedParity)
        {
            if (receivePort == null)
                return;
            Parity parity = (Parity)Enum.Parse(typeof(Parity), selectedParity);
            receivePort.Parity = parity;
            sendPort.Parity = parity;
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

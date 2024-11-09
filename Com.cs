using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Reflection;
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
        private int destinationStation = 0;
        private int sourceStation = 0;
        private Form1 form1;

        private Random random = new Random();


        public Com(int sourceStation, Form1 form)
        {
            form1 = form;
            this.sourceStation = sourceStation;
            specialSymbol = (byte)('a' + (byte)PackageSize);
            flag = new byte[2] { (byte)'$', specialSymbol };
            string[] ports = GetPorts();
            comPorts = new SerialPort[ports.Length];
            for(int i = 0; i < ports.Length; i++)
                comPorts[i] = new SerialPort(ports[i], 9600, Parity.None, 8, StopBits.One);
        }

        private void ComPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] tokenBuffer = new byte[4];
            int tokenCount = receivePort.Read(tokenBuffer, 0, 4);
            if (tokenBuffer[3] == '0')
            {
                if(form1.Monitor())
                {
                    form1.Log("Got Empty token");
                }
                return;
            }

            byte[] buffer = new byte[PackageSize + 7];
            int count = receivePort.Read(buffer, 0, buffer.Length);
            if (count != buffer.Length)
                return;
            currentPackage = new Package(PackageSize + 7);
            for(int i = 4; i < buffer.Length - 2; i++)
            {
                if (buffer[i] == 0x00)
                    break;
                currentPackage.originalData[currentPackage.curr] = buffer[i];
                currentPackage.curr++;
            }
            currentPackage.fcs = buffer[buffer.Length - 2];
            
            if(buffer[buffer.Length - 1] != (byte)(sourceStation + 48))
            {
                Resend(currentPackage, buffer[buffer.Length - 1]);
                if(form1.Monitor())
                {
                    form1.Log("Got data and resending to next station");
                }
            }
            else
            {
                GetResult(currentPackage);
                string token = "0000";
                if (form1.HighPriority())
                {
                    token = token.Remove(sourceStation - 1, 1).Insert(sourceStation - 1, "1");
                }
                byte[] tokenData = Encoding.ASCII.GetBytes(token);
                sendPort.Write(tokenData, 0, 4);
            }
            currentPackage = null;
        }

        private void Resend(Package package, byte destination)
        {
            byte[] buffer = new byte[PackageSize + 7]; // 5 header + 1 COBS header + 1 destination station
            Buffer.BlockCopy(flag, 0, buffer, 0, 2);
            buffer[2] = destAddress;
            buffer[3] = byte.Parse(sendPort.PortName.Substring(3));
            Buffer.BlockCopy(package.originalData, 0, buffer, 4, package.curr);
            for (int j = 4 + package.originalData.Length; j < PackageSize + 5; j++)
            {
                buffer[j] = 0x00;
            }
            buffer[buffer.Length - 2] = package.fcs;
            buffer[buffer.Length - 1] = destination;

            string token = "0001";
            if (form1.HighPriority())
            {
                token = token.Remove(sourceStation - 1, 1).Insert(sourceStation - 1, "1");
            }
            byte[] tokenData = Encoding.ASCII.GetBytes(token);
            sendPort.Write(tokenData, 0, 4);

            sendPort.Write(buffer, 0, buffer.Length);
            sentBytesCount += buffer.Length;
        }

        private void GetResult(Package package)
        {
            int packageLength = package.curr;
            
            byte[] encoded = package.originalData.Take(packageLength).ToArray();

            form1.Invoke(() =>
            {
                form1.Log("Got Package:");
                form1.Log("Original Data:");
                for (int i = 0; i < encoded.Length; i++)
                {
                    form1.Log2("0x" + encoded[i].ToString("X2") + " ");
                }
                form1.Log("");
                byte actualFcs = FCS.CreateFcs(encoded);
                if (package.fcs != actualFcs)
                {
                    form1.Log("FCS mismatch!", Color.Red);
                    byte[] fixedData = FCS.CheckAndCorrectHamming(encoded, package.fcs, form1);
                    Array.Copy(fixedData, encoded, encoded.Length);
                    form1.Log("Fixed Data:");
                    for (int i = 0; i < encoded.Length; i++)
                    {
                        form1.Log2("0x" + encoded[i].ToString("X2") + " ");
                    }
                    form1.Log("");
                }

                byte[] decoded = COBS.Decode(encoded, specialSymbol).ToArray();

                form1.Log("Encoded Data:");
                form1.Log2("0x" + encoded[0].ToString("X2") + " ", Color.Green);

                for (int i = 1; i < encoded.Length; i++)
                {
                    if (encoded[i] != decoded[i - 1])
                    {
                        form1.Log2("0x" + encoded[i].ToString("X2") + " ", Color.Red);
                    }
                    else
                    {
                        form1.Log2("0x" + encoded[i].ToString("X2") + " ");
                    }   
                }
                form1.Log("");
                form1.Log("Decoded Data:");
                for (int i = 0; i < decoded.Length; i++)
                {
                    if (decoded[i] != encoded[i + 1])
                    {
                        form1.Log2("0x" + decoded[i].ToString("X2") + " ", Color.Red);
                    }
                    else
                    {
                        form1.Log2("0x" + decoded[i].ToString("X2") + " ");
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
            if (destinationStation == 0)
                return;
            form1.Clear();
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
                    form1.Log("Simulating one error", Color.Purple);
                    encoded = SimulateErrors(encoded, new int[] { 15 });
                }
                else if (rand > 60 && rand < 85 && data.Length > 4)
                {
                    form1.Log("Simulating two errors", Color.Purple);
                    encoded = SimulateErrors(encoded, new int[] { 15, 30 });
                }
                byte[] buffer = new byte[PackageSize + 7]; // 5 header + 1 COBS header + 1 destination station
                Buffer.BlockCopy(flag, 0, buffer, 0, 2);
                buffer[2] = destAddress;
                buffer[3] = byte.Parse(sendPort.PortName.Substring(3));
                Buffer.BlockCopy(encoded, 0, buffer, 4, encoded.Length);
                for (int j = 4 + encoded.Length; j < PackageSize + 5; j++)
                {
                    buffer[j] = 0x00;
                }
                buffer[buffer.Length - 2] = fcs;
                buffer[buffer.Length - 1] = (byte)(destinationStation + 48);


                string token = "0001";
                if(form1.HighPriority())
                {
                    token = token.Remove(sourceStation - 1, 1).Insert(sourceStation - 1, "1");
                }
                byte[] tokenData = Encoding.ASCII.GetBytes(token);
                sendPort.Write(tokenData, 0, 4);

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

        public void changeStation(int station)
        {
            destinationStation = station;
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

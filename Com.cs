﻿using System.Text;
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
        byte specialSymbol;
        byte[] flag;
        byte destAddress = 0x00;
        byte fcs = 0x00;

        private SerialPort[] comPorts;
        private SerialPort sendPort;
        private SerialPort receivePort;
        private int sentBytesCount = 0;
        private Package currentPackage;


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
                            Form1.richText.AppendText("\r\nGetting data with size: " + (buffer[i + 1] - 'a') + "\r\n");
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
            byte[] decoded = COBS.Decode(encoded, specialSymbol).ToArray();

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
                byte[] buffer = new byte[PackageSize + 6]; // 5 header + 1 COBS header
                Buffer.BlockCopy(flag, 0, buffer, 0, 2);
                buffer[2] = destAddress;
                buffer[3] = byte.Parse(sendPort.PortName.Substring(3));
                Buffer.BlockCopy(encoded, 0, buffer, 4, encoded.Length);
                for(int j = 4 + encoded.Length; j < PackageSize + 5; j++)
                {
                    buffer[j] = 0x00;
                }
                buffer[buffer.Length - 1] = fcs;

                sendPort.Write(buffer, 0, buffer.Length);
                sentBytesCount += buffer.Length;
            }
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

namespace ComPortsApp
{
    internal class FCS
    {
        static int CalculateFcsSize(int dataSize)
        {
            int r = 0;
            while (Math.Pow(2, r) < dataSize + r + 1)
            {
                r++;
            }
            return Math.Max(r + 1, 8);
        }

        public static byte CreateFcs(byte[] data)
        {
            int dataSize = data.Length * 8;
            int fcsSize = CalculateFcsSize(dataSize);
            int actualFcsSize = Math.Min(fcsSize, 8);

            string dataBits = string.Join("", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            char[] fcs = new char[8];
            Array.Fill(fcs, '0');

            int overallParity = 0;
            for (int i = 0; i < actualFcsSize - 1; i++)
            {
                int parity = 0;
                for (int j = 0; j < dataSize; j++)
                {
                    if (((j + 1) & (1 << i)) != 0)
                    {
                        parity ^= dataBits[j] - '0';
                    }
                }
                fcs[i] = (char)(parity + '0');
                overallParity ^= parity;
            }

            foreach (char bit in dataBits)
            {
                overallParity ^= bit - '0';
            }

            fcs[actualFcsSize - 1] = (char)(overallParity + '0');

            // Fill the remaining bits with zeros, if ActualFcsSize < 8
            for (int i = actualFcsSize; i < 8; i++)
            {
                fcs[i] = '0';
            }

            return Convert.ToByte(new string(fcs), 2);
        }

        public static byte[] CheckAndCorrectHamming(byte[] data, byte fcs1)
        {
            int dataSize = data.Length * 8;
            int actualFcsSize = Math.Min(CalculateFcsSize(dataSize), 8);
            string dataBits = string.Join("", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
            int syndrome = 0;

            string fcs = Convert.ToString(fcs1, 2).PadLeft(8, '0');

            int overallParity = fcs[actualFcsSize - 1] - '0';
            int calculatedOverallParity = 0;

            for (int i = 0; i < actualFcsSize - 1; i++)
            {
                int parity = 0;
                for (int j = 0; j < dataSize; j++)
                {
                    if (((j + 1) & (1 << i)) != 0)
                    {
                        parity ^= dataBits[j] - '0';
                    }
                }
                if (parity != (fcs[i] - '0'))
                {
                    syndrome |= (1 << i);
                }
                calculatedOverallParity ^= parity;
            }

            foreach (char bit in dataBits)
            {
                calculatedOverallParity ^= bit - '0';
            }

            if (syndrome == 0 && calculatedOverallParity == overallParity)
            {
                Form1.Log("No Errors found", Color.Green);
                return data;
            }
            else if (syndrome != 0 && calculatedOverallParity != overallParity)
            {
                Form1.Log("Single error detected and fixed", Color.Green);
                int errorPos = syndrome - 1;
                char[] correctedBits = dataBits.ToCharArray();
                correctedBits[errorPos] = correctedBits[errorPos] == '0' ? '1' : '0';
                string correctedBits1 = new string(correctedBits);
                byte[] correctedData = Enumerable.Range(0, correctedBits.Length / 8).Select(i => Convert.ToByte(correctedBits1.Substring(i * 8, 8), 2)).ToArray();
                return correctedData;
            }
            else
            {
                Form1.Log("A double error was detected. Correction is not possible", Color.Red);
                return data;
            }
        }
    }
}

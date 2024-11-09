namespace ComPortsApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            Form1 station1 = new Form1(1);
            station1.Show();
            Form1 station2 = new Form1(2);
            station2.Show();
            Application.Run(new Form1(3));
        }
    }
}
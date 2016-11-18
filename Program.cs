using FtpConnect.Service;

namespace FtpConnect
{
    class Program
    {
        static void Main(string[] args)
        {
            Utility.LogMessage("START");
            FtpUtils.Download();
            Utility.LogMessage("END");
        }
    }
}

using FtpConnect.Service;

namespace FtpConnect
{
    class Program
    {
        static void Main(string[] args)
        {
            Utility.LogMessage("START");
            if (FtpUtils.Download())
            {
                FtpUtils.Upload();
            }
            Utility.LogMessage("END");
        }
    }
}

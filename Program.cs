using FluentFTP;
using FtpConnect.Service;
using System;
using System.Configuration;
using System.Net;

namespace FtpConnect
{
    class Program
    {
        static void Main(string[] args)
        {
            using (FtpClient ftpConnection = new FtpClient())
            {
                string downloadUrl = ConfigurationManager.AppSettings["Download.Url"];
                string credentials = downloadUrl.Split('@')[0];
                ftpConnection.Host = downloadUrl.Split('@')[1];
                ftpConnection.Credentials = new NetworkCredential(
                    credentials.Split(':')[0],
                    credentials.Split(':')[1]);

                ftpConnection.DataConnectionType = FtpDataConnectionType.AutoPassive;
                ftpConnection.SocketKeepAlive = true;
                ftpConnection.ConnectTimeout = 5000;

                Utility.LogMessage(String.Format("START : Connecting to server {0} with {1} ...", ftpConnection.Host, ftpConnection.Credentials.UserName));

                ftpConnection.Connect();

                if (ftpConnection.IsConnected)
                    Utility.LogMessage("Connected to server");
                else
                {
                    Utility.LogMessage("Error connecting to server");
                    Console.ReadLine();
                    return;
                }

                Service.FtpUtils.Download(ftpConnection, ConfigurationManager.AppSettings["Download.RemoteDir"]);
            }

            // Console.ReadLine();
        }
    }
}

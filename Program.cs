using FluentFTP;
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
                
                Console.WriteLine(String.Format("Connecting to server {0} with {1} ...", ftpConnection.Host, ftpConnection.Credentials.UserName));

                ftpConnection.Connect();

                if (ftpConnection.IsConnected)
                    Console.WriteLine("Connected to server");
                else
                {
                    Console.WriteLine("Error connecting to server");
                    Console.ReadLine();
                    return;
                }

                ftpConnection.SocketKeepAlive = true;

                Service.FtpUtils.Download(ftpConnection, ConfigurationManager.AppSettings["Download.RemoteDir"]);
            }

            // Console.ReadLine();
        }
    }
}

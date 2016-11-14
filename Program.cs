using FluentFTP;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;

namespace FtpConnect
{
    class Program
    {
        static void Main(string[] args)
        {

            // ftp://adm-spid:spid6adm@dvedvb80.rouen.francetelecom.fr

            using (FtpClient ftpConnection = new FtpClient())
            {
                ftpConnection.Host = "spidqlf";
                ftpConnection.Credentials = new NetworkCredential("adm-spid", "spid6adm");

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

                Service.FtpUtils.Download(ftpConnection, "/LIVRAISONS/_TRANSFERT");
            }

            Console.ReadLine();
        }
    }
}

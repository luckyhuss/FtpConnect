using FluentFTP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FtpConnect.Service
{
    class FtpUtils
    {
        /// <summary>
        ///  Download files from FTP server
        /// </summary>
        /// <param name="ftpConnection"></param>
        public static void Download(FtpClient ftpConnection, string remoteDir)
        {
            Stopwatch stopWatchBitRate = new Stopwatch();
            Stopwatch stopWatchPerDownload = new Stopwatch();

            double totalByteTranferred = 0;
            double subTotalByteTranferred = 0;

            // set working dir on FTP server
            ftpConnection.SetWorkingDirectory(remoteDir);

            Console.WriteLine("Listing files in {0} ...", ftpConnection.GetWorkingDirectory());
            var remoteFiles = ftpConnection.GetListing();

            Console.WriteLine("File listing completed : ");

            Console.WriteLine(
                String.Format("\t{0}",
                    String.Join(Environment.NewLine + "\t",
                        remoteFiles.Select(
                            ftp => String.Format("{0} [{1:f2} MB]",
                                ftp.Name, ftp.Size / 1024.0f / 1024.0f)).ToList())));

            // How many bytes to read at a time and send to the client
            int bytesToRead = 100000;

            // Buffer to read bytes in chunk size specified above
            byte[] buffer = new Byte[bytesToRead];

            foreach(var remoteFile in remoteFiles)
            {
                // reset
                totalByteTranferred = 0;
                stopWatchPerDownload.Reset();
                stopWatchPerDownload.Start();

                try
                {
                    using (Stream s = ftpConnection.OpenRead(remoteFile.FullName, FtpDataType.Binary))
                    using (var file = File.Create(@"E:\t\ftp\" + remoteFile.Name))
                    {
                        // perform transfer
                        int length;
                        float bitRate = -1.0f;

                        NumberFormatInfo nfi = new NumberFormatInfo { NumberGroupSeparator = ",", NumberDecimalDigits = 0 };

                        Console.WriteLine();
                        Console.WriteLine(String.Format("Downloading {0} of size {1} bytes ...", remoteFile.Name, remoteFile.Size.ToString("n", nfi)));
                        Console.WriteLine("===");

                        // reset stopwatch
                        stopWatchBitRate.Reset();
                        stopWatchBitRate.Start();

                        do
                        {
                            // Verify that the client is connected.
                            if (ftpConnection.IsConnected)
                            {
                                // Read data into the buffer.
                                length = s.Read(buffer, 0, bytesToRead);

                                // and write it out to the response's output stream
                                file.Write(buffer, 0, length);

                                // send data in buffer to file system
                                file.Flush(true);

                                subTotalByteTranferred += length;
                                totalByteTranferred += length;

                                if (stopWatchBitRate.ElapsedMilliseconds >= 1000)
                                {
                                    // 1 second
                                    stopWatchBitRate.Stop();

                                    // transfer rate = (kiloByte / elapsedSeconds)
                                    // kiloByte = (byte / 1000)
                                    // elapsedSeconds = ElapsedMilliseconds / 1000

                                    bitRate = ((float)(subTotalByteTranferred / 1000) / ((float)stopWatchBitRate.ElapsedMilliseconds / 1000)); // kbps

                                    //Console.Write("\rspeed : " + bitRate + " - byte " + subTotalByteTranferred);

                                    stopWatchBitRate.Reset();
                                    stopWatchBitRate.Start();
                                    subTotalByteTranferred = 0;
                                }

                                double progressPercentage = totalByteTranferred / remoteFile.Size * 100;

                                // [....................]
                                string progressBarDot = new String('.', (int)progressPercentage / 5); // ....
                                string progressBarSpace = new String(' ', (20 - (int)progressPercentage / 5)); // space
                                string progressBar = String.Format("[{0}{1}]", progressBarDot, progressBarSpace);

                                Console.Write(
                                    String.Format("\r {3} {2:f1}% => {0} bytes @ {1:f2} KB/s          ",
                                        totalByteTranferred.ToString("n", nfi),
                                        bitRate,
                                        progressPercentage,
                                        progressBar));

                                // clear the buffer
                                buffer = new Byte[bytesToRead];
                            }
                            else
                            {
                                // cancel the download if client has disconnected
                                length = -1;
                            }
                        } while (length > 0); //Repeat until no data is read

                        stopWatchPerDownload.Stop();

                        Console.WriteLine();
                        Console.WriteLine(String.Format("Download completed in {0:f2} minutes", stopWatchPerDownload.Elapsed.TotalMinutes));
                    }
                }
                catch (Exception)
                {
                    // Typical exceptions here are IOException, SocketException, or a FtpCommandException
                }
            }            
        }

        public static void Upload()
        {

        }
    }
}

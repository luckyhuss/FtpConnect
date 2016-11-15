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

            long totalByteTranferred = 0;
            long subTotalByteTranferred = 0;

            // set working dir on FTP server
            ftpConnection.SetWorkingDirectory(remoteDir);

            Console.WriteLine("Listing files in {0} ...", ftpConnection.GetWorkingDirectory());
            var remoteFiles = ftpConnection.GetListing();

            Console.WriteLine("File listing completed : ");

            Console.WriteLine(
                string.Format("\t{0}",
                    string.Join(Environment.NewLine + "\t",
                        remoteFiles.Select(
                            ftp => string.Format("{0} [{1:f2} MB]",
                                ftp.Name, Utility.ByteToMByte(ftp.Size))).ToList())));

            // Buffer to read bytes in chunk size specified above
            byte[] buffer = null;

            foreach (var remoteFile in remoteFiles)
            {
                // reset
                totalByteTranferred = 0;
                stopWatchPerDownload.Reset();
                stopWatchPerDownload.Start();
                double fileSizeMB = Utility.ByteToMByte(remoteFile.Size);

                try
                {
                    using (Stream s = ftpConnection.OpenRead(remoteFile.FullName, FtpDataType.Binary))
                    using (var file = File.Create(@"E:\t\ftp\" + remoteFile.Name))
                    {
                        // perform transfer
                        int length;
                        float bitRate = -1;
                        double timeETA = -1;
                        int animatedProgressCount = 0;

                        Console.WriteLine();
                        Console.WriteLine(string.Format("Downloading {0} of size {1} bytes ...", remoteFile.Name, Utility.NumericSeparator(remoteFile.Size)));
                        Console.WriteLine("===");

                        // reset stopwatch
                        stopWatchBitRate.Reset();
                        stopWatchBitRate.Start();

                        do
                        {
                            // initialize and clear the buffer
                            buffer = new Byte[Utility.CONST_BYTETOREAD];

                            // Verify that the client is connected.
                            if (ftpConnection.IsConnected)
                            {
                                // Read data into the buffer.
                                length = s.Read(buffer, 0, Utility.CONST_BYTETOREAD);

                                // and write it out to the response's output stream
                                file.Write(buffer, 0, length);

                                // send data in buffer to file system
                                file.Flush(true);

                                subTotalByteTranferred += length;
                                totalByteTranferred += length;

                                if (stopWatchBitRate.ElapsedMilliseconds >= 1000)
                                {
                                    // stop the stopwatch
                                    stopWatchBitRate.Stop();
                                    // every 1 second, calculate bitrate
                                    bitRate = Utility.CalculateBitRate(stopWatchBitRate, subTotalByteTranferred);

                                    // calculate ETA
                                    // TODO : Average bitrate
                                    long remainingByte = remoteFile.Size - totalByteTranferred;
                                    timeETA = remainingByte / (bitRate * 1024.0f);

                                    // reset stopwatch
                                    stopWatchBitRate.Reset();
                                    stopWatchBitRate.Start();
                                    subTotalByteTranferred = 0;
                                }

                                double progressPercentage = (double)totalByteTranferred / remoteFile.Size * 100;
                                string progressBar = Utility.AnimatedProgressBar(ref animatedProgressCount, progressPercentage);

                                Console.Write(
                                    String.Format("\r {3} {2:f1}% => {0:f2} MB / {4:f2} MB @ {1:f2} KB/s - ETA : {6:f1} seconds{5}",
                                        Utility.ByteToMByte(totalByteTranferred),
                                        bitRate,
                                        progressPercentage,
                                        progressBar,
                                        fileSizeMB,
                                        new String(' ', 10),
                                        timeETA));
                            }
                            else
                            {
                                // cancel the download if client has disconnected
                                length = -1;
                            }
                        } while (length > 0); //Repeat until all data are read

                        stopWatchPerDownload.Stop();

                        Console.WriteLine();
                        Console.WriteLine(String.Format("Downloaded {0} bytes in {1} min {2} sec", 
                            Utility.NumericSeparator(totalByteTranferred), stopWatchPerDownload.Elapsed.Minutes, stopWatchPerDownload.Elapsed.Seconds));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        public static void Upload()
        {
            // todo :
        }
    }
}

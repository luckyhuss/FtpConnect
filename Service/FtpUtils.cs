using FluentFTP;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

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
            List<float> bitRates = null;

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
                bitRates = new List<float>();

                double fileSizeMB = Utility.ByteToMByte(remoteFile.Size);
                string destinationFilename = Path.Combine(ConfigurationManager.AppSettings["Download.LocalDir"], remoteFile.Name);

                try
                {
                    using (Stream s = ftpConnection.OpenRead(remoteFile.FullName, FtpDataType.Binary))
                    using (var file = File.Create(destinationFilename))
                    {
                        // perform transfer
                        int length;
                        float bitRate = -1;
                        float averageBitRate = -1;
                        double secondETA = -1;
                        TimeSpan timeETA = new TimeSpan();
                        int animatedProgressCount = 0;
                        
                        Console.WriteLine("===");
                        Console.WriteLine(string.Format("Downloading {0} of size {1} bytes ...", remoteFile.Name, Utility.NumericSeparator(remoteFile.Size)));

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

                                // send buffer data to file system
                                file.Flush(true);

                                subTotalByteTranferred += length;
                                totalByteTranferred += length;

                                if (stopWatchBitRate.ElapsedMilliseconds >= 1000)
                                {
                                    // stop the stopwatch
                                    stopWatchBitRate.Stop();
                                    // every 1 second, calculate bitrate
                                    bitRate = Utility.CalculateBitRate(stopWatchBitRate, subTotalByteTranferred);

                                    // calculate average bitrate
                                    bitRates.Add(bitRate);
                                    // calculate ETA
                                    averageBitRate = bitRates.Average();

                                    long remainingByte = remoteFile.Size - totalByteTranferred;
                                    secondETA = remainingByte / (averageBitRate * 1024.0f);
                                    timeETA = TimeSpan.FromSeconds(secondETA);

                                    // reset stopwatch
                                    stopWatchBitRate.Reset();
                                    stopWatchBitRate.Start();
                                    subTotalByteTranferred = 0;
                                }

                                double progressPercentage = (double)totalByteTranferred / remoteFile.Size * 100;
                                string progressBar = Utility.AnimatedProgressBar(ref animatedProgressCount, progressPercentage);

                                Console.Write(
                                    string.Format("\r {3} {2:f1}% => {0:f2} MB / {4:f2} MB @ {1:f2} KB/s - ETA : {6} / Run : {7}{5}",
                                        Utility.ByteToMByte(totalByteTranferred),
                                        averageBitRate,
                                        progressPercentage,
                                        progressBar,
                                        fileSizeMB,
                                        new String(' ', 10),
                                        Utility.TimeSpanToString(timeETA),
                                        Utility.TimeSpanToString(stopWatchPerDownload.Elapsed)));
                            }
                            else
                            {
                                // cancel the download if client has disconnected
                                length = -1;
                            }
                        } while (length > 0); //Repeat until all data are read
                    }

                    stopWatchPerDownload.Stop();

                    string logEnd = String.Format("Downloaded {0} bytes in {1}",
                        Utility.NumericSeparator(totalByteTranferred), Utility.TimeSpanToString(stopWatchPerDownload.Elapsed));

                    Console.WriteLine();
                    Console.WriteLine(logEnd);

                    // TODO log
                    File.AppendAllText("beta.log", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss - ") + logEnd + " => " + remoteFile.Name + Environment.NewLine, Encoding.Default);

                    // update modified date/time
                    FileInfo fi = new FileInfo(destinationFilename);
                    fi.LastWriteTime = remoteFile.Modified;
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

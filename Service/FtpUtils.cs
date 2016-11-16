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

            Utility.LogMessage(String.Format("Listing files in {0} ...", ftpConnection.GetWorkingDirectory()));
            var remoteFiles = ftpConnection.GetListing();
            
            Utility.LogMessage(
                string.Format("File listing completed :\n\t{0}",
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

                // archive files for past two days
                if (File.Exists(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER))
                {
                    // _EPPlus 4.1 Sample.zip_older exists, therefore _old exists
                    // delete _older
                    File.Delete(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);
                    // rename _old -> older
                    File.Move(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);
                    // rename new -> _old
                    File.Move(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                }
                else if (File.Exists(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD))
                {
                    // _EPPlus 4.1 Sample.zip_old exists but not _older
                    // rename _old -> _older
                    File.Move(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);
                    // rename new -> _old
                    File.Move(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                }
                else
                {
                    // create first _old
                    File.Move(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                }

                try
                {

                    using (Stream s = ftpConnection.OpenRead(remoteFile.FullName, FtpDataType.Binary))
                    using (var destFile = File.Create(destinationFilename))
                    {
                        // perform transfer
                        int length = 0;
                        float bitRate = -1;
                        float averageBitRate = -1;
                        double secondETA = -1;
                        TimeSpan timeETA = new TimeSpan();
                        int animatedProgressCount = 0;

                        Utility.LogMessage("===");
                        Utility.LogMessage(string.Format("Downloading {0} of size {1} bytes ...", remoteFile.Name, Utility.NumericSeparator(remoteFile.Size)));

                        // reset stopwatch
                        stopWatchBitRate.Reset();
                        stopWatchBitRate.Start();

                        do
                        {
                            // initialize and clear the buffer
                            buffer = new Byte[Utility.CONST_BYTETOREAD];

                            // Read data into the buffer.
                            length = s.Read(buffer, 0, Utility.CONST_BYTETOREAD);

                            // and write it out to the response's output stream
                            destFile.Write(buffer, 0, length);

                            // send buffer data to file system
                            destFile.Flush(true);

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

                            // get download progress and display bar
                            double progressPercentage = (double)totalByteTranferred / remoteFile.Size * 100;
                            string progressBar = Utility.AnimatedProgressBar(ref animatedProgressCount, progressPercentage);

                            Console.Write(
                                string.Format("\r {3} {2:f1}% {0:f2}/{4:f2}MB @ {1:f2}KB/s {6} {8}{5}",
                                    Utility.ByteToMByte(totalByteTranferred),
                                    averageBitRate,
                                    progressPercentage,
                                    progressBar,
                                    fileSizeMB,
                                    new String(' ', 10),
                                    Utility.TimeSpanToString(timeETA),
                                    Utility.TimeSpanToString(stopWatchPerDownload.Elapsed),
                                    length));

                        } while (length > 0); //Repeat until all data are read
                    }
                }
                catch (Exception ex)
                {
                    Utility.LogMessage(ex.StackTrace);
                }

                stopWatchPerDownload.Stop();

                string logEnd = String.Format("Downloaded {0} bytes in {1} => {2}",
                    Utility.NumericSeparator(totalByteTranferred), Utility.TimeSpanToString(stopWatchPerDownload.Elapsed), remoteFile.Name);

                Utility.LogMessage(string.Empty);
                Utility.LogMessage(logEnd);

                // update modified date/time
                FileInfo fiDownload = new FileInfo(destinationFilename);
                fiDownload.LastWriteTime = remoteFile.Modified.AddHours(-1); // offset by one hour                
            }
        }       

        public static void Upload()
        {
            // todo :
        }
    }
}

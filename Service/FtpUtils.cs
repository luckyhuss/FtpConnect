using FluentFTP;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace FtpConnect.Service
{
    class FtpUtils
    {
        /// <summary>
        /// FTP Connection
        /// </summary>
        /// <returns></returns>
        private static FtpClient Connect(string connectionUrl, string workingDirectory)
        {
            FtpClient ftpConnection = new FtpClient();

            // example : user¤pwd¤address¤ftp
            string[] details = connectionUrl.Split('¤');
            string protocol = details[3];

            ftpConnection.Host = details[2];
            ftpConnection.Credentials = new NetworkCredential(details[0], details[1]);

            ftpConnection.DataConnectionType = FtpDataConnectionType.AutoPassive;
            ftpConnection.SocketKeepAlive = true;            
            ftpConnection.ValidateCertificate += FtpConnection_ValidateCertificate;

            switch (protocol)
            {
                case "ftp":
                    ftpConnection.EncryptionMode = FtpEncryptionMode.None;
                    ftpConnection.SslProtocols = System.Security.Authentication.SslProtocols.Default;
                    break;
                case "ftpes":
                    ftpConnection.EncryptionMode = FtpEncryptionMode.Explicit;
                    ftpConnection.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                    break;
                case "ftps":
                    ftpConnection.EncryptionMode = FtpEncryptionMode.Implicit;
                    ftpConnection.SslProtocols = System.Security.Authentication.SslProtocols.Tls;
                    break;
                case "sftp":
                    ftpConnection.EncryptionMode = FtpEncryptionMode.None;
                    ftpConnection.SslProtocols = System.Security.Authentication.SslProtocols.Default;
                    ftpConnection.Port = 22;
                    break;
            }

            Utility.LogMessage(String.Format("Connecting to server {0} with {1} ..", ftpConnection.Host, ftpConnection.Credentials.UserName));

            try
            {
                ftpConnection.Connect();
            }
            catch (Exception ex)
            {
                Utility.LogMessage("Error connecting to server. " + ex.Message);
                return null;
            }

            if (ftpConnection.IsConnected)
                Utility.LogMessage("Connected to server");

            // set working dir for FTP server
            ftpConnection.SetWorkingDirectory(workingDirectory);

            return ftpConnection;
        }

        private static void FtpConnection_ValidateCertificate(FtpClient control, FtpSslValidationEventArgs e)
        {
            e.Accept = true;
        }

        /// <summary>
        ///  Download files from FTP server
        /// </summary>
        /// <param name="ftpConnection"></param>
        public static bool Download()
        {
            Stopwatch stopWatchBitRate = new Stopwatch();
            Stopwatch stopWatchPerDownload = new Stopwatch();

            long totalByteTranferred = 0;
            long subTotalByteTranferred = 0;
            List<float> bitRates = null;

            FtpClient ftpConnectionDownload = Connect(
                ConfigurationManager.AppSettings["Download.Url"], 
                ConfigurationManager.AppSettings["Download.RemoteDir"]);
            FtpClient ftpConnectionUpload = null;
            
            if (ftpConnectionDownload == null) return false;

            Utility.LogMessage(String.Format("Listing files in {0} ..", ftpConnectionDownload.GetWorkingDirectory()));
            var remoteFiles = ftpConnectionDownload.GetListing();
            
            Utility.LogMessage(
                string.Format("File listing completed :\n\t{0}",
                    string.Join(Environment.NewLine + "\t",
                        remoteFiles.Select(
                            ftpFile => string.Format("{2} [{1:f2} MB] for {0}",
                                ftpFile.Name, Utility.ByteToMByte(ftpFile.Size), ftpFile.Modified.AddHours(-1))).ToList())));

            // Buffer to read bytes in chunk size specified above
            byte[] buffer = null;

            foreach (var remoteFile in remoteFiles)
            {
                // reset
                totalByteTranferred = 0;
                stopWatchPerDownload.Reset();
                stopWatchPerDownload.Start();
                bitRates = new List<float>();
                bool percentageFlag = false;
                ftpConnectionUpload = null;

                double fileSizeMB = Utility.ByteToMByte(remoteFile.Size);
                string destinationFilename = Path.Combine(ConfigurationManager.AppSettings["Download.LocalDir"], remoteFile.Name);
                string destinationFilenameRemote = string.Empty;

                // archive LOCAL files for past two days
                ArchiveFiles(destinationFilename);
                
                // check if Aspin or Spid file
                if (remoteFile.Name.ToLower().Contains("aspin"))
                {
                    // aspin file
                    ftpConnectionUpload = Connect(
                        ConfigurationManager.AppSettings["Upload.Aspin.Url"],
                        ConfigurationManager.AppSettings["Upload.Aspin.RemoteDir"]);
                    
                }
                else // if (remoteFile.Name.ToLower().Contains("spid"))
                {
                    // spid or other files
                    ftpConnectionUpload = Connect(
                        ConfigurationManager.AppSettings["Upload.Spid.Url"],
                        ConfigurationManager.AppSettings["Upload.Spid.RemoteDir"]);
                }

                try
                {
                    Stream streamUpload = null;

                    if (ftpConnectionUpload != null)
                    {
                        destinationFilenameRemote = ftpConnectionUpload.GetWorkingDirectory() + "/" + remoteFile.Name;

                        // archive REMOTE files for past two days
                        ArchiveFiles(destinationFilenameRemote, true, ftpConnectionUpload);

                        // open stream now, after rename
                        streamUpload = ftpConnectionUpload.OpenWrite(destinationFilenameRemote, FtpDataType.Binary);
                    }

                    using (Stream s = ftpConnectionDownload.OpenRead(remoteFile.FullName, FtpDataType.Binary))
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
                        Utility.LogMessage(string.Format("Downloading {0} of size {1} bytes ..", remoteFile.Name, Utility.NumericSeparator(remoteFile.Size)));

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

                            if (streamUpload != null)
                            {
                                // write to upload ftp server
                                streamUpload.Write(buffer, 0, length);
                                //streamUpload.Flush();
                            }

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

                            string progressDetails =
                                string.Format("{3} {2:f1}% {0:f2}MB / {4:f2}MB @ {1:f2} KB/s - ETA : {6} / Run : {7}{5}",
                                    Utility.ByteToMByte(totalByteTranferred),
                                    averageBitRate,
                                    progressPercentage,
                                    progressBar,
                                    fileSizeMB,
                                    new string(' ', 10),
                                    Utility.TimeSpanToString(timeETA),
                                    Utility.TimeSpanToString(stopWatchPerDownload.Elapsed));

                            Console.Write("\r " + progressDetails);

                            if (!percentageFlag && Math.Floor(progressPercentage) % 10 == 0)
                            {
                                Utility.LogMessage(progressDetails, false);
                                // write only once
                                percentageFlag = true;
                            }
                            else if (Math.Floor(progressPercentage) % 10 != 0)
                            {
                                percentageFlag = false;
                            }

                        } while (length > 0); //Repeat until all data are read
                    }

                    if (streamUpload != null)
                    {
                        streamUpload.Flush();
                        streamUpload.Close();
                    }
                }
                catch (Exception ex)
                {
                    Utility.LogMessage(String.Format("{0} - {1}", ex.Message, ex.StackTrace));
                    return false;
                }

                stopWatchPerDownload.Stop();

                string logEnd = string.Format("Downloaded {0} bytes in {1} => {2}",
                    Utility.NumericSeparator(totalByteTranferred), Utility.TimeSpanToString(stopWatchPerDownload.Elapsed), remoteFile.Name);

                Console.WriteLine();
                Utility.LogMessage(logEnd);

                // update modified date/time
                FileInfo fiDownload = new FileInfo(destinationFilename);
                fiDownload.LastWriteTime = remoteFile.Modified.AddHours(-1); // offset by one hour

                if (ftpConnectionUpload != null)
                    ftpConnectionUpload.Disconnect();
            }

            ftpConnectionDownload.Disconnect();

            Utility.LogMessage(string.Format("{0} successful downloads", remoteFiles.Length));
            return true;
        }

        private static void ArchiveFiles(string destinationFilename, bool ftpFile = false, FtpClient ftpConnectionUpload = null)
        {
            Utility.LogMessage(string.Format("Archiving {0} file : {1}", ftpFile ? "REMOTE" : "LOCAL", destinationFilename));

            if (ftpFile && ftpConnectionUpload != null)
            {
                // REMOTE files
                if (ftpConnectionUpload.FileExists(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER))
                {
                    // _EPPlus 4.1 Sample.zip_older exists, therefore _old exists
                    // delete _older
                    ftpConnectionUpload.DeleteFile(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);

                    try
                    {
                        // rename _old -> older
                        ftpConnectionUpload.Rename(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);
                    }
                    catch { }

                    try
                    {
                        // rename new -> _old
                        ftpConnectionUpload.Rename(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                    }
                    catch { }
                }
                else if (ftpConnectionUpload.FileExists(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD))
                {
                    // _EPPlus 4.1 Sample.zip_old exists but not _older
                    try
                    {
                        // rename _old -> _older
                        ftpConnectionUpload.Rename(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);
                    }
                    catch { }

                    try
                    {
                        // rename new -> _old
                        ftpConnectionUpload.Rename(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        // create first _old
                        ftpConnectionUpload.Rename(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                    }
                    catch { }
                }
            }
            else
            {
                // LOCAL files
                if (File.Exists(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER))
                {
                    // _EPPlus 4.1 Sample.zip_older exists, therefore _old exists
                    // delete _older
                    File.Delete(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);

                    try
                    {
                        // rename _old -> older
                        File.Move(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);
                    }
                    catch { }

                    try
                    {
                        // rename new -> _old
                        File.Move(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                    }
                    catch { }
                }
                else if (File.Exists(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD))
                {
                    // _EPPlus 4.1 Sample.zip_old exists but not _older
                    try
                    {
                        // rename _old -> _older
                        File.Move(destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLDER);
                    }
                    catch { }

                    try
                    {
                        // rename new -> _old
                        File.Move(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        // create first _old
                        File.Move(destinationFilename, destinationFilename + Utility.CONST_PREFIX_ARCHIVE_OLD);
                    }
                    catch { }
                }
            }
        }

        public static void Upload()
        {
            
        }
    }
}

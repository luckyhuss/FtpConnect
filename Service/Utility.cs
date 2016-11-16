using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace FtpConnect.Service
{
    class Utility
    {
        // How many bytes to read at a time and send to the client
        public const int CONST_BYTETOREAD = 100000;

        // archiving of files
        public const string CONST_PREFIX_ARCHIVE_OLD = "_old";
        public const string CONST_PREFIX_ARCHIVE_OLDER = "_older";

        public static double ByteToMByte(long fileSize)
        {
            return fileSize / (1024.0f * 1024.0f);
        }

        public static string NumericSeparator(long number)
        {
            NumberFormatInfo nfi = new NumberFormatInfo { NumberGroupSeparator = ",", NumberDecimalDigits = 0 };
            return number.ToString("n", nfi);
        }

        public static string TimeSpanToString(TimeSpan timeSpan)
        {
            return timeSpan.ToString(@"hh\hmm\mss\s");
        }

        public static float CalculateBitRate(Stopwatch stopWatchBitRate, long subTotalByteTranferred)
        {
            // KB/s
            // transfer rate = (kiloByte / elapsedSeconds)
            // kiloByte = (byte / 1024.0f)
            // elapsedSeconds = ElapsedMilliseconds / 1000

            return ((subTotalByteTranferred / 1024.0f) / ((float)stopWatchBitRate.Elapsed.TotalMilliseconds / 1000));            
        }

        public static string AnimatedProgressBar(ref int animatedProgressCount, double progressPercentage)
        {
            string animatedProgress = @"/-\|.";

            // [....................]
            if (progressPercentage == 100)
                animatedProgressCount = 4;
            else if (animatedProgressCount == 4)
                animatedProgressCount = 0;

            string progressBarDot = new String('.', (int)progressPercentage / 5); // ....
            string progressBarSpace = new String(' ', (20 - (int)progressPercentage / 5)); // space
            string progressBar = String.Format("[{0}{1}{2}]", progressBarDot, animatedProgress[animatedProgressCount], progressBarSpace);

            animatedProgressCount++;

            return progressBar;
        }

        public static void LogMessage(string message)
        {
            // write on console
            Console.WriteLine(message);

            // add log file
            File.AppendAllText(
                ConfigurationManager.AppSettings["Log.Filename"],
                string.Format("{0} - {1}{2}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), message, Environment.NewLine), Encoding.Default);
        }
    }
}

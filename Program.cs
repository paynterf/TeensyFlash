using System;
using System.IO.Ports;
using System.Threading;
using System.Timers;

/*
Small console app to facilitate over-the-air (OTA) updates to a Teensy 3.x/4.x controller,
using VS2019 with the Visual Micro extension as the Arduino IDE. It is called by a post-build
'hook' statement in a file called 'board.txt' located in whatever Teensy program is 
being updated.  This app does the following:

 - Extract the project path and selected COMPORT number from the arguments to the call to Main()
 - Opens a UART serial port connection to the Teensy, typically one provided by a BT adaptor
   operating in 'pass-through' mode. The serial port COMPORT number is passed into this app
   as an argument.
 - Sends whatever command is required to put the existing Teensy firmware into 'update' mode
 - Using the path of the updating program (passed in as an argument), locates the .HEX file 
   associated with the project, and sends it's contents to the Teensy, one line at a time, counting
   lines and confirming checksums line-by-line
 - Compares the number of lines sent to the Teensy with the number of lines received by the Teensy,
   and if there is a match, allows the Teensy update process to complete; otherwise aborts
*/

namespace TeensyFlash
{  
    class Program
    {
        const string startCmdStr = "U"; //used in sketch's 'GetUserInput()' to start upload sequence
        static string rcvStr = string.Empty;
        private static System.Timers.Timer aTimer;
        private static bool bTimedOut;

        static void Main(string[] args)
        {
            //Extract the build path and selected COMPORT number from the arguments to the call to Main()
            Console.WriteLine("Teensy Flash Console");
            Console.WriteLine("Number of arguments in args = {0}\n", args.Length);
            int argindex = 0;
            string comPortStr = string.Empty;
            foreach (var item in args)
            {
                Console.WriteLine(item);
                if (item.Contains("COM"))
                {
                    comPortStr = args[argindex];
                }

                argindex++;
            }

            string build_path = args[0];
            string projectName = args[args.Length - 1];
            projectName = projectName.Substring(0, projectName.Length - 4); //remove extension
            build_path = build_path.Replace("\"", string.Empty).Trim();
            Console.WriteLine("path = {0}", build_path);
            Console.WriteLine("comport = {0}", comPortStr);
            Console.WriteLine("build name = {0}", projectName);
            Console.WriteLine("path to HEX file = {0}", build_path + "\\" + projectName + ".hex");

            //Find and open .HEX file - maybe pre-process to confirm checksums and get number of bytes.
            try
            {
                Console.WriteLine("just before file read");
                string[] lines = System.IO.File.ReadAllLines(build_path + "\\" + projectName + ".hex");
                Console.WriteLine("Read {0} lines from hex file", lines.Length);
                int numlines = 0;

                //Open UART serial port connection to the Teensy, typically one provided by a BT adaptor
                //Send whatever command is required to put the existing Teensy firmware into 'update' mode
                try
                {
                    SerialPort _serport = new SerialPort(comPortStr, 115200);
                    _serport.Open();
                    _serport.DiscardOutBuffer();
                    _serport.DiscardInBuffer();
                    Thread.Sleep(100);
                    Console.WriteLine(startCmdStr);
                    _serport.Write(startCmdStr);

                    rcvStr = string.Empty;
                    aTimer = new System.Timers.Timer();
                    aTimer.Interval = 5000;
                    aTimer.Elapsed += OnTimedEvent;

                    aTimer.Start();
                    while (!rcvStr.Contains("waiting") && !bTimedOut)
                    {
                        if (_serport.BytesToRead > 0)
                        {
                            rcvStr = _serport.ReadLine();
                        }
                    }
                    aTimer.Stop();

                    if (bTimedOut)
                    {
                        Console.WriteLine("Timed out waiting for 'waiting' response from Teensy");
                    }
                    else
                    {
                        //if we get to here, the Teensy is ready to receive HEX file contents
                        Thread.Sleep(500);
                        numlines = 0;
                        foreach (string item in lines)
                        {
                            numlines++;
                            _serport.WriteLine(item);
                        }
                        Console.WriteLine("total lines = {0}", numlines);

                        //now we wait for Teensy to emit "hex file: xx lines xx bytes..." and then "enter xx to flash..."
                        aTimer.Start();
                        while (!rcvStr.Contains("hex file:") && !bTimedOut)
                        {
                            if (_serport.BytesToRead > 0)
                            {
                                rcvStr = _serport.ReadLine();
                            }
                        }
                        aTimer.Stop();
                        aTimer.Dispose();

                        if (bTimedOut)
                        {
                            Console.WriteLine("Timed out waiting for response from Teensy");
                        }
                        else
                        {
                            //extract number of lines from Teensy string, and compare with numlines.
                            //If they match, then send the number back to Teensy to complete the update.
                            //Otherwise, send '0' to abort

                            int colonIdx = rcvStr.IndexOf(':');
                            int lineIdx = rcvStr.IndexOf("lines");
                            string compareStr = rcvStr.Substring(colonIdx+1, lineIdx - colonIdx-1);
                            compareStr = compareStr.Trim();
                            int numTeensyLines = Convert.ToInt16(compareStr);

                            Console.WriteLine("sent {0} teensy replied {1}",numlines, numTeensyLines);
                            if (numTeensyLines == numlines)
                            {
                                Console.WriteLine("numlines {0} matches numTeensyLines {1} - send confirmation",
                                    numlines, numTeensyLines);

                                _serport.WriteLine(compareStr);
                            }
                        }


                        //Send [project_name].Hex file to the Teensy, confirming checksum and counting lines
                        //If sent bytes match received bytes, allow the update to complete.
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.Message);
                }// end CATCH portion of TRY/CATCH block
            }
            catch (Exception e)
            {
                Console.WriteLine("hex file read failed with message:" + e.Message);
            }
            aTimer.Dispose();


        }

        static string chksum(string input)
        {
            int TwosComplement(string s)
            {
                if (s.Length % 2 != 0)
                    throw new FormatException(nameof(input));

                var checksum = 0;

                for (var i = 0; i < s.Length; i += 2)
                {
                    var value = int.Parse(s.Substring(i, 2), System.Globalization.NumberStyles.AllowHexSpecifier);

                    checksum = (checksum + value) & 0xFF;
                }

                return 256 - checksum & 0xFF;
            }

            //return string.Concat(":", input, " ", TwosComplement(input).ToString("X2"));
            return TwosComplement(input).ToString("X2");
        }
        private static void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new System.Timers.Timer(2000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            //aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            Console.WriteLine("The Elapsed event was raised at {0:HH:mm:ss.fff}",
                              e.SignalTime);
            bTimedOut = true;
        }
    }

}

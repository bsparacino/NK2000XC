#region Namespace Inclusions
using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Diagnostics;
#endregion

namespace NK2000XC
{
    class Times
    {
        // Data read in from the watch
        List<byte> myData = new List<byte>();

        // The main control for communicating through the RS-232 port
        private SerialPort comport = new SerialPort();

        public Times(String comPortName)
        {
            // Data received event handler
            comport.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);

            // Set the port's settings
            comport.PortName = comPortName;
            comport.BaudRate = 4800;
            comport.DataBits = 8;
            comport.StopBits = StopBits.One;
            comport.Parity = Parity.None;            
            comport.ReceivedBytesThreshold = 4;
            comport.DtrEnable = true;
            comport.RtsEnable = true;

            // Open the com port
            try
            {                
                comport.Open();
            }
            catch (IOException) { }

            // Start processing data from the watch
            Thread processDataThread = new Thread(processData);
            processDataThread.Start();

            // Request data from the watch
            RequestData();
        }

        // Process the watch data
        public void processData()
        {
            int one_seventy_hit = 0; // Detect when we reach rows with 170 values
            int list_cnt = 0; // The number of races or segments
            List<List<int>> times = new List<List<int>>(); // List of times
            int numTimes = -1; // Number of times in the last segment
            int numSegments = -1; // Number of segments
            int foundSegmentsTimes = 0; // Have we found the real number of segments and times
            int continueLoop = 1; // How we break out of the process data loop

            while (continueLoop == 1)
            {
                // Process 4 bytes at a time
                if (myData.Count >= 4)
                {
                    Debug.WriteLine(myData[0] + " " + myData[1] + " " + myData[2] + " " + myData[3]);


                    // Process row that starts with a 0
                    if (myData[0] == 0)
                    {
                        // Total milliseconds
                        int time_ms = int.Parse(myData[3].ToString()) + int.Parse(myData[2].ToString()) * 256 + int.Parse(myData[1].ToString()) * 65536;

                        //Console.WriteLine(time_ms);

                        // If we are still reading times
                        if (one_seventy_hit == 0)
                        {
                            // Add the time to the list
                            times[list_cnt - 1].Add(time_ms);                            
                        }
                        // If 0 no longer specifies a time then it may signify the number of times in the last segment
                        else if (foundSegmentsTimes == 0)
                        {
                            // The third byte contains the number of segments
                            // The fourth byte contains the number of times
                            // It seems that there can be multiple of these after the 170 lines
                            // and that the second last line is the line we care about
                            int numSegmentsByte = int.Parse(myData[1].ToString());
                            int numTimesByte = int.Parse(myData[3].ToString());                            

                            // Ignore the line if it says 0 times
                            if (numTimesByte != 0)
                            {
                                numSegments = numSegmentsByte;
                                numTimes = numTimesByte;                                
                            }
                            else
                            {
                                foundSegmentsTimes = 1;
                            }
                        }

                        // We have processed this data, remove it from the list
                        myData.RemoveRange(0, 4);
                    }
                    // Process row that starts with a 170, we don't care about these rows
                    else if (myData[0] == 170)
                    {
                        one_seventy_hit = 1;

                        // We have processed this data, remove it from the list
                        myData.RemoveRange(0, 4);
                    }
                    // Process row that signifies a new segment
                    //else if (myData[0] == 255 && myData[1] == 32 && myData[2] == 3 && myData[3] == 18)
                    else if (myData[0] == 255 && myData[1] == 32)
                    {
                        // Remove this data from the list
                        myData.RemoveRange(0, 4);

                        // If we are still reading times
                        if (one_seventy_hit == 0)
                        {
                            // Add a new segment to the list
                            times.Add(new List<int>());
                            list_cnt++;
                        }
                        // No longer reading times so we can now process the times and write them to a file
                        else
                        {
                            // Create a writer and open the file
                            TextWriter file = new StreamWriter("C:/times.txt");
                            TextWriter file2 = new StreamWriter("C:/times2.txt");

                            // Counter for which list segment we are on
                            int list_cnt2 = 0;

                            // Loop through each list segment
                            foreach (var timeList in times)
                            {
                                // If we are on the final list segment
                                if (list_cnt2 == numSegments-1)
                                {
                                    // Counter for which time we are on
                                    int timeCnt = 0;

                                    // Loop through each time
                                    foreach (int time in timeList)
                                    {
                                        timeCnt++;

                                        // If over the number of times then break out
                                        if (timeCnt > numTimes)
                                        {
                                            break;
                                        }
                                        // Otherwise write out the time
                                        else
                                        {
                                            file.WriteLine(list_cnt2 + "," + time);
                                            file2.WriteLine(list_cnt2 + "," + new DateTime(TimeSpan.FromMilliseconds(time * 10).Ticks).ToString("HH:mm:ss.ff"));
                                        }
                                    }
                                    list_cnt2++;                                    
                                }
                                // If we are not on the last list segment
                                else if (list_cnt2 < numSegments-1)
                                {
                                    // Loop through each time
                                    foreach (int time in timeList)
                                    {
                                        // Write out the time
                                        file.WriteLine(list_cnt2 + "," + time);
                                        file2.WriteLine(list_cnt2 + "," + new DateTime(TimeSpan.FromMilliseconds(time * 10).Ticks).ToString("HH:mm:ss.ff"));
                                    }
                                    list_cnt2++;
                                }
                            }

                            file.Close();
                            file2.Close();

                            // Stop looping, we are done
                            continueLoop = 0;
                        }

                    }
                    // Throw away all other rows
                    else
                    {
                        // Remove this data from the list
                        myData.RemoveRange(0,4);
                    }
                }
                else
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        // This method will be called when there is data waiting in the port's buffer
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // If the com port has been closed, do nothing
            if (!comport.IsOpen) return;
   
            // Obtain the number of bytes waiting in the port's buffer
            int bytes = comport.BytesToRead;

            // Create a byte array buffer to hold the incoming data
            byte[] buffer = new byte[bytes];

            // Read the data from the port and store it in our buffer
            comport.Read(buffer, 0, bytes);

            // Add each byte to the data buffer
            foreach (byte b in buffer)
            {
                myData.Add(b);
            }

        }

        // Initialize a request for data from the watch
        private void RequestData()
        {           
            try
            {
                // Init string to send to the watch
                byte[] data = new byte[] { 85, 85, 85, 85, 85, 85, 85, 85, 85, 85 };

                // Send the binary data out the port
                comport.Write(data, 0, data.Length);
            }
            catch (FormatException) { }
        }

    }
}

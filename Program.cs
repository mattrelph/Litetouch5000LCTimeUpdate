/*
 * Litetouch 5000LC CCU Time Update Utility
 * v 0.1
 * 
 * This utility updates the time on a Litetouch 5000LC CCU.
 * XXX.XXX.XXX.XXX = First argument is the Target CCU Host Address (IPv4 Address, FQDN, etc)
 * -c = Use Computer Clock
 * -i = Use Internet Time Service (time.nist.gov) (Default)
 * -z = Check CCU Clock only. (No Update)
 * 
 * 
5000LC UART Protocol Commands (Carriage return = end of command)
Command:	Get Clock
Format:	R,DGCLK
Return:	R,RQRES,DGCLK,yyyymmddhhmmss
yyyy – year
mm – month
dd – day
hh – hours
mm – minutes
ss - seconds
Description:	Requests the date-time.

 
Command:	Set Clock
Format:	R,DSCLK,yyyymmddhhmmss	see Get Clock for details
Return:	none
Description:	Sets the clock with the specified date-time.
 * 
 * Litetouch Com Port is 10001  and uses TCP protocol
 * 
 * */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;



namespace Litetouch5000LCTimeUpdate
{
    class Program
    {

        static bool debugFlag;

        static void Main(string[] args)
        {


            debugFlag = true;
            Console.WriteLine("Litetouch 5000LC CCU Time Update Utility\n");
            if (args.Length == 0)
            {
                Console.WriteLine("This utility updates the time on a Litetouch 5000LC CCU.\n");
                
                Console.WriteLine("XXX.XXX.XXX.XXX = First argument is the Target CCU Host Address (IPv4 Address, FQDN, etc)");
                Console.WriteLine("-c = Use Computer Clock");
                Console.WriteLine("-i = Use Internet Time Service (time.nist.gov) (Default)");
                Console.WriteLine("-z = Check CCU Clock only. (No Update)");

                Console.WriteLine("Example Usage: \n\tLitouch5000LCTimeUpdate 192.168.1.10 -i\n\tUpdates the CCU at 192.168.1.10 with time.nist.gov");
                
            }
            else
            {
                //First argument should be a network or web address
                //Let's attempt to gather that
                string targetCCUaddress = getIPAddress(args[0]);
                if(debugFlag)
                {
                    Console.WriteLine("Target CCU:" + targetCCUaddress);
                }
                //If there is a second argument, it should specify the update type
                bool noUpdate = false;
                bool internetUpdate = true;
                if (args.Length >=2)
                {
                    switch (args[1])
                    {
                        case "-c":
                            {
                                internetUpdate = false;
                                break;
                            }
                        case "-i":
                            {
                                internetUpdate = true;
                                break;
                            }
                        case "-z":
                            {
                                noUpdate = true;
                                break;
                            }
                    }
                }
                //We ignore further arguments


                //Now we grab the CCU's reported time
                AsynchronousClient targetCCU = new AsynchronousClient();
                targetCCU.StartClient(targetCCUaddress, 10001);     //Default Port for Litetouch Control = 10001

                //Send "GET CLOCK" Command to CCU 
                //Format = R,DGCLK
                string commandString = "R,DGCLK\r";
                string responseString = "";
                targetCCU.SendCommand(commandString);
                responseString = targetCCU.ReceiveResponse();
                if (debugFlag)
                {
                    Console.WriteLine("Command Sent: " + commandString);
                    Console.WriteLine("Response Received: " + responseString);
                }


                //DateTime oldTime = getCCUTime(targetCCUaddress);

                DateTime oldTime = convertCCUTimeResponse(responseString);
                Console.WriteLine("Old CCU Time:" + oldTime.ToString("yyyy-MM-dd HH:mm:ss"));


                //If we update, we get time from our sources, and then push to CCU
                DateTime newTime= DateTime.Parse("1900-01-01");  //Pre-assigned clearly wrong date, in case anything goes wrong
                if (!noUpdate)
                {
                    if (internetUpdate)
                    {
                        newTime = getInternetTime();
                        if (debugFlag)
                        {
                            Console.WriteLine("NTP Server Time:" + newTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                        
                    }
                    else
                    {
                        newTime = getComputerTime();
                        if (debugFlag)
                        {
                            Console.WriteLine("Computer Clock Time:" + newTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                    }
                    commandString = convertNewCCUTime(newTime);

                    //Send "SET CLOCK" Command to CCU 
                    //Format = R,DSCLK,yyyymmddhhmmss
                    targetCCU.SendCommand(commandString);
                    if (debugFlag)
                    {
                        Console.WriteLine("Time Update Command Sent:" + commandString);
                    }

                    //Send "GET CLOCK" Command to CCU 
                    //Format = R,DGCLK
                    commandString = "R,DGCLK\r";
                    responseString = "";
                    targetCCU.SendCommand(commandString);
                    responseString = targetCCU.ReceiveResponse();
                    if (debugFlag)
                    {
                        Console.WriteLine("Command Sent: " + commandString);
                        Console.WriteLine("Response Received: " + responseString);
                    }

                    DateTime updatedTime = convertCCUTimeResponse(responseString);
                    Console.WriteLine("Old CCU Time:" + oldTime.ToString("yyyy-MM-dd HH:mm:ss"));

                    //Calculate the difference between the times. If they are less than a 10s apart, we will accept it
                    TimeSpan duration = updatedTime - newTime;
                    Console.WriteLine("Old CCU Time: " + oldTime);
                    Console.WriteLine("Updated Time: " + newTime);
                    Console.WriteLine("CCU Time: " + updatedTime);
                    Console.WriteLine("Difference:" + duration.TotalSeconds + " seconds");
                    if (duration.TotalSeconds < 10)
                    {
                        Console.WriteLine("Success: " + oldTime);

                    }
                    else
                    {

                        Console.WriteLine("Something went wrong.");

                    }


                }
                targetCCU.StopClient();
                Console.WriteLine("Finished");
                
            }

            if (debugFlag)
            {
                Console.WriteLine("End of Program");
            }
        }

        //Returns the current system clock time
        private static DateTime convertCCUTimeResponse(string oldCCUTIme)
        {
            //Expected LT Protocol Expected Return: R,RQRES,DGCLK,yyyymmddhhmmss\r
            if (debugFlag)
            {
                Console.WriteLine("Converting this string: " + oldCCUTIme);
            }
            
            string formatString = "R,RQRES,DGCLK,yyyymmddhhmmss\r";


            //Sometimes we lose the first R of the message, so let's add that back in, if necessary

            //int i = 0;
            if (oldCCUTIme.Length < formatString.Length)
            {
                oldCCUTIme = formatString.Substring(0, (formatString.Length - oldCCUTIme.Length)) + oldCCUTIme;
                Console.WriteLine("Converting altered string: " + oldCCUTIme);
            }
            //Lets convert this to the "yyyy-MM-dd HH:mm:ss" format

            string parsedTime = oldCCUTIme.Substring(14, 4) + "-" + oldCCUTIme.Substring(18, 2) + "-" + oldCCUTIme.Substring(20, 2);
            parsedTime = parsedTime + ' ' + oldCCUTIme.Substring(22, 2) + ":" + oldCCUTIme.Substring(24, 2) + ":" + oldCCUTIme.Substring(26, 2);
            if (debugFlag)
            {

                Console.WriteLine("Parsed Time: " + parsedTime);
            }

            DateTime parsedDate = DateTime.ParseExact(parsedTime, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

            return parsedDate;
        }

        private static string convertNewCCUTime(DateTime newTime)
        {
           //Command:	Set Clock
           // Format:	R,DSCLK,yyyymmddhhmmss
            
            string newCommand = "R,DSCLK," + newTime.ToString("yyyy") + newTime.ToString("MM") + newTime.ToString("dd") + newTime.ToString("HH") + newTime.ToString("mm") + newTime.ToString("ss") + "\r";
            return newCommand;
        }

        //Returns the current system clock time
        private static DateTime getComputerTime()
        {
            DateTime date = DateTime.Now;

            return date;
        }


        //Returns the current time from the government NTP server
        private static DateTime getInternetTime()
        {
            DateTime internetTimeNow = GetNetworkTime("time.nist.gov");

            return internetTimeNow;
        }




        //Network time fetcher - Connects to NTP servers
        //Derived from https://stackoverflow.com/a/12150289
        public static DateTime GetNetworkTime(string timeServer)
        {
            //default Windows time server
            string ntpServer = timeServer;

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //The UDP port number assigned to NTP is 123
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //NTP uses UDP

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);

                //Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 3000;

                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }


        //Swaps endianness of a byte bitwise
        //Derived from stackoverflow.com/a/3294698/162671
        private static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }


        //Checks if we were passed a host name or IP address. Returns string of resolved IP address
        private static string getIPAddress(string hostAddress)
        {
            string ValidIpAddressRegex = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";

            string ValidHostnameRegex = @"^(([a-zA-Z]|[a-zA-Z][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z]|[A-Za-z][A-Za-z0-9\-]*[A-Za-z0-9])$";


            string CCUAddress = "";

            if (Regex.IsMatch(hostAddress, ValidIpAddressRegex))
            {
                // The string is in IP address format
                CCUAddress = hostAddress;
                if (debugFlag)
                {
                    Console.WriteLine("Appears to be an IP Address: " + CCUAddress);
                }

            }
            else if (Regex.IsMatch(hostAddress, ValidHostnameRegex))
            {
                // The string is in host format

                try
                {
                    var addresses = Dns.GetHostEntry(hostAddress).AddressList;

                    if (debugFlag)
                    {
                        Console.WriteLine("Address mapping: " + hostAddress + " to " + addresses[0].ToString());
                    }

                    CCUAddress = addresses[0].ToString();
                    return CCUAddress;
                }
                catch (Exception e)
                {
                    //DNS Issue?
                    Console.WriteLine(e.ToString());
                    System.Environment.Exit(1);
                    return "error";
                }
                
            }
            return CCUAddress;

        }
    }
}

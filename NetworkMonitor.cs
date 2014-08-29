using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

using RestSharp;
using Newtonsoft.Json;

using System.Threading;
using WMPLib;

using System.Xml;
using System.IO;
using System.Collections.Specialized;

using System.Net.NetworkInformation;
using System.Net;

using log4net;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]


namespace network_monitor
{
    class NetworkMonitor
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod
().DeclaringType);

        //hudson build statuses
        public const String SUCCESS = "SUCCESS";
        public const String EXCEPTION_THROWN = "EXCEPTION THROWN";
        private static Object thisLock = new Object();

        public const String CON_FAILED = "CONNECTION FAILURE";

        public static Boolean isMonitoringEnabled = false;
        public static Boolean beSilentTillNextCommit = false;
        public static Boolean isPingOperationRunning = false;

        private static Dictionary<String, PingStatus> failedPings = new Dictionary<String, PingStatus>();
        private static Dictionary<String, PingStatus> currentPings = new Dictionary<String, PingStatus>();


        //settings
        private static int sleepLength = Convert.ToInt32(ConfigurationManager.AppSettings["monitoringFrequency"]) * 1000;
        private static int pingTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["pingTimeout"]);
        private static NameValueCollection settings = ConfigurationManager.GetSection("pingJobsSection/pingJobs")
                                            as System.Collections.Specialized.NameValueCollection;



        public static void run()
        {
            log.Info("Monitoring started.");

            while (isMonitoringEnabled)
            {
                try
                {
                    //don't rerun the loop if the previous ping operation is still running
                    lock (thisLock) 
                    {
                        //get the lock
                        //isPingOperationRunning = true;

                        //refresh the output
                        Console.SetCursorPosition(0, 0);

                        Console.WriteLine("\t\t ### #################### ### ");
                        Console.WriteLine("\t\t ###  MFG Network Monitor ### ");
                        Console.WriteLine("\t\t ### #################### ### ");
                        Console.WriteLine();

                        //print the time
                        Console.WriteLine("\t\t   Current Time: " + DateTime.Now.ToString("h:mm:ss tt"));
                        Console.WriteLine();


                        if (settings != null)
                        {
                            Console.Write(" Name".PadRight(15));
                            Console.Write("IP".PadRight(15));
                            Console.Write("RTT".PadRight(8));
                            Console.Write("Last Failed Time".PadRight(20));
                            Console.Write("Status".PadRight(1));
                            Console.WriteLine();
                            Console.WriteLine();

                            Console.SetCursorPosition(0, 8);

                            foreach (string urlKey in settings.AllKeys)
                            {


                                PingStatus ping = Ping_URL(urlKey, settings[urlKey]);

                                //add to the current pings list
                                if (currentPings.ContainsKey(urlKey))
                                {
                                    currentPings.Remove(urlKey);
                                }
                                currentPings.Add(urlKey, ping);

                                //do not move on to play the alarm if .isMonitoringEnabled  is false;
                                if (!isMonitoringEnabled)
                                {
                                    break;
                                }

                                Console.Write((" "+urlKey).PadRight(15));
                                Console.Write(settings[urlKey].PadRight(15));
                                Console.Write(Convert.ToString(ping.RoundtripTime).PadRight(8));
                                Console.Write(currentPings[urlKey].lastFailedTime.PadRight(20));
                                if (!ping.result.Equals(SUCCESS))
                                {
                                    String result = ping.result.ToString();
                                    if (result.Length > 21)
                                    {
                                        result = result.Substring(0, 21);
                                    }
                                    Console.Write(result.PadRight(21));
                                }
                                else
                                {
                                    String result = ping.result.ToString();
                                    if (result.Length > 21)
                                    {
                                        result = result.Substring(0, 21);
                                    }
                                    Console.Write(result.PadRight(21));
                                }

                                Console.WriteLine();


                                //if the status is not success 
                                if (!ping.result.Equals(SUCCESS))
                                {

                                    //add the ping to the failed list
                                    if (!failedPings.ContainsKey(urlKey))
                                    {
                                        log.Info("Ping: " + urlKey + " URL: " + ping.pingedAddress + " - Added the ping to the failed list.");
                                        failedPings.Add(urlKey, ping);
                                    }

                                    else
                                    {
                                        log.Info("Ping: " + urlKey + " URL: " + ping.pingedAddress + " - Ping failed, playing alarm.");
                                    }

                                    //play the alarm
                                    AlarmPlayer.Play_Alarm();

                                }
                                //if the status is success
                                else
                                {
                                    //check if the ping has failed in the previous time also
                                    if (failedPings.ContainsKey(urlKey))
                                    {
                                        log.Info("Ping: " + urlKey + " URL: " + ping.pingedAddress + " -  Removing ping form failed list.");

                                        //remove it from the failed list 
                                        failedPings.Remove(urlKey);

                                        //disable the alarm if no pings are failing now
                                        if (failedPings.Count == 0)
                                        {
                                            log.Info("All pings fine, disabling alarm.");
                                            AlarmPlayer.Stop_Alarm();
                                        }
                                    }
                                }
                            }

                        }

                        //release the lock
                        //isPingOperationRunning = false;

                    }

                }
                catch (Exception e)
                {
                    //release the lock
                    isPingOperationRunning = false;
                    log.Info("Exception : Error Message - " + e.Message + System.Environment.NewLine + "Stack Trace - " + e.StackTrace);
                }

                Thread.Sleep(sleepLength);
            }

            //stop the alarm when stopping monitoring
            AlarmPlayer.Stop_Alarm();

            log.Info("Monitoring stopped.");



        }

        /***
         * Clear all the information regarding the previous failed ping requests.
         */
        public static void ClearPingHistory()
        {
            failedPings.Clear();
        }

        /**
         * Sends a ping request to the given IP
         */ 
        private static PingStatus Ping_URL(String urlKey, String url)
        {
            try
            {
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(url, pingTimeout, Encoding.ASCII.GetBytes("TestData"));

                if (reply.Status == IPStatus.Success)
                {
                    //if the last failed time is known
                    if (currentPings.ContainsKey(urlKey))
                    {
                        return new PingStatus(url, reply.RoundtripTime, SUCCESS, currentPings[urlKey].lastFailedTime);
                    }
                    else
                    {
                        return new PingStatus(url, reply.RoundtripTime, SUCCESS, "UNKNOWN");
                    }
                    
                }
                else
                {
                    return new PingStatus(url, reply.RoundtripTime, reply.Status.ToString(), DateTime.Now.ToString("MM/dd h:mm:ss tt"));
                }
            }
            catch (Exception e)
            {
                log.Info("[" + urlKey + "] Exception : Error Message - " + e.Message + System.Environment.NewLine + "Stack Trace - " + e.StackTrace);
                return new PingStatus(url, 0, EXCEPTION_THROWN, DateTime.Now.ToString("MM/dd h:mm:ss tt"));
            }
        }
    }

}
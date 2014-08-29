using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace network_monitor
{
    /**
     * This class is used to store information of pings 
     */
    class PingStatus
    {
        public String pingedAddress { get; set; }
        public long RoundtripTime { get; set; }
        public String result { get; set; }
        public String lastFailedTime { get; set; }

        public PingStatus(String pingedAddress, long RoundtripTime, String result, String lastFailedTime)
        {
            this.pingedAddress = pingedAddress;
            this.RoundtripTime = RoundtripTime;
            this.result = result;
            this.lastFailedTime = lastFailedTime;
        }
    }
}

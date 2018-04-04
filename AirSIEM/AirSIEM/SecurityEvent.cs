using NLog;
using System;
using System.Globalization;

namespace AirSIEM
{
    public enum LogMessageType
    {
        ApacheLog = 1
    }

    public class SecurityEvent
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // See OSSEC Eventinfo struct (src/analysisd/eventinfo.h)
        public string logString = "";
        public string message = "";
        public DateTime time;
        public string srcIP = "";
        public int srcPort = 0;
        public string destIP = "";
        public int destPort = 0;

        // Parse event message and construct SecurityEvent object
        public SecurityEvent(string logMessage, LogMessageType messageType)
        {
            try
            {
                logString = logMessage;

                if (messageType == LogMessageType.ApacheLog)
                {
                    // LogFormat "%h %l %u %t \"%r\" %>s %b \"%{Referer}i\" \"%{User-Agent}i\"" combined
                    // 10.4.0.150 - - [01/Jan/2099:00:00:00 +0000] 
                    // "GET /opencms/proxy/ips/?doc_itself=&backlink=1&nd=102089030&page=1&rdk=0 HTTP/1.1" 200 13783 
                    // "-" "Mozilla/5.0 (compatible; YandexBot/3.0; +http://yandex.com/bots)"
                    string messageString = logMessage.Replace("ApacheConnector:", "");

                    int splitPos;
                    string timeString = "";
                    string requestLine = "";
                    string referer = "";
                    string userAgent = "";

                    // %h - remote host
                    splitPos = messageString.IndexOf(" ");
                    srcIP = messageString.Substring(0, splitPos);
                    messageString = messageString.Substring(splitPos + 1);

                    // %l - identity of the user determined by identd
                    // not used by us
                    splitPos = messageString.IndexOf(" ");
                    messageString = messageString.Substring(splitPos + 1);

                    // %u - the user name identified by HTTP auth
                    // not used by us
                    splitPos = messageString.IndexOf(" ");
                    messageString = messageString.Substring(splitPos + 1);

                    // %t - time the server finished processing the request
                    splitPos = messageString.IndexOf("]");
                    // Cut out the time zone
                    timeString = messageString.Substring(1, splitPos - 7);
                    time = DateTime.ParseExact(timeString, "dd/MMM/yyyy:HH:mm:ss", 
                        CultureInfo.InvariantCulture);
                    messageString = messageString.Substring(splitPos + 3);

                    // %r - request line from the client
                    splitPos = messageString.IndexOf("\"");
                    requestLine = messageString.Substring(0, splitPos);
                    messageString = messageString.Substring(splitPos + 2);

                    // %s - status code 200, 404, etc. (not used by us)
                    // not used by us
                    splitPos = messageString.IndexOf(" ");
                    messageString = messageString.Substring(splitPos + 1);

                    // %b - size of response
                    // not used by us
                    splitPos = messageString.IndexOf(" ");
                    messageString = messageString.Substring(splitPos + 2);

                    // referer
                    // not used by us
                    splitPos = messageString.IndexOf("\"");
                    referer = messageString.Substring(0, splitPos);
                    messageString = messageString.Substring(splitPos + 3);

                    // user agent
                    // not used by us
                    splitPos = messageString.IndexOf("\"");
                    userAgent = messageString.Substring(0, splitPos);

                    message = requestLine;
                }
            }
            catch (Exception ex)
            {
                logger.Warn("SecurityEvent exception: {0}", ex.ToString());
            }
        }

        public override string ToString()
        {
            return String.Format("SecurityEvent object => timestamp=[{0}], source=[{1}], destination=[{2}], message=[{3}]", 
                time.ToString("HH:mm:ss"), srcIP, destIP, message);
        }
    }
}


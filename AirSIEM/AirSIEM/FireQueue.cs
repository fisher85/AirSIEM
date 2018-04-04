using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AirSIEM
{
    public class FireQueue
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Queue<FireQueueItem> Queue;
        public int ID;
        public int timeFrame;
        public int maxSize; 

        public FireQueue(int vID, int vTimeFrame, int vMaxSize = 1000)
        {
            Queue = new Queue<FireQueueItem>();
            ID = vID;
            timeFrame = vTimeFrame;
            maxSize = vMaxSize;
        }

        public void Enqueue(SecurityEvent securityEvent)
        {
            FireQueueItem item = new FireQueueItem(securityEvent.time, securityEvent.srcIP,
                securityEvent.srcPort, securityEvent.destIP, securityEvent.destPort);

            // Remove first item if queue is full
            if (Queue.Count == maxSize)
            {
                logger.Trace("{0}  Dequeue item (>maxSize): {1}", Assistant.GetPadding(), Queue.First().ToString());
                Queue.Dequeue();
            }

            Queue.Enqueue(item);
            logger.Trace("{0}  Enqueue item: {1}", Assistant.GetPadding(), item.ToString());

            // Remove first items if the time difference between the first and last item > timeFrame
            while (true)
            {
                if ((item.time - Queue.First().time).TotalSeconds > timeFrame)
                {
                    logger.Trace("{0}  Dequeue item (>timeFrame): {1}", Assistant.GetPadding(), Queue.First().ToString());
                    Queue.Dequeue();
                }
                else break;
            }

            ShowQueueBrief();
        }

        public void Dequeue()
        {
            Queue.Dequeue();
        }

        public bool CheckIfMatched(SecurityEvent securityEvent, Rule rule)
        {
            // Scan from the end of the queue
            // Increase the counter while the event is within a timeframe
            // If the counter has reached rule.frequency, return true

            logger.Trace(Assistant.GetPadding() + "QueueDictionary.CheckIfMatched start");

            int counter = 0;
            int counterSameSourceIP = 0;

            for (int i = Queue.Count - 1; i >= 0; i--)
            {
                // Check whether an event is within a timeframe
                if ((securityEvent.time - Queue.ElementAt(i).time).TotalSeconds > rule.timeFrame) break;
                counter++;
                logger.Trace("{0}  counter++ => counter=[{1}]", Assistant.GetPadding(), counter);

                // Check if the IP addresses are the same
                if (rule.sameSourceIP)
                {
                    if (securityEvent.srcIP.Equals(Queue.ElementAt(i).srcIP))
                    {
                        counterSameSourceIP++;
                        logger.Trace("{0}  counterSameSourceIP++ => counterSameSourceIP=[{1}]", 
                            Assistant.GetPadding(), counterSameSourceIP);
                    }
                    if (counterSameSourceIP >= rule.frequency) return true;
                }

                // Check the frequency
                if (!rule.sameSourceIP)
                    if (counter >= rule.frequency) return true;
            }

            return false;
        }

        public void ShowQueueBrief()
        {
            logger.Trace(Assistant.GetPadding() + "  " + this.ToString());

            int index = 0;
            foreach (FireQueueItem item in Queue)
            {
                index++;
                logger.Trace(Assistant.GetPadding() + "    " + index + ": " + item.ToString());
            }
        }

        public override string ToString()
        {
            return String.Format("FireQueue object => ID=[{0}], count=[{1}], timeFrame=[{2} sec], maxSize=[{3}]",
                ID, Queue.Count, timeFrame, maxSize);
        }
    }

    public class FireQueueItem
    {
        public DateTime time;
        public string srcIP;
        public int srcPort;
        public string destIP;
        public int destPort;

        public FireQueueItem(DateTime vTime, string vSrcIP, int vSrcPort, string vDestIP, int vDestPort)
        {
            time = vTime;
            srcIP = vSrcIP;
            srcPort = vSrcPort;
            destIP = vDestIP;
            destPort = vDestPort;
        }

        public override string ToString()
        {
            return String.Format("FireQueueItem object => timestamp=[{0}], source=[{1}], destination=[{2}]", 
                time.ToString("HH:mm:ss"), srcIP, destIP);
        }
    }
}

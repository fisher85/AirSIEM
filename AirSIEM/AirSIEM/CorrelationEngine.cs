using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace AirSIEM
{
    public class CorrelationEngine
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int maxCount = 10000; // Maximum value of the matchList counter
        public Dictionary<int, FireQueue> fireDictionary = new Dictionary<int, FireQueue>();
        public Dictionary<int, Rule> ruleList = new Dictionary<int, Rule>();
        public Dictionary<int, int> matchList = new Dictionary<int, int>();

        // Used to pass alerts back for processing
        public delegate void onReceiveAlert(SecurityEvent securityEvent, Rule rule);
        public event onReceiveAlert onAlertReceived;

        public void ParseRuleDir(string rulePath)
        {
            int fileNum = 0;
            int ruleNum = 0;

            logger.Debug("ParseRuleDir start: {0}", rulePath);

            try
            {
                if (!rulePath.Equals(""))
                {
                    string[] fileEntries = Directory.GetFiles(rulePath, "*.xml", SearchOption.AllDirectories);
                    ruleList = new Dictionary<int, Rule>();

                    foreach (string fileName in fileEntries)
                    {
                        FileInfo file = new FileInfo(fileName);
                        List<Rule> fileRuleList = new List<Rule>();
                        ParseRulesFromXML(file.FullName, ref fileRuleList);

                        foreach (Rule rule in fileRuleList)
                            ruleList.Add(rule.ID, rule);

                        fileNum++;
                        ruleNum += fileRuleList.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn("ParseRuleDir exception: {0}", ex.ToString());
            }

            logger.Trace("ParseRuleDir: total {0} files processed", fileNum);
            logger.Trace("ParseRuleDir: total {0} rules processed", ruleNum);
            logger.Debug("ParseRuleDir stop");

            CheckDependencies(ref ruleList);
        }

        public void ParseRulesFromXML(string fileName, ref List<Rule> ruleList)
        {
            // Rules syntax: http://ossec.github.io/docs/syntax/head_rules.html
            // Code example: http://kbyte.ru/ru/Programming/Articles.aspx?id=74&mode=art

            string logString = "";

            try
            {
                logger.Trace("ParseRulesFromXML handles file: " + fileName);

                FileInfo file = new FileInfo(fileName);

                // Rule file may not be valid XML, there may be several root elements
                string xml = File.ReadAllText(fileName);
                xml = "<root>" + xml + "</root>";

                // Process variables
                using (XmlReader preReader = XmlReader.Create(new StringReader(xml)))
                {
                    while (preReader.ReadToFollowing("var"))
                    {
                        string varName = preReader.GetAttribute("name");
                        string varValue = preReader.ReadString();

                        // Replace variable name with its value
                        xml = xml.Replace("$" + varName, varValue);
                    }
                }

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                // Each file combines rules into a group, only the name attribute is allowed
                // src/analysisd/rule.c 
                XmlNode groupNode = doc.SelectNodes("root/group")[0];
                string groupName = groupNode.Attributes["name"].Value.ToString();

                // Parse XML
                foreach (XmlNode node in doc.SelectNodes("root/group/rule"))
                {
                    Rule rule = new Rule();
                    rule.children = new List<int>();

                    rule.ID = (node.Attributes["id"] == null) ? 0 : int.Parse(node.Attributes["id"].Value);
                    logString += rule.ID + " ";

                    rule.level = (node.Attributes["level"] == null) ? 0 : int.Parse(node.Attributes["level"].Value);
                    rule.frequency = (node.Attributes["frequency"] == null) ? 0 : int.Parse(node.Attributes["frequency"].Value);
                    rule.timeFrame = (node.Attributes["timeframe"] == null) ? 0 : int.Parse(node.Attributes["timeframe"].Value);

                    rule.ifSID = (node.SelectSingleNode("if_sid") == null) ? 0 : int.Parse(node.SelectSingleNode("if_sid").InnerText);
                    rule.ifMatchedSID = (node.SelectSingleNode("if_matched_sid") == null) ? 0 : int.Parse(node.SelectSingleNode("if_matched_sid").InnerText);

                    rule.sourceIP = (node.SelectSingleNode("srcip") == null) ? "" : node.SelectSingleNode("srcip").InnerText;
                    rule.destIP = (node.SelectSingleNode("dstip") == null) ? "" : node.SelectSingleNode("dstip").InnerText;
                    rule.sameSourceIP = (node.SelectSingleNode("same_source_ip") == null) ? false : true;

                    rule.match = (node.SelectSingleNode("match") == null) ? "" : node.SelectSingleNode("match").InnerText;
                    rule.description = (node.SelectSingleNode("description") == null) ? "" : node.SelectSingleNode("description").InnerText;
                    rule.parentFile = file.Name;
                    rule.XML = node.OuterXml;

                    ruleList.Add(rule);
                }

                logString = logString.Trim().Replace(" ", ", ");
                logger.Trace("  {0} rules processed: {1}", ruleList.Count, logString);
            }
            catch (Exception ex)
            {
                logger.Warn("ParseRulesFromXML exception (log {0}): {1}", logString, ex.ToString());
            }
        }

        public void CheckDependencies(ref Dictionary<int, Rule> ruleList)
        {
            logger.Debug("CheckDependencies start");

            // Mark the descendants in the rule tree
            foreach (KeyValuePair<int, Rule> ruleKVP in ruleList)
            {
                Rule rule = ruleKVP.Value;

                if (rule.ifSID != 0)
                    ruleList[rule.ifSID].children.Add(rule.ID);
                if (rule.ifMatchedSID != 0)
                    ruleList[rule.ifMatchedSID].children.Add(rule.ID);
            }

            logger.Trace("Dependencies: ");

            foreach (KeyValuePair<int, Rule> ruleKVP in ruleList)
            {
                string logString = "";
                foreach (int item in ruleKVP.Value.children) logString += item + " ";
                logger.Trace("  {0} children => {1}", ruleKVP.Value.ID, logString.Trim().Replace(" ", ", "));
            }

            logger.Debug("CheckDependencies stop");
        }

        public void GenerateQueueList(Dictionary<int, Rule> ruleList,
            ref Dictionary<int, FireQueue> queueDictionary)
        {
            // Create FireQueues for all rules with nonzero frequency
            logger.Debug("GenerateQueueList start");

            try
            {
                foreach (KeyValuePair<int, Rule> ruleKVP in ruleList)
                {
                    // ruleKVP.Value - processed rule
                    if (ruleKVP.Value.frequency == 0) continue;

                    // QueueDictionary.ID = ID from ifMatchedSID element
                    int ID = ruleKVP.Value.ifMatchedSID;

                    // If the ID exists
                    if (queueDictionary.ContainsKey(ID))
                    {
                        // Then update the timeFrame value 
                        queueDictionary[ID].timeFrame = Math.Max(ruleKVP.Value.timeFrame,
                            queueDictionary[ID].timeFrame);
                    }
                    else
                    {
                        // Else create new FireQueue object
                        FireQueue queue = new FireQueue(ID, ruleKVP.Value.timeFrame);
                        queueDictionary.Add(ID, queue);
                    }
                }

                logger.Trace("Created {0} queues:", queueDictionary.Count);
                foreach (KeyValuePair<int, FireQueue> kvp in queueDictionary)
                    logger.Trace("  " + kvp.ToString());
            }
            catch (Exception ex)
            {
                logger.Warn("GenerateQueueList exception: " + ex.ToString());
            }

            logger.Debug("GenerateQueueList stop");
        }

        public void ProcessMessage(SecurityEvent securityEvent)
        {
            // First step of recursion
            // Apply only rules "without parents"
            foreach (KeyValuePair<int, Rule> ruleKVP in ruleList)
            {
                Rule rule = ruleKVP.Value;
                if (!rule.HasParent()) ApplyRule(securityEvent, rule);
            }
        }

        public void ApplyRule(SecurityEvent securityEvent, Rule rule)
        {
            string padding = Assistant.GetPadding();
            logger.Trace("{0}Check rule {1} - {2}", padding, rule.ID, rule.description);

            if (CheckIfMatched(ref securityEvent, ref rule) == true)
            {
                // Perform actions when the rule is triggered
                MatchRule(securityEvent, rule);

                // Process rules recursively
                if (rule.HasChidren())
                {
                    logger.Trace(padding + "  Check the child rules");
                    foreach (int item in rule.children)
                        ApplyRule(securityEvent, ruleList[item]);
                    logger.Trace(padding + "  Check the child rules: OK");
                }
            }
            else
            {
                logger.Trace("{0}Rule {1} not matched", padding, rule.ID);
            }

            logger.Trace("{0}Check rule {1}: OK", padding, rule.ID);
        }

        public bool CheckIfMatched(ref SecurityEvent securityEvent, ref Rule rule)
        {
            string padding = Assistant.GetPadding();

            // Check <if_sid> element => scan dependencies and matchList
            if (rule.ifSID != 0)
            {
                logger.Trace("{0}Check <if_sid>{1}</if_sid>", padding, rule.ifSID);

                if (!matchList.ContainsKey(rule.ifSID)) return false;
                if (matchList[rule.ifSID] == 0) return false;
            }

            // Check <same_source_ip> element
            if (!rule.sourceIP.Equals(""))
            {
                logger.Trace(padding + "  Check <same_source_ip/>");
                if (!securityEvent.srcIP.Equals(rule.sourceIP)) return false;
            }

            // Check <match> element
            if (!rule.match.Equals(""))
            {
                logger.Trace("{0}Check <match>{1}</match>", padding, rule.match);

                // Process logical OR 
                bool check = false;
                string[] parts = rule.match.Split(new Char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string part in parts)
                {
                    if (securityEvent.message.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        check = true;
                        break;
                    }
                }

                if (!check) return false;
            }

            // Check <if_matched_sid> element
            if (rule.ifMatchedSID != 0)
            {
                logger.Trace("{0}Check <if_matched_sid>{1}</if_matched_sid>", padding, rule.ifMatchedSID);

                if (matchList.ContainsKey(rule.ifMatchedSID))
                {
                    if (matchList[rule.ifMatchedSID] == 0) return false;

                    // Scan FireQueue
                    if (!fireDictionary.ContainsKey(rule.ifMatchedSID)) return false;
                    if (fireDictionary[rule.ifMatchedSID].CheckIfMatched(securityEvent, rule))
                    {
                        logger.Trace("{0}  Rule {1} QueueDictionary.CheckIfMatched == TRUE", padding, rule.ifMatchedSID);
                    }
                    else
                    {
                        logger.Trace("{0}  Rule {1} QueueDictionary.CheckIfMatched == FALSE", padding, rule.ifMatchedSID);
                        return false;
                    }
                }
            }

            // Rule matched
            return true;
        }

        public void MatchRule(SecurityEvent securityEvent, Rule rule)
        {
            // Raise an event
            onAlertReceived(securityEvent, rule);

            // If rule is queue tracked, increase a counter
            if (fireDictionary.ContainsKey(rule.ID))
            {
                logger.Trace(Assistant.GetPadding() + "  Matched rule is queue tracked");
                fireDictionary[rule.ID].Enqueue(securityEvent);
            }

            // Save statistics
            if (matchList.ContainsKey(rule.ID))
            {
                matchList[rule.ID]++;
                if (matchList[rule.ID] > maxCount) matchList[rule.ID] = maxCount;
            }
            else
                matchList.Add(rule.ID, 1);
        }

        public void LogStatistics()
        {
            logger.Debug("MatchList stat: ");
            foreach (KeyValuePair<int, int> kvp in matchList)
                logger.Trace("  Rule {0} fires {1} times", kvp.Key, kvp.Value);
        }
    }
}

using System.Collections.Generic;

namespace AirSIEM
{
    public class Rule
    {
        // OSSEC Rules Syntax: http://ossec.github.io/docs/syntax/head_rules.html

        public int ID;
        public int level;
        public int frequency;
        public int timeFrame;
        public int ignore;

        // Matches if the ID has matched, allowed: any rule id
        public int ifSID;
        // Matches if an alert of the defined ID has been triggered in a set number of seconds
        public int ifMatchedSID; 

        public string sourceIP;
        public string destIP;
        public bool sameSourceIP = false;

        public string match;
        public string description;
        public string parentFile;
        public string XML;

        public List<int> children;

        public bool HasParent()
        {
            if (ifSID != 0 || ifMatchedSID != 0) return true;
            else return false;
        }

        public bool HasChidren()
        {
            if (children.Count > 0) return true;
            else return false;
        }
    }
}

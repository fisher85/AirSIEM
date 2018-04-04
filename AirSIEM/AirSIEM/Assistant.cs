using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AirSIEM
{
    public static class Assistant
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static string GetPadding()
        {
            // Get padding for tree-like trace log
            // We use recursive rule processing
            // Thus, each call to ApplyRule and CheckIfMatched will cause padding increase
            int callNum = 0;

            try
            {
                StackTrace stackTrace = new StackTrace();
                StackFrame[] stackFrames = stackTrace.GetFrames();
                
                foreach (var stackFrame in stackFrames)
                {
                    string source = stackFrame.ToString();

                    // Scan stackFrame string for ApplyRule calls
                    int n = 0;
                    string subString = "ApplyRule";
                    while ((n = source.IndexOf(subString, n, StringComparison.InvariantCulture)) != -1)
                    {
                        n += subString.Length;
                        callNum++;
                    }

                    // Scan stackFrame string for CheckIfMatched calls
                    n = 0;
                    subString = "CheckIfMatched";
                    while ((n = source.IndexOf(subString, n, StringComparison.InvariantCulture)) != -1)
                    {
                        n += subString.Length;
                        callNum++;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn("GetPadding exception: " + ex.ToString());
            }

            return "".PadRight((callNum - 1) * 2, ' ');
        }
    }
}


using System;
using System.Runtime.CompilerServices;
namespace TsMap.Helpers.Logger
{
    // SimDeck imports in a separate process; no persistent log or background logging queue.
    public class Logger
    {
        private static readonly Logger _instance = new Logger();
        public static Logger Instance => _instance;
        public int Errors { get; private set; }
        public void Debug(string msg, [CallerMemberName] string callerName="", [CallerFilePath] string callerPath="") { }
        public void Info(string msg, [CallerMemberName] string callerName="", [CallerFilePath] string callerPath="") { }
        public void Warning(string msg, [CallerMemberName] string callerName="", [CallerFilePath] string callerPath="") { }
        public void Error(string msg, [CallerMemberName] string callerName="", [CallerFilePath] string callerPath="") { Errors++; }
    }
}

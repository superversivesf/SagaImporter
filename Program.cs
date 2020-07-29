using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AudioBookOrganizer
{
    class Program
    {
        static void Main(string[] args)
        {
            var types = LoadVerbs();

            Parser.Default.ParseArguments(args, types)
                  .WithParsed(Run)
                  .WithNotParsed(HandleErrors);
        }

        private static Type[] LoadVerbs()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();
        }

        private static void HandleErrors(IEnumerable<Error> errs)
        {
            if (errs.IsVersion())
            {
                Console.WriteLine("Version Request");
                return;
            }

            if (errs.IsHelp())
            {
                Console.WriteLine("Help Request");
                return;
            }
            Console.WriteLine("Parser Fail");
        }

        private static void Run(object obj)
        {
            try
            {
                switch (obj)
                {
                    case InputOptions i:
                        var _inputProcess = new InputProcessor();
                        _inputProcess.Initialize(i);
                        _inputProcess.Execute();
                        break;
                    case OutputOptions o:
                        var _outputProcess = new OutputProcessor();
                        _outputProcess.Initialize(o);
                        _outputProcess.Execute();
                        break;
                    case LookupOptions l:
                        var _lookupProcess = new LookupProcessor();
                        _lookupProcess.Initialize(l);
                        _lookupProcess.Execute();
                        break;
                    case DumpOptions d:
                        var _dumpProcess = new DumpProcessor();
                        _dumpProcess.Initialize(d);
                        _dumpProcess.Execute();
                        break;
                }
            }
            catch (Exception e)
            {
                var st = new StackTrace(e, true);
                var frame = st.GetFrame(0);
                var line = frame.GetFileLineNumber();
                Console.WriteLine($"\nSomething went wrong -> {e.Message}");
                Console.WriteLine($"Filename -> {frame.GetFileName()}");
                Console.WriteLine($"Line Number -> {frame.GetFileLineNumber()}");
                Console.WriteLine($"Stack Trace \n {st.ToString()}");

            }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MemoryDemo
{
    public abstract class Slide
    {
        protected static Random rand = new Random();
        public abstract void Play(bool jump);

        internal static Slide Text(string message)
        {
            return new TextSlide(message);
        }

        internal static Slide Code(string code)
        {
            return new CodeSlide(code);
        }

        internal static Slide Command(string process, string arguments)
        {
            return new CommandSlide(process, arguments);
        }

        internal static Slide Inspect()
        {
            return new InspectSlide();
        }

        protected static void SimulatedTyping(string message, bool jump, bool suppressColour = false)
        {
            foreach (var word in message.Split(' '))
            {
                if (word.Length >= Console.BufferWidth - Console.CursorLeft)
                {
                    Console.WriteLine();
                }

                foreach (var c in word)
                {
                    if (!suppressColour)
                    {
                        switch (c)
                        {
                            case '`':
                                ToggleColour(ConsoleColor.DarkYellow);
                                continue;
                            case '*':
                                ToggleColour(ConsoleColor.DarkMagenta);
                                continue;
                            case '_':
                                ToggleColour(ConsoleColor.DarkCyan);
                                continue;
                        }
                    }

                    Console.Write(c);

                    if (!jump)
                    {
                        Thread.Sleep(rand.Next(0, 16));
                    }
                }

                Console.Write(' ');
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        protected string FindSolutionDirectory()
        {
            return FindSolutionDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        protected string FindSolutionDirectory(string startPath)
        {
            if (Directory.EnumerateFiles(startPath, "*.sln").Any())
            {
                return startPath;
            }

            return FindSolutionDirectory(Path.GetDirectoryName(startPath));
        }

        private static void ToggleColour(ConsoleColor desiredColour)
        {
            if (Console.ForegroundColor == desiredColour)
            {
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = desiredColour;
            }
        }
    }

    public class CommandSlide : Slide
    {
        private readonly string process;
        private readonly string arguments;

        public CommandSlide(string process, string arguments)
        {
            this.process = process;
            this.arguments = arguments;
        }

        public override void Play(bool jump)
        {
            SimulatedTyping($"\n> {this.process} {this.arguments}", jump, true);
            if (jump)
            {
                Console.WriteLine("[EXECUTION SKIPPED]");
                return;
            }

            var workingDirectory = FindSolutionDirectory();

            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = this.process,
                Arguments = this.arguments,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            var process = Process.Start(startInfo);
            process.OutputDataReceived += this.OnOutputDataReceived;
            while (!process.HasExited)
            {
                Console.WriteLine(process.StandardOutput.ReadLine());
            }
            process.WaitForExit();
            Console.WriteLine(">");
            Console.WriteLine();
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.Write(e.Data);
        }
    }

    public class TextSlide : Slide
    {
        private readonly string message;

        public TextSlide(string message)
        {
            this.message = message;
        }

        public override void Play(bool jump)
        {
            SimulatedTyping(this.message, jump);
            Console.WriteLine();
        }
    }

    public class InspectSlide : Slide
    {
        public override void Play(bool jump)
        {
            if (jump)
            {
                Console.WriteLine("[INSPECT SKIPPED]");
            }

            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = FindSolutionDirectory(),
                FileName = "docker",
                Arguments = "ps --latest --quiet",
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            string lastContainerId;

            using (var process = Process.Start(startInfo))
            {
                lastContainerId = process.StandardOutput.ReadToEnd();
            }

            var inspectStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = FindSolutionDirectory(),
                FileName = "docker",
                Arguments = "inspect " + lastContainerId.Trim(),
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using (var inspectProcess = Process.Start(inspectStartInfo))
            {
                using (var reader = new JsonTextReader(inspectProcess.StandardOutput))
                {
                    var inspectResult = JToken.ReadFrom(reader);
                    Console.WriteLine(inspectResult[0]["State"].ToString());
                }
            }
            Console.WriteLine();
        }
    }

    public class CodeSlide : Slide
    {
        private readonly string code;
        private static readonly Regex keywordMatch = new Regex(@"\b(const|int|static|void|string|byte|new|try|for|using|catch)\b");
        private static readonly Regex classMatch = new Regex(@"\b(MemoryFailPoint|InsufficientMemoryException)\b");

        public CodeSlide(string code)
        {
            this.code = code;
        }

        public override void Play(bool jump)
        {
            foreach (var line in code.Split("\r\n"))
            {
                Console.Write(line);
                Colourize(line, keywordMatch, ConsoleColor.Blue);
                Colourize(line, classMatch, ConsoleColor.Cyan);
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        private void Colourize(string line, Regex pattern, ConsoleColor selectedColour)
        {
            Console.ForegroundColor = selectedColour;

            for (var match = pattern.Match(line); match.Success; match = match.NextMatch())
            {
                Console.CursorLeft = match.Index;
                Console.Write(match.Value);
            }

            Console.ResetColor();
        }
    }
}
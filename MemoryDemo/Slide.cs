using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace MemoryDemo
{
    public abstract class Slide
    {
        protected static Random rand = new Random();

        internal static Slide Text(string message)
        {
            return new TextSlide(message);
        }

        public abstract void Play(bool jump);

        internal static Slide Command(string process, string arguments)
        {
            return new CommandSlide(process, arguments);
        }

        protected static void SimulatedTyping(string message, bool jump)
        {
            foreach (var word in message.Split(' '))
            {
                if (word.Length >= Console.BufferWidth - Console.CursorLeft)
                {
                    Console.WriteLine();
                }

                foreach (var c in word)
                {
                    Console.Write(c);

                    if (!jump)
                    {
                        Thread.Sleep(rand.Next(16, 33));
                    }
                }

                Console.Write(' ');
            }

            Console.WriteLine();
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
            SimulatedTyping($"> {this.process} {this.arguments}", jump);
            if (jump)
            {
                Console.WriteLine("[EXECUTION SKIPPED]");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = this.process,
                Arguments = this.arguments,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            var process = Process.Start(startInfo);
            process.OutputDataReceived += this.OnOutputDataReceived;
            process.WaitForExit();
            Console.Write(process.StandardOutput.ReadToEnd());
            Console.WriteLine();
            Console.WriteLine(">");
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
        }
    }
}
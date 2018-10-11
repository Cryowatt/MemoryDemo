using System;
using System.Collections.Generic;
using System.Text;

namespace MemoryDemo
{
    public class Program
    {
        public static int SlideIndex = 0;
        public static bool Jump = false;

        public static List<Slide> Slides = new List<Slide>
        {
            Slide.Text("_.NET_ is a *garbage collected* runtime."),
            Slide.Text("The garbage collector works by cleaning up unreference memory as required."),
            Slide.Text("To do this, the garbage collector needs to know three things:"),
            Slide.Text("    • How much memory is the application *using*"),
            Slide.Text("    • How much memory does the application *need*"),
            Slide.Text("    • How much memory is *available*"),
            Slide.Text("This produces a metric that _.NET_ calls *memory pressure*."),
            Slide.Text("Normally, this metric works great."),
            Slide.Text("When running in _Docker_, however, 'memory' isn't always what you expect. For example here are the results of the `free` command when run in a VM with 1GB of memory."),
            Slide.Command("docker", "run --rm alpine free"),
            Slide.Text("The total memory is listed at about 1Gb, as expected."),
            Slide.Text("_Docker_ can limit the memory used by a container with the `--memory` argument. Not what would you expect to happen when I call `free` with the `--memory` argument set to 40mb?"),
            Slide.Command("docker", "run --rm --memory=40mb alpine free"),
            Slide.Text("Those are the same results as before. `free` is lying to us about how much memory is actually available to use."),
            Slide.Text("When you set memory limitations in Docker, it's setting the limits using _cgroup_."),
            Slide.Text("Instead of querying the usual APIs that the operating system provides, a process running in a container must query the metrics that _cgroup_ provides."),
            Slide.Text("Here's what _cgroup_ gives me for available memory for my container:"),
            Slide.Command("docker", "run --rm alpine cat /sys/fs/cgroup/memory/memory.limit_in_bytes"),
            Slide.Text("Wow that's a lot of ram!"),
            Slide.Text("In fact, it's 2^63, which makes sense for a 64-bit machine as this is `Int64.MaxValue`."),
            Slide.Text("Now lets try the previous command but with the `--memory` argument set."),
            Slide.Command("docker", "run --rm --memory=40mb alpine cat /sys/fs/cgroup/memory/memory.limit_in_bytes"),
            Slide.Text("We finally have a reasonable number!"),
            Slide.Text("What happens if we try something similar in _.NET_. For this example I'm using _powershell core_."),
            Slide.Command("docker", "run --rm mcr.microsoft.com/powershell:ubuntu-18.04 pwsh -Command [System.Diagnostics.Process]::GetCurrentProcess().MaxWorkingSet"),
            Slide.Text("Ok, looks the same so far. Now again with `--memory`."),
            Slide.Command("docker", "run --rm --memory=40mb mcr.microsoft.com/powershell:ubuntu-18.04 pwsh -Command [System.Diagnostics.Process]::GetCurrentProcess().MaxWorkingSet"),
            Slide.Text("That's not what I was hoping to see."),
            Slide.Text("*The bad news:*"),
            Slide.Text("As it turns out, _Microsoft_ hasn't implemented the _dotnet core_ runtime in such a way that it handles _Docker_ correctly."),
            Slide.Text("To prove this, I've written a simple application. Here's roughly what the code looks like:"),
            Slide.Code(@"const int ChunkSize = 32_000_0000;

static void Main(string[] args)
{ 
    byte[][] hole = new byte[BigNumber][];

    try
    {
        for (int i = 0; i < hole.Length; i++)
        {
            using (var check = new MemoryFailPoint(ChunkSize * 2 / Megabyte))
            {
                hole[i] = new byte[ChunkSize];
            }
        }
    }
    catch (InsufficientMemoryException)
    {
        // Fail gracefully
    }
}"),
            Slide.Text("This application uses a class called `System.MemoryFailPoint` to ask the runtime whether or not a block of code would run out of memory for a given operation."),
            Slide.Text("`MemoryFailPoint` with throw an `InsufficientMemoryException` if your memory allocation would have likely thrown a `OutOfMemoryException` during execution at that time."),
            Slide.Text("This is critical because it is a very bad idea to catch an `OutMemoryException`, as doing so would require the runtime to allocate more memory for handling the exception. This is incredible unpredictable and often leaves the application in a corrupted state."),
            Slide.Text("`MemoryFailPoint` has allowed me to ask the runtime to figure out if the memory I need for an operation would exceed the available memory, so that I may handle the scenario gracefully."),
            Slide.Text("Here's the sample applcation running natively in Windows."),
            Slide.Command("dotnet", @"run --project .\MemoryHole\MemoryHole\ 512"),
            Slide.Text("The application works exactly as it should."),
            Slide.Text("Here's how it runs in _Docker_ in _dotnet_ `2.0` with `--memory 128MB`:"),
            Slide.Command("docker-compose", @"run dotnet2_0"),
            Slide.Text("What happened? No `OutOfMemoryException`, no `InsufficientMemoryException`, no stacktrace?"),
            Slide.Text("Here's what `docker inspect` shows as the container state."),
            Slide.Inspect(),
            Slide.Text("But Eric, `dotnet` 2.0 is *so old* that _Microsoft_ doesn't even support it anymore!"),
            Slide.Text("Correct, let's see if _Microsoft_ fixed it in 2.1"),
            Slide.Command("docker-compose", @"run dotnet2_1"),
            Slide.Inspect(),
            Slide.Text("Disappointing, right? Well did you know 2.2 is out in preview right now?"),
            Slide.Command("docker-compose", @"run dotnet2_2"),
            Slide.Inspect(),
            Slide.Text("\n...\n...\n..."),
            Slide.Text("What's the solution?"),
            Slide.Text("Ideally _Microsoft_ needs to fix this. There are a number of _github_ bugs open around this issue. I've opened this one: https://github.com/dotnet/corefx/issues/32748"),
            Slide.Text("While they are fixing that, I've created a terrible hack."),
            Slide.Text("I call it _GarbageTruck_."),
            Slide.Text("It's a simple service that polls the `cgroup` metrics and if your process gets too close to the memory limits, it calls `GC.Collect()`."),
            Slide.Text("If you're writing _dotnet_ code in _Docker_, then I *highly recommend* adding a _GarbageTruck_ to your service to keep the memory in check."),
            Slide.Text("[END]"),
        };

        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            for (; ; SlideIndex++)
            {
                Pause();
                Slides[SlideIndex].Play(Jump);
                Jump = false;
            }
        }

        private static void Pause()
        {
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);
            }
            while (Console.KeyAvailable);

            if (key.Key == ConsoleKey.LeftArrow)
            {
                SlideIndex -= 2;
                Jump = true;
            }
            else if (key.Key == ConsoleKey.RightArrow)
            {
                Jump = true;
            }
            else if (key.Key == ConsoleKey.R)
            {
                SlideIndex--;
                Jump = false;
            }
            else if(key.Key == ConsoleKey.Q)
            {
                Environment.Exit(0);
            }

            SlideIndex = Math.Clamp(SlideIndex, 0, Slides.Count - 1);

            if (Jump)
            {
                Console.Clear();
                Console.WriteLine("Slide {0}", SlideIndex);
            }
        }
    }
}

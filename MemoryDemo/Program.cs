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
            Slide.Text(".NET is a garbage collected language."),
            Slide.Text("The garbage collector works by cleaning up unreference memory as required."),
            Slide.Text("To do this, the garbage collector needs to know three things:"),
            Slide.Text("    • How much memory is the application using"),
            Slide.Text("    • How much memory does the application need"),
            Slide.Text("    • How much memory is available"),
            Slide.Text("\nThis produces a metric that dotnet calls \"memory pressure\""),
            Slide.Text("Normally, this metric works great. When running in Docker, however, 'memory' isn't always what you expect. For example here is the results of the `free` command when run in a VM with 1GB of memory."),
            Slide.Command("docker", "run --rm alpine free"),
            Slide.Text("[END]"),
            //Technical Mumbo-Jumbo
            // runtime must know how much memory is available for it to consume. This usually works fine, but Docker has some quirks about it. Here are the results of the free command when running on a VM with 1Gb of memory available.
            //PS C:\Users\ericc> docker run --rm alpine free

            //             total       used       free     shared    buffers     cached

            //Mem:        997100     914040      83060       1124     159620     328912

            //-/+ buffers/cache:     425508     571592

            //Swap:      1048572       6532    1042040
            //The total memory is listed at about 1Gb, as expected. Docker can limit the memory used by a container with the --memory argument. Now what would you expect to happen when I call free with the --memory argument set to 40mb?
            //PS C:\Users\ericc> docker run --rm --memory=40mb alpine free

            //             total       used       free     shared    buffers     cached

            //Mem:        997100     915152      81948       1156     159732     328944

            //-/+ buffers/cache:     426476     570624

            //Swap:      1048572       6532    1042040
            //Those are the same results as before. free is lying to us about how much memory is actually available to use. When you set memory limitations in Docker, it's setting the limits using cgroups, which need to be queried differently than available system memory. Here's what cgroups gives me for available memory for my process
            //PS C:\Users\ericc> docker run --rm alpine cat /sys/fs/cgroup/memory/memory.limit_in_bytes
            //9223372036854771712
            //Wow that's a lot of ram! That's basically 2^63, which makes sense for a 64-bit machine. Now the same thing, but with the --memory argument set.
            //PS C:\Users\ericc> docker run --rm --memory=40mb alpine cat /sys/fs/cgroup/memory/memory.limit_in_bytes
            //41943040
            //We finally have a reasonable number! What happens if we try something similar in .NET.
            //PS C:\Users\ericc> docker run --rm mcr.microsoft.com/powershell:ubuntu-18.04 pwsh -Command '[System.Diagnostics.Process]
            //::GetCurrentProcess().MaxWorkingSet'
            //9223372036854775807
            //Ok, looks the same so far. Now again with --memory.
            //PS C:\Users\ericc> docker run --rm --memory=40mb mcr.microsoft.com/powershell:ubuntu-18.04 pwsh -Command '[System.Diagno
            //stics.Process]::GetCurrentProcess().MaxWorkingSet'
            //9223372036854775807
            //That's not what I was hoping to see.

            //The bad news
            //I've spent most of the day trying to find some corner of .NET Framework or the runtime itself that implements anything that analyzes memory utilization correctly in Docker and I've found nothing. Based on the number of open issues in GitHub, this is still an ongoing problem. Because of this limitation in the runtime, .NET will often waits too long to perform a garabge collection, and the container will be killed by cgroups. Normally .NET is a good citizen and keeps its resources tidy, but .NET simply doesn't know where the line is and cannot react quickly enough. I have a repro of this issue with a simple service that pings Fiddler with a simple web request. Each request allocated a couple of kb worth of managed and native resources. The garbage collector will ocassionally run to keep everything tidy, and it can run indefinitely without the --memory argument set in Docker. However, if I allocate a block on memory near the memory threashold, I can reproduce the sigkill due to memory limits predictably with the same application.

            //The good news
            //I have two experiments that I'd like to run on TheChunnel's scheduler service. The scheduler is incredibley simple and actually does very few memory allocations, so it's a much easier to test against than HEX. Based on my research and local reproductions of this issue, we have two possible options.

            //Option 1: Server Garbage Collector
            //This one is a simple change to our service to enable ServerGarbageCollection mode.  This is slightly different implementation of the garbage collection in netcore that is designed for long-running services. The fact that we aren't using it currently might be a massively oversight on our part, and I will definitely recommend updating all the templates and services we currently have to include this option. 

            //There are some people who claim that enabling this garbage collection "fixes" the issues with Docker containers, but the results seem inconsistent. This experiment is easy to perform, just set the flag in the csproj file, deploy, and wait.

            //Option 2: Garbage Haxin Code
            //I feel dirty by even suggesting it, because it goes against all of the recommended practices and guidelines. But what we do is we create a background thread that monitors the process memory and when it approaches the limits defined by cgroups then we explicitly call GC.Collect(). The code I'm suggesting would set off a million red flags if I saw it in a code review, but at this point in time I don't see any other way to fix our memory woes.
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

            SlideIndex = Math.Clamp(SlideIndex, 0, Slides.Count - 1);

            if (Jump)
            {
                Console.Clear();
                Console.WriteLine("Slide {0}", SlideIndex);
            }
        }
    }
}

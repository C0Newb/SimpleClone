using Microsoft.VisualBasic;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;

namespace SimpleClone
{
    class Program
    {

        private static String sourceDir;
        private static String destinationDir;


        static void Main()
        {
            Console.Title = "SimpleClone: <source> <dest>";

             Run();
        }

        private static string GetArg(string[] args, int index)
        {
            if (args.Length >= index - 1)
                return args[index];
            else
                return "";
        }

        private static void Run()
        {

            string[] args = Environment.GetCommandLineArgs();

            bool CreateSource = false;
            bool NoSync = false;
            bool PeriodicSync = false;
            string SyncInterval = "";

            // If a directory is not specified, exit program.
            if (args.Length < 3 && GetArg(args, 1) != "-?")
            {
                // Display the proper way to call the program.
                Console.WriteLine("Usage: simpleclone.exe <source dir> <destination> [options]\r\n\"simplecloner.exe -?\" for more help");
                return;
            }

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("-") || args[i].StartsWith("/"))
                {
                    // Option
                    if (args[i].Substring(1) == "cs" || args[i].Substring(1) == "createsource")
                        CreateSource = true;
                    else if (args[i].Substring(1) == "?" || args[i].Substring(1) == "help")
                    {
                        Console.WriteLine("SimpleClone mirrors a directory to another directory. SimpleClone monitors the 'source' directory for changes and, upon change, syncs effected files."
                            + Environment.NewLine + Environment.NewLine + "Usage: simpleclone [-cs | -createsource] [-nosync] [-syncinterval N(s,m,h)<source> <destination>"
                            + Environment.NewLine + Environment.NewLine + "Options:"
                            + Environment.NewLine + "   -cs | -createsource:    Creates the <source> directory (if it doesn't already exist)"
                            + Environment.NewLine + "   -nosync:                Skips initial sync"
                            + Environment.NewLine + "   -syncinterval X[s|m|h]: Enables periodic syncing every X second(s)/minute(s)/hour(s)"
                            );
                        return;
                    }
                    else if (args[i].Substring(1) == "nosync")
                        NoSync = true;
                    else if (args[i].Substring(1) == "syncinterval")
                    {
                        PeriodicSync = true;
                        SyncInterval = args[i+1];
                        i++;
                    }
                }
                else
                {
                    if (sourceDir == null)
                        sourceDir = args[i];
                    else
                        destinationDir = args[i];
                }
            }

            if (PeriodicSync && SyncInterval != "")
            {
                string timeSetting = SyncInterval.Substring(SyncInterval.Length - 1);
                int time;

                Console.WriteLine(SyncInterval.Substring(0, SyncInterval.Length - 1));

                if (!int.TryParse(SyncInterval.Substring(0, SyncInterval.Length - 1), out time))
                {
                    Console.WriteLine("Invalid time interval");
                    return;
                }

                if (timeSetting == "s")
                    time *= 1000;
                else if (timeSetting == "m")
                    time *= 60000;
                else // Assume hour
                    time *= 3600000;

                Console.WriteLine("Periodic sync enabled for every " + time / 60000 + " minute(s).");

                new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(time);
                        Console.WriteLine("Periodic sync...");
                        CopyFile(sourceDir, destinationDir, "\\", true);
                    }
                })
                { IsBackground = true, Name = "SimpleClone Periodic Sync" }.Start();
            }
            
            if (sourceDir == null || destinationDir == null)
            {
                Console.WriteLine(((sourceDir == null)? "Source" : "Destination") + " directory not provided.\r\nsimpleclone.exe <source dir> <destination dir> [options]");
                return;
            }

            Console.Title = "SimpleClone: " + sourceDir + " -> " + destinationDir;

            if (!Directory.Exists(sourceDir + "\\"))
            {
                if (!CreateSource)
                {
                    Console.WriteLine("Source directory does not exist." + Environment.NewLine + "Use \"-cs\" to create the source folder.");
                    return;
                }
                else
                {
                    Console.WriteLine("Creating source directory...");
                    Directory.CreateDirectory(sourceDir + "\\");
                }
            }

            if (!NoSync)
            {
                Console.WriteLine("Syncing. . .");
                CopyFile(sourceDir, destinationDir, "\\", true);
            }


            // Create a new FileSystemWatcher and set its properties.
            using (FileSystemWatcher watcher = new FileSystemWatcher())
            {


                watcher.Path = args[1];

                // Watch for changes in LastAccess and LastWrite times, and
                // the renaming of files or directories.
                watcher.NotifyFilter = NotifyFilters.LastAccess
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.FileName
                                     | NotifyFilters.DirectoryName;

                // Add event handlers.
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.Deleted += OnChanged;
                watcher.Renamed += OnRenamed;

                watcher.IncludeSubdirectories = true;

                // Begin watching.
                watcher.EnableRaisingEvents = true;

                // Wait for the user to quit the program.
                Console.WriteLine("Now listening to \"" + sourceDir + "\"");

                if (!Directory.Exists(destinationDir))
                    Directory.CreateDirectory(destinationDir);

                Console.WriteLine("Type \"(h)elp\" for help.\r\nType \"(q)uit\" to stop.");
                bool canExit = false;
                while (!canExit)
                {
                    string command = Console.ReadLine();
                    if (command.StartsWith("q"))
                        canExit = true;
                    else if (command.StartsWith("h"))
                        Console.WriteLine("Commands: "
                            + Environment.NewLine + "(q)uit:        Exits"
                            + Environment.NewLine + "(h)elp:        This message"
                            + Environment.NewLine + "sync:      Initiate a sync");
                    else if (command == "sync")
                    {
                        new Thread(() =>
                        {
                            Console.WriteLine("Syncing. . .");

                            CopyFile(sourceDir, destinationDir, "\\", true);
                        })
                        { IsBackground = true, Name = "SimpleClone - Sync" }.Start();
                    }
                }
            }
        }

        // Define the event handlers.
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            // Specify what is done when a file is changed, created, or deleted.
            //Console.WriteLine($"\"{e.Name}\" {e.ChangeType.ToString().ToLower()}");

            bool dir = Directory.Exists(e.FullPath); // Note: when something is deleted, we can't check what it WAS

            if (dir)
            {
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    Console.WriteLine("CREATE: " + "\\" + e.Name + "\\");
                    if (!Directory.Exists(destinationDir + "\\" + e.Name))
                    {
                        CopyFile(e.FullPath, destinationDir + "\\" + e.Name, e.Name, true);
                        //Directory.CreateDirectory(destinationDir + "\\" + e.Name);
                    }

                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    Console.WriteLine("DELETE: " + "\\" + e.Name + "\\");
                    if (Directory.Exists(destinationDir + "\\" + e.Name))
                        Directory.Delete(destinationDir + "\\" + e.Name, true);
                }
            }
            else
            {
                if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    CopyFile(e.FullPath, destinationDir + "\\" + e.Name, e.Name, false);
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    Console.WriteLine("DELETE: " + "\\" + e.Name);
                    if (Directory.Exists(destinationDir + "\\" + e.Name))
                        Directory.Delete(destinationDir + "\\" + e.Name, true);
                    else if (File.Exists(destinationDir + "\\" + e.Name))
                            File.Delete(destinationDir + "\\" + e.Name);
                }
                else if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    CopyFile(e.FullPath, destinationDir + "\\" + e.Name, e.Name, false);
                }   
                else
                {
                    Console.WriteLine("Unknown change?: " + e.ChangeType);
                }
            }
        }

        private static void OnRenamed(object source, RenamedEventArgs e)
        {
            // Specify what is done when a file is renamed.
            //Console.WriteLine($"File: {e.OldFullPath} renamed to {e.FullPath}");
            
            if (Directory.Exists(e.FullPath))
            {
                Console.WriteLine("RENAME (MOVE): " + "\\" + e.OldName + "\\" + " TO " + "\\" + e.Name + "\\");
                if (Directory.Exists(destinationDir + "\\" + e.Name))
                    Directory.Delete(destinationDir + "\\" + e.Name);

                if (Directory.Exists(destinationDir + "\\" + e.OldName))
                {
                    Directory.Move(destinationDir + "\\" + e.OldName, destinationDir + "\\" + e.Name);
                    Console.WriteLine("Move successful.");
                }
            }
            else
            {
                Console.WriteLine("RENAME (MOVE): " + "\\" + e.OldName + " TO " + "\\" + e.Name);
                if (File.Exists(destinationDir + "\\" + e.Name))
                    File.Delete(destinationDir + "\\" + e.Name);

                if (File.Exists(destinationDir + "\\" + e.OldName))
                {
                    File.Move(destinationDir + "\\" + e.OldName, destinationDir + "\\" + e.Name);
                    Console.WriteLine("Move successful.");
                }
                else
                {
                    CopyFile(e.FullPath, destinationDir + "\\" + e.Name, e.Name, false);
                }
            }
        }

        private static void CreateParent(string source, string destination)
        {
            if (!Directory.Exists(Directory.GetParent(destination).FullName))
                CreateParent(Directory.GetParent(source).FullName, Directory.GetParent(destination).FullName);

            if (Directory.Exists(source) && !Directory.Exists(destination))
                Directory.CreateDirectory(destination);

        }

        const int BYTES_TO_READ = sizeof(Int64);
        private static bool FileDiffers(string source, string destination)
        {
            if (!File.Exists(destination))
                return true;
            try
            {
                FileInfo s = new FileInfo(source);
                FileInfo d = new FileInfo(destination);

                if (s.Length != d.Length)
                    return true;

                if (string.Equals(s.FullName, d.FullName, StringComparison.OrdinalIgnoreCase))
                    return false;

                int iterations = (int)Math.Ceiling((double)s.Length / BYTES_TO_READ);

                using (FileStream fs1 = s.OpenRead())
                using (FileStream fs2 = d.OpenRead())
                {
                    byte[] one = new byte[BYTES_TO_READ];
                    byte[] two = new byte[BYTES_TO_READ];

                    for (int i = 0; i < iterations; i++)
                    {
                        fs1.Read(one, 0, BYTES_TO_READ);
                        fs2.Read(two, 0, BYTES_TO_READ);

                        if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                            return true;
                    }
                }

                return false;
            }
            catch { return true; }
        }

        private static void CopyFile(string source, string destination, string Name, bool dir)
        {
            //if (Directory.Exists(destination))
            //    Directory.Delete(destination, true);


            try
            {
                Console.WriteLine("MODIFY (COPY) " + ((dir)? "(!DIR!): " : ": ") + "\\" + Name + ((dir)? "\\" : ""));
                if (dir)
                {
                    foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                        if (!Directory.Exists(dirPath.Replace(source, destination)))
                            Directory.CreateDirectory(dirPath.Replace(source, destination));

                    foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
                    {
                        //Console.Write("Checking: \"" + newPath + "\" ... ");
                        if (FileDiffers(newPath, newPath.Replace(source, destination)))
                        {
                            File.Copy(newPath, newPath.Replace(source, destination), true);
                            Console.WriteLine("\"" + newPath + "\" has been synced!");
                        }
                        //else
                        //    Console.WriteLine("already synced, ignoring.");
                    }
                            
                }
                else
                {
                    CreateParent(source, destination);
                    if (File.Exists(source))
                        File.Copy(source, destination, true);
                }
                   
            }
            catch (Exception)
            {
                Console.WriteLine("!! MODIFY FAILED: " + "\\" + Name);
                new Thread(() =>
                {
                    Thread.Sleep(5000);
                    bool canQuit = false;
                    int attemptTime = 0;
                    Exception error = new Exception("...");

                    while (!canQuit && attemptTime < 5)
                        try
                        {
                            Thread.Sleep(5000);
                            attemptTime++;
                            Console.WriteLine("MODIFY (ATTEMPT: " + attemptTime + "): " + "\\" + Name);
                            
                            if (!File.Exists(source)) // Does the file still exist?
                                canQuit = true;

                            File.Copy(source, destination, true);
                            canQuit = true;
                        }
                        catch (Exception e) 
                        {
                            error = e;
                        }
                    if (!canQuit)
                    {
                        Console.WriteLine("!!!MODIFY COMPLETLY FAILED!!!: " + "\\" + Name + " ERROR: " + error.Message);
                        Console.WriteLine("Beginning sync process due to modify failure...");
                        new Thread(() =>
                        {
                            Thread.Sleep(60000);
                            Console.WriteLine("[Modify Failed] Syncing. . .");
                            CopyFile(sourceDir, destinationDir, "\\", true);
                        })
                        { IsBackground = true, Name = "SimpleClone - Sync" }.Start();
                    }
                })
                { IsBackground = true, Name = "Retroactive Cloner" }.Start();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Reactive;
using System.Threading;

namespace FolderWatcher
{
    class Program
    {
        private static readonly DiagnosticSource diaglog = new DiagnosticListener("FolderWatcherDS");
        private static readonly List<IDisposable> eventSubscriptions = new List<IDisposable>();
        private static readonly EventSource log = new EventSource("FolderWatcher");

        static Program()
        {
            static void SubscribeWatchEvents(KeyValuePair<string, object?> kv)
            {
                if ("Renamed" == kv.Key)
                {
                    var ev = (RenamedEventArgs)kv.Value!;
                    Console.WriteLine($"{kv.Key}: '{ev.OldFullPath}' -> '{ev.FullPath}'");
                }
                else
                {
                    var ev = (FileSystemEventArgs)kv.Value!;
                    Console.WriteLine($"{kv.Key}: '{ev.FullPath}'");
                }
            }

            var s = DiagnosticListener.AllListeners.Subscribe(
                listener => {
                    if ("FolderWatcherDS" == listener.Name)
                    {
                        eventSubscriptions.Add(listener.Subscribe(SubscribeWatchEvents));
                    }
                });
            eventSubscriptions.Add(s);
        }

        static void Main(string[] args)
        {
            if (args.Length != 1 || !Directory.Exists(args[0]))
            {
                Console.WriteLine("Usage: FolderWatcher <directory-path>");
                return;
            }

            using var watcher = new FileSystemWatcher(args[0]);

            watcher.Created += OnChange;
            watcher.Changed += OnChange;
            watcher.Deleted += OnChange;
            watcher.Renamed += OnRename;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;

            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (o, ev) => { ev.Cancel = true; cts.Cancel(); };

            Console.WriteLine("Press Ctrl + C to stop.");
            cts.Token.WaitHandle.WaitOne();
        }

        private static void OnRename(object sender, RenamedEventArgs ev)
        {
            FileOperationEventSource.Log.ActiveFolderOperationCounter.Increment();
            FileOperationEventSource.Log.IncrementOperationsCount();

            var evname = ev.ChangeType.ToString();
            if (diaglog.IsEnabled(evname))
            {
                diaglog.Write(evname, ev);
            }
            var options = new EventSourceOptions {
                Keywords = Directory.Exists(ev.FullPath) ? Keywords.Directory : Keywords.File,
                Level = EventLevel.Informational
            };
            log.Write("Rename", options, new { ev.Name, ev.FullPath, ev.OldName, ev.OldFullPath });
        }

        private static void OnChange(object sender, FileSystemEventArgs ev)
        {
            FileOperationEventSource.Log.ActiveFolderOperationCounter.Increment();
            FileOperationEventSource.Log.IncrementOperationsCount();

            var evname = ev.ChangeType.ToString();
            if (diaglog.IsEnabled(evname))
            {
                diaglog.Write(ev.ChangeType.ToString(), ev);
            }
            switch (ev.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    var options = new EventSourceOptions {
                        Keywords = Directory.Exists(ev.FullPath) ? Keywords.Directory : Keywords.File,
                        Level = EventLevel.Informational
                    };
                    log.Write("Create", options, new { ev.Name, ev.FullPath });
                    break;
                case WatcherChangeTypes.Deleted:
                    options = new EventSourceOptions { Keywords = Keywords.Directory | Keywords.File, Level = EventLevel.Informational };
                    log.Write("Delete", options, new { ev.Name, ev.FullPath });
                    break;
                case WatcherChangeTypes.Changed:
                    options = new EventSourceOptions {
                        Keywords = Directory.Exists(ev.FullPath) ? Keywords.Directory : Keywords.File,
                        Level = EventLevel.Verbose
                    };
                    log.Write("Update", options, new { ev.Name, ev.FullPath });
                    break;
                default:
                    Debug.Assert(false, "invalid change type");
                    break;
            }
        }

        private static void OnError(object sender, ErrorEventArgs ev)
        {
            var options = new EventSourceOptions { Level = EventLevel.Error };
            var ex = ev.GetException();
            log.Write("Error", options, new { ErrorMessage = ex.Message, ErrorType = ex.GetType().FullName });
        }

    }
}

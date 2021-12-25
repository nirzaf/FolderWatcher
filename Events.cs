using System.Diagnostics.Tracing;
using System.Threading;

namespace FolderWatcher
{
    public class Keywords
    {
        public const EventKeywords File = (EventKeywords)1;
        public const EventKeywords Directory = (EventKeywords)2;
    }

    public class Tasks
    {
        public const EventTask Create = (EventTask)1;
        public const EventTask Rename = (EventTask)2;
        public const EventTask Update = (EventTask)3;
        public const EventTask Delete = (EventTask)4;
        public const EventTask Error = (EventTask)5;
    }

    [EventSource(Name = "FolderWatcher-Operations")]
    public sealed class FileOperationEventSource : EventSource
    {
        private long totalFolderOperationsCount = 0;

        public static readonly FileOperationEventSource Log = new FileOperationEventSource();
        public readonly IncrementingEventCounter ActiveFolderOperationCounter;
        public readonly PollingCounter TotalFolderOperationCounter;

        public void IncrementOperationsCount() => Interlocked.Increment(ref totalFolderOperationsCount);

        private FileOperationEventSource() : base(EventSourceSettings.EtwSelfDescribingEventFormat)
        {
            ActiveFolderOperationCounter = new IncrementingEventCounter("active-folder-operations", this) {
                DisplayName = "# of operations in the folder",
                DisplayUnits = "count"
            };
            TotalFolderOperationCounter = new PollingCounter("total-folder-operations",
                this, () => Interlocked.Read(ref totalFolderOperationsCount)) { 
                DisplayName = "# of operations in the folder (total)",
            };
        }
    }
}

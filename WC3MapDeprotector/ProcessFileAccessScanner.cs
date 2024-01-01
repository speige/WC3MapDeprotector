using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace WC3MapDeprotector
{
    public partial class ProcessFileAccessScanner : IDisposable
    {
        public int ProcessId { get; set; }
        public string MonitorFolderPath { get; set; }

        public delegate void Delegate(string fileName);
        public event Delegate FileAccessed;

        protected TraceEventSession _kernelSession;
        protected Thread _processingThread;

        public ProcessFileAccessScanner()
        {
            _kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName, TraceEventSessionOptions.NoRestartOnCreate)
            {
                BufferSizeMB = 128,
                CpuSampleIntervalMSec = 10,
                StackCompression = true,
                StopOnDispose = true
            };

            _processingThread = new Thread(() =>
            {
                _kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.FileIOInit);
                var kernelParser = new KernelTraceEventParser(_kernelSession.Source);
                kernelParser.FileIOCreate += KernelParser_FileIOCreate;
                _kernelSession.Source.Process();
            });
            _processingThread.IsBackground = true;
            _processingThread.Start();
        }

        private void KernelParser_FileIOCreate(Microsoft.Diagnostics.Tracing.Parsers.Kernel.FileIOCreateTraceData traceData)
        {
            if (FileAccessed != null && traceData.ProcessID == ProcessId && traceData.FileName.Length > MonitorFolderPath.Length && traceData.FileName.StartsWith(MonitorFolderPath, StringComparison.InvariantCultureIgnoreCase))
            {
                var filePath = traceData.FileName.Substring(MonitorFolderPath.Length + 1);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    FileAccessed(filePath);
                }
            }
        }

        public void Dispose()
        {
            _kernelSession.Dispose();
        }
    }
}

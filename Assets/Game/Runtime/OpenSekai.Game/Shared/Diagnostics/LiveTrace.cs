using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace OpenSekai.Shared.Diagnostics
{
    /// <summary>
    /// Which part of the live pipeline a trace line belongs to. A run usually needs one or two
    /// categories, and every extra one costs logcat ring buffer, so they are switched separately.
    /// </summary>
    [Flags]
    public enum LiveTraceCategory
    {
        None = 0,
        Hold = 1 << 0,
        Tail = 1 << 1,
        Swipe = 1 << 2,
        Judge = 1 << 3,
        Pool = 1 << 4,
        View = 1 << 5,
        All = ~0,
    }

    /// <summary>
    /// Resident diagnostics for the live judgement pipeline, switched from the settings panel.
    /// Off by default and free when off: callers ask <see cref="IsOn"/> first, so nothing is
    /// formatted, concatenated or logged until someone turns a category on.
    ///
    /// Lines land in three places: the ring buffer below (readable in game), <c>Debug.Log</c>
    /// (visible through <c>adb logcat</c>) and a file under <c>persistentDataPath/logs</c> that
    /// survives the process, so a reproduction does not have to be repeated just to capture it.
    /// </summary>
    public static class LiveTrace
    {
        public const string EnabledPrefKey = "diagnostics.trace.enabled";
        public const string CategoryPrefKey = "diagnostics.trace.categories";

        private const int BufferSize = 4096;
        private static readonly string[] _buffer = new string[BufferSize];
        private static int _next;
        private static int _count;

        private static bool _enabled;
        private static bool _loaded;
        private static LiveTraceCategory _categories = LiveTraceCategory.All;
        private static StreamWriter _sink;
        private static string _logFilePath;

        public static bool Enabled
        {
            get
            {
                EnsureLoaded();
                return _enabled;
            }
            set
            {
                EnsureLoaded();
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                PlayerPrefs.SetInt(EnabledPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();

                if (value)
                {
                    OpenSink();
                    // Announced on the console and in the file, but not in the ring buffer: the buffer
                    // is what a reproduction is read back from, and a banner would sit in front of the
                    // first real event of every capture.
                    string Banner = string.Concat("[TL][All] trace enabled mask=", ((int)Categories).ToString());
                    Debug.Log(Banner);
                    if (_sink != null)
                    {
                        _sink.WriteLine(Banner);
                    }
                }
                else
                {
                    CloseSink();
                }
            }
        }

        public static LiveTraceCategory Categories
        {
            get
            {
                EnsureLoaded();
                return _categories;
            }
            set
            {
                EnsureLoaded();
                _categories = value;
                PlayerPrefs.SetInt(CategoryPrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string LogFilePath
        {
            get
            {
                EnsureLoaded();
                return _logFilePath;
            }
        }

        /// <summary>True when a line in this category would be kept. Always free to call.</summary>
        public static bool IsOn(LiveTraceCategory category)
        {
            // The preference has to be read here: nothing in the live pipeline touches Enabled, so a
            // session that never opened the settings panel ran with the default (off) even though the
            // switch was on disk.
            EnsureLoaded();

            if (!_enabled)
            {
                return false;
            }

            // An empty mask cannot mean "keep nothing": someone switched tracing on and expects to
            // see the pipeline. Treating it as "nothing" made the switch look dead.
            return _categories == LiveTraceCategory.None || (category & _categories) != 0;
        }

        public static void Write(LiveTraceCategory category, string message)
        {
            if (!IsOn(category))
            {
                return;
            }

            // A quit closes the file while the switch stays on: the next line has to start a new one,
            // or a resumed session would log to the console and the ring buffer only.
            EnsureSink();

            string line = string.Concat("[TL][", category.ToString(), "] ", message);
            Push(line);
            Debug.Log(line);
            if (_sink != null)
            {
                _sink.WriteLine(line);
            }
        }

        /// <summary>
        /// A rule that must always hold, whatever category is on: a finished note being judged,
        /// time running backwards, a bar keeping itself alive in the future. Kept even when the
        /// category mask is narrow, because a violation invalidates everything else in the log.
        /// </summary>
        public static void Invariant(bool holds, string message)
        {
            if (holds || !_enabled)
            {
                return;
            }

            EnsureSink();

            string line = string.Concat("[TL][INVARIANT] ", message);
            Push(line);
            Debug.LogWarning(line);
            if (_sink != null)
            {
                _sink.WriteLine(line);
            }
        }

        public static void ClearBuffer()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _next = 0;
            _count = 0;
        }

        /// <summary>Most recent lines, oldest first. Used by in game dumps and tests.</summary>
        public static string Recent(int maxLines)
        {
            int take = Mathf.Clamp(maxLines, 0, _count);
            StringBuilder builder = new StringBuilder();
            for (int i = _count - take; i < _count; i++)
            {
                int index = ((_next - _count + i) % BufferSize + BufferSize) % BufferSize;
                builder.AppendLine(_buffer[index]);
            }

            return builder.ToString();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _enabled = PlayerPrefs.GetInt(EnabledPrefKey, 0) != 0;
            _categories = (LiveTraceCategory)PlayerPrefs.GetInt(CategoryPrefKey, (int)_categories);
            if (_enabled)
            {
                OpenSink();
            }
        }

        private static void Push(string line)
        {
            string stamped = string.Concat(DateTime.Now.ToString("HH:mm:ss.fff"), " ", line);
            _buffer[_next] = stamped;
            _next = (_next + 1) % BufferSize;
            if (_count < BufferSize)
            {
                _count++;
            }
        }

        private static void EnsureSink()
        {
            if (_sink == null)
            {
                OpenSink();
            }
        }

        private static void OpenSink()
        {
            if (_sink != null)
            {
                return;
            }

            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "logs");
                Directory.CreateDirectory(directory);
                // A run can be opened twice inside one second (leaving play mode and entering it
                // again): the second sink must not truncate the first run in that case.
                string stamp = "live-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string candidate = Path.Combine(directory, stamp + ".log");
                for (int suffix = 1; File.Exists(candidate); suffix++)
                {
                    candidate = Path.Combine(directory, stamp + "-" + suffix + ".log");
                }

                _logFilePath = candidate;
                // FileShare.Read so a running capture can be pulled and read while the game is still
                // writing it; without it the sink locks its own file on this platform.
                var stream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _sink = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                _sink.WriteLine("# Open Sekai Atlas live trace " + Application.version);
            }
            catch (Exception exception)
            {
                _sink = null;
                Debug.LogWarning("LiveTrace could not open its log file: " + exception.Message);
            }
        }

        private static void CloseSink()
        {
            if (_sink == null)
            {
                return;
            }

            _sink.Flush();
            _sink.Dispose();
            _sink = null;
        }

        /// <summary>
        /// Pushes what the writer holds onto disk without closing it. A pause can end in the process
        /// being killed, and the stream keeps a buffer of its own; the file has to be complete before
        /// that, and reopening on resume would start a second file for the same run.
        /// </summary>
        public static void Flush()
        {
            EnsureLoaded();
            _sink?.Flush();
        }

        /// <summary>Flushes and closes the file sink. Called when the app quits.</summary>
        public static void Shutdown()
        {
            CloseSink();
        }
    }
}

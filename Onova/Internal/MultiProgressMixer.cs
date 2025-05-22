namespace Onova.Internal
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    public class MultiLineProgressBar : IDisposable
    {
        private readonly ConcurrentDictionary<string, ProgressBarState> _progressBars;
        private readonly Timer _timer;
        private readonly object _lockObject = new();
        private int _startCursorTop;
        private bool _disposed;
        private const int blockCount = 50; // Smaller to fit better
        private readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string animation = @"|/-\";
        private int _animationIndex = 0;

        public MultiLineProgressBar()
        {
            _progressBars = new ConcurrentDictionary<string, ProgressBarState>();
            _startCursorTop = Console.CursorTop;
            Console.CursorVisible = false;

            if (!Console.IsOutputRedirected)
            {
                _timer = new Timer(UpdateDisplay, null, animationInterval, animationInterval);
            }
        }

        public IProgress<double> CreateProgressBar(string name, string description = "")
        {
            lock (_lockObject)
            {
                if (_disposed) return null;

                var state = new ProgressBarState
                {
                    Name = name,
                    Description = description,
                    Progress = 0.0,
                    LineIndex = _progressBars.Count
                };

                _progressBars.TryAdd(name, state);

                // Reserve space for this progress bar
                Console.WriteLine($"{name}: {description}");

                return new Progress<double>(progress => UpdateProgress(name, progress));
            }
        }

        private void UpdateProgress(string name, double value)
        {
            if (_progressBars.TryGetValue(name, out var state))
            {
                value = Math.Max(0, Math.Min(1, value));
                Interlocked.Exchange(ref state.Progress, value);
            }
        }

        public void SetStatus(string name, string status)
        {
            if (_progressBars.TryGetValue(name, out var state))
            {
                state.Status = status;
            }
        }

        public void CompleteProgress(string name, string message = "✓ Completed")
        {
            if (_progressBars.TryGetValue(name, out var state))
            {
                state.Progress = 1.0;
                state.Status = message;
                state.IsCompleted = true;
            }
        }

        public void FailProgress(string name, string message)
        {
            if (_progressBars.TryGetValue(name, out var state))
            {
                state.Status = $"✗ {message}";
                state.IsFailed = true;
            }
        }

        public void SkipProgress(string name, string message)
        {
            if (_progressBars.TryGetValue(name, out var state))
            {
                state.Status = $"⚠ {message}";
                state.IsCompleted = true;
            }
        }

        private void UpdateDisplay(object state)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                _animationIndex++; // Advance animation for all bars

                try
                {
                    foreach (var kvp in _progressBars)
                    {
                        var progressState = kvp.Value;
                        var targetLine = _startCursorTop + progressState.LineIndex;

                        Console.SetCursorPosition(0, targetLine);
                        var progressText = BuildProgressText(progressState);
                        Console.Write(progressText.PadRight(Console.WindowWidth - 1));
                    }

                    Console.SetCursorPosition(0, _startCursorTop + _progressBars.Count);
                }
                catch (Exception)
                {
                    // Ignore console errors
                }
            }
        }

        private string BuildProgressText(ProgressBarState state)
        {
            int progressBlockCount = (int)(state.Progress * blockCount);
            int percent = (int)(state.Progress * 100);

            char animChar = state.IsCompleted || state.IsFailed ? ' ' : animation[_animationIndex % animation.Length];

            var progressBar = $"[{new string('#', progressBlockCount)}{new string('-', blockCount - progressBlockCount)}]";

            var result = $"{state.Name,-12}: {progressBar} {percent,3}% {animChar}";

            if (!string.IsNullOrEmpty(state.Status))
            {
                result += $" {state.Status}";
            }
            else if (!string.IsNullOrEmpty(state.Description))
            {
                result += $" {state.Description}";
            }

            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                _disposed = true;
                _timer?.Dispose();

                // Final update without animation
                foreach (var kvp in _progressBars)
                {
                    var progressState = kvp.Value;
                    var targetLine = _startCursorTop + progressState.LineIndex;

                    Console.SetCursorPosition(0, targetLine);
                    var progressText = BuildProgressText(progressState);
                    Console.Write(progressText.PadRight(Console.WindowWidth - 1));
                }

                Console.CursorVisible = true;
                Console.SetCursorPosition(0, _startCursorTop + _progressBars.Count);
            }
        }

        private class ProgressBarState
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public double Progress = 0;
            public string Status { get; set; }
            public int LineIndex { get; set; }
            public bool IsCompleted { get; set; }
            public bool IsFailed { get; set; }
        }
    }
}
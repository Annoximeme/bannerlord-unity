using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace CampaignEventHarness
{
    /// <summary>
    /// Subscribes to every public static property on <see cref="CampaignEvents"/> — all 277,
    /// not a hand-picked subset — via reflection, since each one uniformly exposes
    /// <c>AddNonSerializedListener(object, Action&lt;...&gt;)</c> for 0 to 7 generic arguments
    /// (verified against the real install with apiscan before writing this). Logs a global
    /// firing sequence, per-event frequency, and same-event re-entrancy.
    /// </summary>
    public sealed class EventOrderingBehavior : CampaignBehaviorBase
    {
        private static string _logPath;
        private static readonly object LogLock = new object();

        private long _sequence;
        private readonly Dictionary<string, long> _counts = new Dictionary<string, long>();
        private readonly HashSet<string> _activeNow = new HashSet<string>();
        private DateTime _sessionStartUtc;
        private int _subscribedCount;
        private readonly List<string> _subscribeErrors = new List<string>();

        public override void RegisterEvents()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dir = Path.Combine(docs, "Mount and Blade II Bannerlord", "CampaignEventHarness");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "event-order.log");
            _sessionStartUtc = DateTime.UtcNow;
            Log($"=== CampaignEventHarness session start {_sessionStartUtc:O} ===");

            SubscribeToAll();

            int total = typeof(CampaignEvents)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Length;
            Log($"Subscribed to {_subscribedCount} of {total} CampaignEvents properties.");
            if (_subscribeErrors.Count > 0)
            {
                Log($"{_subscribeErrors.Count} propert(y/ies) could not be subscribed:");
                foreach (string e in _subscribeErrors)
                {
                    Log("  " + e);
                }
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void SubscribeToAll()
        {
            PropertyInfo[] props = typeof(CampaignEvents).GetProperties(BindingFlags.Public | BindingFlags.Static);
            foreach (PropertyInfo prop in props)
            {
                try
                {
                    MethodInfo method = prop.PropertyType.GetMethod("AddNonSerializedListener");
                    if (method == null)
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 2 || !typeof(Delegate).IsAssignableFrom(parameters[1].ParameterType))
                    {
                        continue;
                    }

                    object eventInstance = prop.GetValue(null);
                    if (eventInstance == null)
                    {
                        continue;
                    }

                    Delegate handler = BuildLoggingDelegate(prop.Name, parameters[1].ParameterType);
                    method.Invoke(eventInstance, new object[] { this, handler });
                    _subscribedCount++;
                }
                catch (Exception ex)
                {
                    _subscribeErrors.Add($"{prop.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Builds, at runtime, a delegate of exactly the type each event needs (Action,
        /// Action&lt;T1&gt;, ... Action&lt;T1..T7&gt;) whose body boxes its arguments into an
        /// object[] and forwards to <see cref="OnEvent"/> — so one method handles every arity
        /// without 277 hand-written signatures.
        /// </summary>
        private Delegate BuildLoggingDelegate(string eventName, Type delegateType)
        {
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parms = invoke.GetParameters();
            ParameterExpression[] paramExprs = parms
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();

            Expression argsArray = paramExprs.Length == 0
                ? (Expression)Expression.Constant(Array.Empty<object>())
                : Expression.NewArrayInit(
                    typeof(object),
                    paramExprs.Select(p => (Expression)Expression.Convert(p, typeof(object))));

            MethodInfo onEventMethod = typeof(EventOrderingBehavior).GetMethod(
                nameof(OnEvent), BindingFlags.Instance | BindingFlags.NonPublic);

            Expression body = Expression.Call(
                Expression.Constant(this, typeof(EventOrderingBehavior)),
                onEventMethod,
                Expression.Constant(eventName),
                argsArray);

            return Expression.Lambda(delegateType, body, paramExprs).Compile();
        }

        private void OnEvent(string eventName, object[] args)
        {
            long seq = Interlocked.Increment(ref _sequence);

            lock (LogLock)
            {
                _counts.TryGetValue(eventName, out long count);
                _counts[eventName] = count + 1;
            }

            bool reentrant;
            lock (_activeNow)
            {
                reentrant = _activeNow.Contains(eventName);
                _activeNow.Add(eventName);
            }

            try
            {
                Log($"[{seq}] {eventName}({SummarizeArgs(args)}){(reentrant ? " REENTRANT" : "")}");

                // QuarterHourlyTickEvent is one of the 277 and fires often enough to double
                // as a periodic checkpoint for a running frequency summary, without a
                // separate timer.
                if (eventName == "QuarterHourlyTickEvent")
                {
                    WriteFrequencySummary();
                }
            }
            finally
            {
                lock (_activeNow)
                {
                    if (!reentrant)
                    {
                        _activeNow.Remove(eventName);
                    }
                }
            }
        }

        private static string SummarizeArgs(object[] args)
        {
            if (args.Length == 0)
            {
                return "";
            }

            var parts = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                parts[i] = Summarize(args[i]);
            }
            return string.Join(", ", parts);
        }

        private static string Summarize(object value)
        {
            if (value == null)
            {
                return "null";
            }

            switch (value)
            {
                case string s:
                    return $"\"{s}\"";
                case bool b:
                    return b ? "true" : "false";
                case Enum:
                    return value.ToString();
                case int or long or uint or float or double:
                    return value.ToString();
            }

            // Anything else: just the runtime type name. Never call an arbitrary ToString()
            // on a game object that might be mid-construction inside its own constructor's
            // event firing.
            return value.GetType().Name;
        }

        private void WriteFrequencySummary()
        {
            lock (LogLock)
            {
                Log($"--- frequency summary @ seq {_sequence}, " +
                    $"{DateTime.UtcNow - _sessionStartUtc:hh\\:mm\\:ss} since session start ---");
                foreach (KeyValuePair<string, long> kv in _counts.OrderByDescending(kv => kv.Value).Take(20))
                {
                    Log($"    {kv.Value,8}  {kv.Key}");
                }
            }
        }

        private static void Log(string line)
        {
            lock (LogLock)
            {
                string stamped = $"{DateTime.UtcNow:O} {line}";
                Console.WriteLine("[CampaignEventHarness] " + stamped);
                try
                {
                    if (_logPath != null)
                    {
                        File.AppendAllText(_logPath, stamped + Environment.NewLine);
                    }
                }
                catch
                {
                    // Best-effort; the in-game console output above is the fallback record.
                }
            }
        }
    }
}

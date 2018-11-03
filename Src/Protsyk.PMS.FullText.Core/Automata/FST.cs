using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Protsyk.PMS.FullText.Core.Collections;
using Protsyk.PMS.FullText.Core.Common;

namespace Protsyk.PMS.FullText.Core.Automata
{
    public static class FSTExtensions
    {
        public static bool TryMatch<T>(this FST<T> fst, IEnumerable<char> input, out T value)
        {
            var outputType = fst.OutputType;
            var v = outputType.Zero();
            var s = fst.Initial;
            foreach (var c in input)
            {
                if (fst.TryMove(s, c, out var to, out var o))
                {
                    s = to;
                    v = outputType.Sum(v, o);
                }
                else
                {
                    value = outputType.Zero();
                    return false;
                }
            }

            value = v;
            return fst.IsFinal(s);
        }
    }

    public class FSTBuilder<T>
    {
        private readonly IDictionary<int, List<StateWithTransitions>> minimalTransducerStatesDictionary;

        private readonly IFSTOutput<T> outputType;

        public FSTBuilder(IFSTOutput<T> outputType)
        {
            this.minimalTransducerStatesDictionary = new Dictionary<int, List<StateWithTransitions>>();
            this.outputType = outputType;
        }

        private class StateWithTransitions
        {
            private static int NextId = 0;

            public int Id { get; private set; }

            public bool IsFinal { get; set; }

            public bool IsFronzen { get; set; }

            public List<Transition> Arcs { get; private set; }

            public StateWithTransitions()
            {
                Id = Interlocked.Increment(ref NextId);
                IsFronzen = false;
                IsFinal = false;
                Arcs = new List<Transition>();
            }

            public int GetDedupHash()
            {
                var result = 0;
                for (int i = 0; i < Arcs.Count; ++i)
                {
                    result = HashCombine.Combine(result,
                                                 Arcs[i].To.Id.GetHashCode(),
                                                 Arcs[i].Input.GetHashCode(),
                                                 Arcs[i].Output.GetHashCode());
                }
                return HashCombine.Combine(result, IsFinal ? 1 : 0);
            }

            public bool IsEquivalent(StateWithTransitions other)
            {
                if (IsFinal != other.IsFinal)
                    return false;

                if (Arcs.Count != other.Arcs.Count)
                    return false;

                foreach (var arc in Arcs)
                {
                    bool found = false;
                    foreach (var otherArc in other.Arcs)
                    {
                        if (arc.To == otherArc.To &&
                            arc.Input == otherArc.Input &&
                            arc.Output.Equals(otherArc.Output))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private struct Transition
        {
            public StateWithTransitions To { get; set; }

            public char Input { get; set; }

            public T Output { get; set; }
        }

        private static StateWithTransitions CopyOf(StateWithTransitions s)
        {
            var t = new StateWithTransitions();
            t.IsFinal = s.IsFinal;
            t.Arcs.AddRange(s.Arcs);
            return t;
        }

        private StateWithTransitions FindMinimized(StateWithTransitions s)
        {
            bool minimize = true;
            if (!minimize)
            {
                var r = CopyOf(s);
                r.IsFronzen = true;
                return r;
            }
            else
            {
                var h = s.GetDedupHash();
                if (!minimalTransducerStatesDictionary.TryGetValue(h, out var l))
                {
                    l = new List<StateWithTransitions>();
                    minimalTransducerStatesDictionary.Add(h, l);
                }

                for (int i = 0; i < l.Count; ++i)
                {
                    if (l[i].IsEquivalent(s))
                    {
                        return l[i];
                    }
                }

                var r = CopyOf(s);
                r.IsFronzen = true;
                l.Add(r);
                return r;
            }
        }

        private void SetTransition(StateWithTransitions from, char c, StateWithTransitions to)
        {
            if (from.IsFronzen) throw new Exception("What?");

            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                if (from.Arcs[i].Input == c)
                {
                    from.Arcs[i] = new Transition { Output = from.Arcs[i].Output, Input = c, To = to };
                    return;
                }
            }

            from.Arcs.Add(new Transition { Output = outputType.Zero(), Input = c, To = to });
        }

        private static T GetOutput(StateWithTransitions from, char c)
        {
            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                if (from.Arcs[i].Input == c)
                {
                    return from.Arcs[i].Output;
                }
            }
            throw new Exception("Nothing");
        }

        private static void SetOutput(StateWithTransitions from, char c, T output)
        {
            if (from.IsFronzen) throw new Exception("What?");

            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                if (from.Arcs[i].Input == c)
                {
                    from.Arcs[i] = new Transition
                    {
                        Output = output,
                        Input = from.Arcs[i].Input,
                        To = from.Arcs[i].To
                    };
                    return;
                }
            }
            throw new Exception("Nothing");
        }

        private static void PropagateOutput(StateWithTransitions from, T output, IFSTOutput<T> outputType)
        {
            if (from.IsFronzen) throw new Exception("What?");

            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                from.Arcs[i] = new Transition
                {
                    Output = outputType.Sum(from.Arcs[i].Output, output),
                    Input = from.Arcs[i].Input,
                    To = from.Arcs[i].To
                };
            }
        }

        private static void ClearState(StateWithTransitions s)
        {
            if (s.IsFronzen) throw new Exception("What?");

            s.IsFinal = false;
            s.Arcs.Clear();
        }

        private void Print(StateWithTransitions initial)
        {
            var visited = new HashSet<StateWithTransitions>();
            visited.Add(initial);
            Print(initial, visited);
        }

        private void Print(StateWithTransitions s, HashSet<StateWithTransitions> v)
        {
            foreach (var arc in s.Arcs)
            {
                Console.WriteLine(s.Id + " --- " + arc.Input + ((!arc.Output.Equals(outputType.Zero())) ? $"({arc.Output})" : "") + " --> " + arc.To.Id + (arc.To.IsFinal ? " (F)" : ""));
            }

            foreach (var arc in s.Arcs)
            {
                if (!v.Contains(arc.To))
                {
                    v.Add(arc.To);
                    Print(arc.To, v);
                }
            }
        }

        private static void Build(FST<T> output, StateWithTransitions s, Dictionary<int, int> map, HashSet<StateWithTransitions> visited)
        {
            if (!s.IsFronzen) throw new Exception("What? What are you doing? This state can still change");

            if (!map.TryGetValue(s.Id, out var fromFstState))
            {
                fromFstState = output.AddState().Id;
                output.SetFinal(fromFstState, s.IsFinal);
                map.Add(s.Id, fromFstState);
            }

            foreach (var arc in s.Arcs.OrderBy(a => a.Input))
            {
                if (!map.TryGetValue(arc.To.Id, out var toFstState))
                {
                    toFstState = output.AddState().Id;
                    output.SetFinal(toFstState, arc.To.IsFinal);
                    map.Add(arc.To.Id, toFstState);
                }

                output.AddTransition(fromFstState, arc.Input, toFstState, arc.Output);
            }

            foreach (var arc in s.Arcs)
            {
                if (!visited.Contains(arc.To))
                {
                    visited.Add(arc.To);
                    Build(output, arc.To, map, visited);
                }
            }
        }

        private static void SetFinal(StateWithTransitions s)
        {
            if (s.IsFronzen) throw new Exception("What?");

            s.IsFinal = true;
        }

        public FST<T> FromList(string[] inputs, T[] outputs)
        {
            var maxWordSize = 1 + inputs.Max(x => x.Length);
            var tempState = new StateWithTransitions[maxWordSize];
            for (int i = 0; i < maxWordSize; ++i)
            {
                tempState[i] = new StateWithTransitions();
            }
            var previousWord = string.Empty;
            var currentWord = default(string);
            for (int j = 0; j < inputs.Length; ++j)
            {
                currentWord = inputs[j];

                if (System.StringComparer.Ordinal.Compare(currentWord, previousWord) <= 0)
                {
                    throw new Exception($"Input should be ordered and each item should be unique");
                }

                var currentOutput = outputs[j];
                var prefixLengthPlusOne = 1 + Utils.LCP(previousWord, currentWord);

                if (prefixLengthPlusOne == 1 + currentWord.Length)
                {
                    throw new Exception($"Duplicate input {currentWord}");
                }

                // Minimize the states from suffix of the previous word
                for (int i = previousWord.Length; i >= prefixLengthPlusOne; --i)
                {
                    SetTransition(tempState[i - 1],
                                  previousWord[i - 1],
                                  FindMinimized(tempState[i]));
                }

                // Initialize tail the states for the current word
                for (int i = prefixLengthPlusOne; i < currentWord.Length + 1; ++i)
                {
                    ClearState(tempState[i]);
                    SetTransition(tempState[i - 1],
                                  currentWord[i - 1],
                                  tempState[i]);
                }

                SetFinal(tempState[currentWord.Length]);

                // Set outputs
                for (int i = 1; i < prefixLengthPlusOne; ++i)
                {
                    var output = GetOutput(tempState[i - 1], currentWord[i - 1]);
                    var commonOutput = outputType.Min(output, currentOutput);
                    if (!commonOutput.Equals(output))
                    {
                        var suffixOutput = outputType.Sub(output, commonOutput);
                        SetOutput(tempState[i - 1], currentWord[i - 1], commonOutput);
                        if (!suffixOutput.Equals(outputType.Zero()))
                        {
                            if (tempState[i].IsFinal || tempState[i].Arcs.Count == 0)
                            {
                                throw new Exception("What?");
                            }
                            else
                            {
                                PropagateOutput(tempState[i], suffixOutput, outputType);
                            }
                        }
                    }
                    currentOutput = outputType.Sub(currentOutput, commonOutput);
                }

                SetOutput(tempState[prefixLengthPlusOne - 1], currentWord[prefixLengthPlusOne - 1], currentOutput);

                previousWord = currentWord;
            }

            for (int i = currentWord.Length; i > 0; --i)
            {
                SetTransition(tempState[i - 1],
                              previousWord[i - 1],
                              FindMinimized(tempState[i]));
            }

            var initial = FindMinimized(tempState[0]);

            // Reorder states, states that have more incoming transitions should have smaller ids
            // see FromBytesCompressed
            var result = new FST<T>(outputType);
            result.Initial = result.AddState().Id;
            var map = new Dictionary<int, int>();
            map.Add(initial.Id, result.Initial);
            var visited = new HashSet<StateWithTransitions>();
            visited.Add(initial);
            Build(result, initial, map, visited);

            // Print(initial);

            return result;
        }
    }

    public interface IFST<T>
    {
        int Initial { get; set; }

        bool IsFinal(int stateId);

        bool TryMove(int fromId, char c, out int toId, out T o);

        State AddState();

        void AddTransition(int fromId, char c, int toId, T output);

        void SetFinal(int stateId, bool isFinal);
    }

    public class FST<T> : IFST<T>
    {
        #region Fields
        private readonly List<State> states;

        private readonly HashSet<int> final;

        private readonly Dictionary<int, List<Arc<T>>> trans;

        private readonly IFSTOutput<T> outputType;
        #endregion

        #region Properties
        public IFSTOutput<T> OutputType => outputType;
        #endregion

        #region Methods
        public FST(IFSTOutput<T> outputType)
        {
            this.states = new List<State>();
            this.final = new HashSet<int>();
            this.trans = new Dictionary<int, List<Arc<T>>>();
            this.outputType = outputType;

            Initial = 0;
        }
        #endregion

        #region Serialization
        public byte[] GetBytes()
        {
            var size = Numeric.GetByteSize(Initial);
            for (int i = 0; i < states.Count; ++i)
            {
                size += Numeric.GetByteSize(states[i].Id | (IsFinal(states[i].Id) ? 0x40000000 : 0)) /* State Id + Flag: IsFinal */;
                if (trans.TryGetValue(states[i].Id, out var ts))
                {
                    size += Numeric.GetByteSize(ts.Count) /* Transition count */;
                    foreach (var t in ts)
                    {
                        size += Numeric.GetByteSize(t.Input) +
                                Numeric.GetByteSize(t.To) + 
                                outputType.GetByteSize(t.Output);
                    }
                }
                else
                {
                    size += Numeric.GetByteSize(0) /* Transition count */;
                }
            }

            var result = new byte[size];
            var writeIndex = 0;
            writeIndex += Numeric.WriteInt(Initial, result, writeIndex);
            for (int i = 0; i < states.Count; ++i)
            {
                // To read id (v & 0x3FFFFFFF)
                // To check for final (v & 0x40000000) == 0x40000000
                writeIndex += Numeric.WriteInt(states[i].Id | (IsFinal(states[i].Id) ? 0x40000000 : 0), result, writeIndex);
                if (trans.TryGetValue(states[i].Id, out var ts))
                {
                    writeIndex += Numeric.WriteInt(ts.Count, result, writeIndex);
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        writeIndex += Numeric.WriteInt(ts[j].Input, result, writeIndex);
                        writeIndex += outputType.WriteTo(ts[j].Output, result, writeIndex);
                        writeIndex += Numeric.WriteInt(ts[j].To, result, writeIndex);
                    }
                }
                else
                {
                    writeIndex += Numeric.WriteInt(0, result, writeIndex);
                }
            }
            if (writeIndex != result.Length)
            {
                throw new Exception($"What is going on? {writeIndex} != {result.Length}");
            }
            return result;
        }

        public static FST<T> FromBytes(byte[] data, IFSTOutput<T> outputType)
        {
            var fst = new FST<T>(outputType);
            var readIndex = 0;
            readIndex += Numeric.ReadInt(data, readIndex, out var v);
            fst.Initial = v;
            while (readIndex != data.Length)
            {
                readIndex += Numeric.ReadInt(data, readIndex, out v);
                var sId = v & 0x3FFFFFFF;
                var s = fst.AddState();
                if (s.Id != sId)
                {
                    throw new Exception("Read error");
                }

                if ((v & 0x40000000) == 0x40000000)
                {
                    fst.SetFinal(sId, true);
                }

                readIndex += Numeric.ReadInt(data, readIndex, out var tsCount);
                for (int i = 0; i < tsCount; ++i)
                {
                    readIndex += Numeric.ReadInt(data, readIndex, out var input);
                    readIndex += outputType.ReadFrom(data, readIndex, out var output);
                    readIndex += Numeric.ReadInt(data, readIndex, out var toId);

                    fst.AddTransition(sId, (char)input, toId, output);
                }
            }
            return fst;
        }

        public byte[] GetBytesCompressed()
        {
            var size = 0;
            size += VarInt.GetByteSize((uint)Initial);
            for (int i = 0; i < states.Count; ++i)
            {
                if (trans.TryGetValue(states[i].Id, out var ts) && (ts.Count > 0))
                {
                    size += VarInt.GetByteSize(((uint)ts.Count << 1) | (IsFinal(states[i].Id) ? 1u : 0u));
                    var prev = 0;
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        var next = (int)ts[j].Input;
                        size += VarInt.GetByteSize((uint)(next - prev));
                        size += outputType.GetByteSize(ts[j].Output);
                        size += VarInt.GetByteSize((uint)ts[j].To);
                        prev = next;
                    }
                }
                else
                {
                    size += VarInt.GetByteSize(IsFinal(states[i].Id) ? 1u : 0u);
                }
            }

            var result = new byte[size];
            var writeIndex = 0;
            writeIndex += VarInt.WriteVInt32(Initial, result, writeIndex);
            for (int i = 0; i < states.Count; ++i)
            {
                if (trans.TryGetValue(states[i].Id, out var ts) && (ts.Count > 0))
                {
                    writeIndex += VarInt.WriteVInt32((ts.Count << 1) | (IsFinal(states[i].Id) ? 1 : 0), result, writeIndex);
                    var prev = 0;
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        var next = (int)ts[j].Input;
                        writeIndex += VarInt.WriteVInt32((next - prev), result, writeIndex);
                        writeIndex += outputType.WriteTo(ts[j].Output, result, writeIndex);
                        writeIndex += VarInt.WriteVInt32(ts[j].To, result, writeIndex);
                        prev = next;
                    }
                }
                else
                {
                    writeIndex += VarInt.WriteVInt32(IsFinal(states[i].Id) ? 1 : 0, result, writeIndex);
                }
            }
            if (writeIndex != result.Length)
            {
                throw new Exception("What is going on?");
            }
            return result;
        }

        public static FST<T> FromBytesCompressed(byte[] data, IFSTOutput<T> outputType)
        {
            var fst = new FST<T>(outputType);
            var readIndex = 0;
            var sId = 0;
            readIndex += VarInt.ReadVInt32(data, readIndex, out var initial);
            fst.Initial = (int)initial;
            while (readIndex != data.Length)
            {
                readIndex += VarInt.ReadVInt32(data, readIndex, out var v);
                var s = fst.AddState();
                if (s.Id != sId)
                {
                    throw new Exception("Read error");
                }

                if ((v & 1) == 1)
                {
                    fst.SetFinal(sId, true);
                }

                int tsCount = (int)(v >> 1);
                if (tsCount > 0)
                {
                    int prev = 0;
                    for (int i = 0; i < tsCount; ++i)
                    {
                        readIndex += VarInt.ReadVInt32(data, readIndex, out var input);
                        readIndex += outputType.ReadFrom(data, readIndex, out var output);
                        readIndex += VarInt.ReadVInt32(data, readIndex, out var toId);

                        fst.AddTransition(sId, (char)(input + prev), toId, output);
                        prev = (int)(input + prev);
                    }
                }

                ++sId;
            }
            return fst;
        }
        #endregion

        #region IFST
        public int Initial { get; set; }

        public State AddState()
        {
            var s = new State { Id = states.Count };
            states.Add(s);
            trans.Add(s.Id, new List<Arc<T>>());
            return s;
        }

        public void AddTransition(int fromId, char c, int toId, T output)
        {
            if (!trans.TryGetValue(fromId, out var t))
            {
                throw new Exception("Bad state");
            }

            if (t.Count > 0 && t[t.Count - 1].Input >= c)
            {
                throw new Exception("Bad transition: should be ordered and unique");
            }

            t.Add(new Arc<T>
            {
                From = fromId,
                Input = c,
                To = toId,
                Output = output
            });
        }

        public void SetFinal(int stateId, bool isFinal)
        {
            if (isFinal)
            {
                final.Add(stateId);
            }
            else
            {
                final.Remove(stateId);
            }
        }

        public bool IsFinal(int stateId)
        {
            return final.Contains(stateId);
        }

        public bool TryMove(int fromId, char c, out int toId, out T o)
        {
            if (trans.TryGetValue(fromId, out var ts))
            {
                if (ts.Count < 8)
                {
                    foreach (var t in ts)
                    {
                        if (t.Input == c)
                        {
                            toId = t.To;
                            o = t.Output;
                            return true;
                        }
                    }
                }
                else
                {
                    var a = 0;
                    var b = ts.Count;
                    while (a != b)
                    {
                        var mid = (a + b) >> 1;
                        var s = ts[mid];
                        if (s.Input > c)
                        {
                            b = mid;
                        }
                        else if (s.Input < c)
                        {
                            a = mid;
                        }
                        else
                        {
                            toId = s.To;
                            o = s.Output;
                            return true;
                        }
                    }
                }
            }

            toId = -1;
            o = default(T);
            return false;
        }

        public IEnumerable<IEnumerable<char>> Match(IDfaMatcher<char> matcher)
        {
            var result = new List<string>();
            var prefix = new List<char>();
            MatchRecursive(matcher, Initial, result, prefix);
            return result;
        }

        private void MatchRecursive(IDfaMatcher<char> matcher, int s, List<string> result, List<char> prefix)
        {
            if (IsFinal(s) && matcher.IsFinal())
            {
                result.Add(new string(prefix.ToArray()));
            }

            if (trans.TryGetValue(s, out var ts))
            {
                foreach (var t in ts)
                {
                    if (matcher.Next(t.Input))
                    {
                        prefix.Add(t.Input);

                        MatchRecursive(matcher, t.To, result, prefix);

                        prefix.RemoveAt(prefix.Count - 1);
                        matcher.Pop();
                    }
                }
            }
        }
        #endregion
    }

    public struct State : IEquatable<State>
    {
        public static readonly State NoState = new State { Id = -1 };

        public int Id { get; set; }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(State other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return Equals((State)obj);
        }
    }

    public struct Arc<T> : IEquatable<Arc<T>>
    {
        public int From { get; set; }

        public int To { get; set; }

        public char Input { get; set; }

        public T Output { get; set; }

        public override int GetHashCode()
        {
            return HashCombine.Combine(From.GetHashCode(), To.GetHashCode(), Input.GetHashCode(), Output.GetHashCode());
        }

        public bool Equals(Arc<T> other)
        {
            return From.Equals(other.From) &&
                   To.Equals(other.To) &&
                   Input.Equals(other.Input) &&
                   Output.Equals(other.Output);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return Equals((Arc<T>)obj);
        }
    }

    public interface IFSTOutput<T>
    {
        T Zero();

        T Sum(T a, T b);

        T Sub(T a, T b);

        T Min(T a, T b);

        int GetByteSize(T value);

        int ReadFrom(byte[] buffer, int startIndex, out T result);

        int WriteTo(T value, byte[] buffer, int startIndex);
    }

    public abstract class FSTIntOutputBase : IFSTOutput<int>
    {
        protected FSTIntOutputBase() {}

        public int Min(int a, int b) => Math.Min(a, b);

        public int Sub(int a, int b) => a - b;

        public int Sum(int a, int b) => a + b;

        public int Zero() => 0;

        public abstract int GetByteSize(int value);

        public abstract int ReadFrom(byte[] buffer, int startIndex, out int result);

        public abstract int WriteTo(int value, byte[] buffer, int startIndex);
    }

    public class FSTVarIntOutput : FSTIntOutputBase
    {
        public static readonly FSTVarIntOutput Instance = new FSTVarIntOutput();

        public override int GetByteSize(int value)
        {
            return VarInt.GetByteSize((uint)value);
        }

        public override int ReadFrom(byte[] buffer, int startIndex, out int result)
        {
            return VarInt.ReadVInt32(buffer, startIndex, out result);
        }

        public override int WriteTo(int value, byte[] buffer, int startIndex)
        {
            return VarInt.WriteVInt32(value, buffer, startIndex);
        }
    }

    public class FSTIntOutput : FSTIntOutputBase
    {
        public static readonly FSTIntOutput Instance = new FSTIntOutput();

        public override int GetByteSize(int value)
        {
            return Numeric.GetByteSize(value);
        }

        public override int ReadFrom(byte[] buffer, int startIndex, out int result)
        {
            return Numeric.ReadInt(buffer, startIndex, out result);
        }

        public override int WriteTo(int value, byte[] buffer, int startIndex)
        {
            return Numeric.WriteInt(value, buffer, startIndex);
        }
    }

    public class FSTStringOutput : IFSTOutput<string>
    {
        public static readonly FSTStringOutput Instance = new FSTStringOutput();

        private FSTStringOutput() {}

        public string Min(string a, string b)
        {
            return a.Substring(0, Utils.LCP(a, b));
        }

        public string Sub(string a, string b)
        {
            if (b == string.Empty) return a;
            if (a.Length == b.Length) return string.Empty;
            if (a.Length < b.Length) throw new ArgumentException();
            return a.Substring(b.Length);
        }

        public string Sum(string a, string b)
        {
            return a + b;
        }

        public string Zero() => string.Empty;

        public int GetByteSize(string value)
        {
            var size = Encoding.UTF8.GetByteCount(value);
            return VarInt.GetByteSize((ulong)size) + size;
        }

        public int ReadFrom(byte[] buffer, int startIndex, out string result)
        {
            var size = VarInt.ReadVInt32(buffer, startIndex, out var byteCount);
            result = Encoding.UTF8.GetString(buffer, startIndex + size, byteCount);
            return size + byteCount;
        }

        public int WriteTo(string value, byte[] buffer, int startIndex)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var size = VarInt.WriteVInt32(bytes.Length, buffer, startIndex);
            Array.Copy(bytes, 0, buffer, startIndex+size, bytes.Length);
            return size + bytes.Length;
        }
    }

    internal class Utils
    {
        // Calculate length of the longest common prefix
        public static int LCP(string a, string b)
        {
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i])
            {
                ++i;
            }
            return i;
        }
    }
}

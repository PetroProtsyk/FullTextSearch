using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Protsyk.PMS.FullText.Core.Collections;

namespace Protsyk.PMS.FullText.Core.Automata
{
    public static class FSTExt
    {
        public static bool TryMatch<T, V>(this FST<T> fst, IEnumerable<char> input, Func<V, T, V> aggregate, out V value)
        {
            var v = default(V);
            var s = fst.Initial;
            foreach (var c in input)
            {
                if (fst.TryMove(s, c, out var to, out var o))
                {
                    s = to;
                    v = aggregate(v, o);
                }
                else
                {
                    value = default(V);
                    return false;
                }
            }

            value = v;
            return fst.IsFinal(s);
        }

        // Calculate length of the longest common prefix
        private static int LCP(string a, string b)
        {
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i])
            {
                ++i;
            }
            return i;
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
                var seed = 17;
                for (int i = 0; i < Arcs.Count; ++i)
                {
                    seed ^= 11 * (7 * (Arcs[i].To.Id.GetHashCode()) ^
                                      Arcs[i].Input.GetHashCode()) ^
                                      Arcs[i].Output.GetHashCode(); ;
                    seed <<= 3;
                }
                seed ^= IsFinal ? 0b1100110011001100 : 0b0101010101010100;
                return seed;
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
                            arc.Input == otherArc.Input)
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

            public int Output { get; set; }
        }

        private static StateWithTransitions CopyOf(StateWithTransitions s)
        {
            var t = new StateWithTransitions();
            t.IsFinal = s.IsFinal;
            t.Arcs.AddRange(s.Arcs);
            return t;
        }

        private static StateWithTransitions FindMinimized(IDictionary<int, List<StateWithTransitions>> m, StateWithTransitions s)
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
                if (!m.TryGetValue(h, out var l))
                {
                    l = new List<StateWithTransitions>();
                    m.Add(h, l);
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

        private static void SetTransition(StateWithTransitions from, char c, StateWithTransitions to)
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

            from.Arcs.Add(new Transition { Output = 0, Input = c, To = to });
        }

        private static int GetOutput(StateWithTransitions from, char c)
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

        private static void SetOutput(StateWithTransitions from, char c, int output)
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

        private static void PropagateOutput(StateWithTransitions from, int output)
        {
            if (from.IsFronzen) throw new Exception("What?");

            for (int i = 0; i < from.Arcs.Count; ++i)
            {
                from.Arcs[i] = new Transition
                {
                    Output = from.Arcs[i].Output + output,
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

        private static void Print(StateWithTransitions initial)
        {
            var visited = new HashSet<StateWithTransitions>();
            visited.Add(initial);
            Print(initial, visited);
        }

        private static void Print(StateWithTransitions s, HashSet<StateWithTransitions> v)
        {
            foreach (var arc in s.Arcs)
            {
                Console.WriteLine(s.Id + " --- " + arc.Input + ((arc.Output != 0) ? $"({arc.Output})" : "") + " --> " + arc.To.Id + (arc.To.IsFinal ? " (F)" : ""));
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

        private static void Build(FST<int> output, StateWithTransitions s, Dictionary<int, int> map, HashSet<StateWithTransitions> visited)
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

        public static FST<int> FromList(string[] inputs, int[] outputs)
        {
            var minimalTransducerStatesDictionary = new Dictionary<int, List<StateWithTransitions>>();
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

                if (StringComparer.Ordinal.Compare(currentWord, previousWord) <= 0)
                {
                    throw new Exception($"Input should be ordered and each item should be unique");
                }

                var currentOutput = outputs[j];
                var prefixLengthPlusOne = 1 + LCP(previousWord, currentWord);

                if (prefixLengthPlusOne == 1 + currentWord.Length)
                {
                    throw new Exception($"Duplicate input {currentWord}");
                }

                // Minimize the states from suffix of the previous word
                for (int i = previousWord.Length; i >= prefixLengthPlusOne; --i)
                {
                    SetTransition(tempState[i - 1],
                                  previousWord[i - 1],
                                  FindMinimized(minimalTransducerStatesDictionary, tempState[i]));
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
                    var commonOutput = Math.Min(output, currentOutput);
                    var suffixOutput = output - commonOutput;
                    SetOutput(tempState[i - 1], currentWord[i - 1], commonOutput);
                    PropagateOutput(tempState[i], suffixOutput);
                    currentOutput -= commonOutput;
                }

                SetOutput(tempState[prefixLengthPlusOne - 1], currentWord[prefixLengthPlusOne - 1], currentOutput);

                previousWord = currentWord;
            }

            for (int i = currentWord.Length; i > 0; --i)
            {
                SetTransition(tempState[i - 1],
                              previousWord[i - 1],
                              FindMinimized(minimalTransducerStatesDictionary, tempState[i]));
            }

            var initial = FindMinimized(minimalTransducerStatesDictionary, tempState[0]);

            // Reorder states, states that have more incoming transitions should have smaller ids
            // see FromBytesCompressed
            var result = new FST<int>();
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
        private readonly List<State> states = new List<State>();

        private readonly HashSet<int> final = new HashSet<int>();

        private readonly Dictionary<int, List<Arc<T>>> trans = new Dictionary<int, List<Arc<T>>>();
        #endregion

        #region Methods
        public FST()
        {
            Initial = 0;
        }
        #endregion

        #region Serialization
        public byte[] GetBytes(Func<T, int> convertOutput)
        {
            var size = sizeof(int);
            for (int i = 0; i < states.Count; ++i)
            {
                size += sizeof(int) /* State Id + Flag: IsFinal */ + sizeof(int) /* Transition count */;
                if (trans.TryGetValue(states[i].Id, out var ts))
                {
                    size += ts.Count * (3 * sizeof(int)) /* Input + Output + Next State Id */;
                }
            }

            var result = new byte[size];
            var writeIndex = 0;
            writeIndex += WriteInt(Initial, result, writeIndex);
            for (int i = 0; i < states.Count; ++i)
            {
                // To read id (v & 0x3FFFFFFF)
                // To check for final (v & 0x40000000) == 0x40000000
                writeIndex += WriteInt(states[i].Id | (IsFinal(states[i].Id) ? 0x40000000 : 0), result, writeIndex);
                if (trans.TryGetValue(states[i].Id, out var ts))
                {
                    writeIndex += WriteInt(ts.Count, result, writeIndex);
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        writeIndex += WriteInt(ts[j].Input, result, writeIndex);
                        writeIndex += WriteInt(convertOutput(ts[j].Output), result, writeIndex);
                        writeIndex += WriteInt(ts[j].To, result, writeIndex);
                    }
                }
                else
                {
                    writeIndex += WriteInt(0, result, writeIndex);
                }
            }
            if (writeIndex != result.Length)
            {
                throw new Exception("What is going on?");
            }
            return result;
        }

        public static FST<T> FromBytes(byte[] data, Func<int, T> convertOutput)
        {
            var fst = new FST<T>();
            var readIndex = 0;
            readIndex += ReadInt(data, readIndex, out var v);
            fst.Initial = v;
            while (readIndex != data.Length)
            {
                readIndex += ReadInt(data, readIndex, out v);
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

                readIndex += ReadInt(data, readIndex, out var tsCount);
                for (int i = 0; i < tsCount; ++i)
                {
                    readIndex += ReadInt(data, readIndex, out var input);
                    readIndex += ReadInt(data, readIndex, out var output);
                    readIndex += ReadInt(data, readIndex, out var toId);

                    fst.AddTransition(sId, (char)input, toId, convertOutput(output));
                }
            }
            return fst;
        }

        public byte[] GetBytesCompressed(Func<T, int> convertOutput)
        {
            var size = 0;
            var incoming = new int[this.states.Count];
            foreach (var ss in trans.Values)
            {
                foreach (var zz in ss)
                {
                    incoming[zz.To]++;
                }
            }

            var states = this.states.OrderByDescending(s => incoming[s.Id]).ToList();
            var rename = new Dictionary<int, int>();
            for (int i = 0; i < states.Count; ++i)
            {
                rename[states[i].Id] = i;
            }
            size += SizeVInt((uint)(rename[Initial]));
            for (int i = 0; i < states.Count; ++i)
            {
                if (trans.TryGetValue(states[i].Id, out var ts) && (ts.Count > 0))
                {
                    size += SizeVInt(((uint)ts.Count << 1) | (IsFinal(states[i].Id) ? 1u : 0u));
                    var prev = 0;

                    var compressOutput = ts.Count > 1; // false
                    if (compressOutput)
                    {
                        var vo = 0;
                        var cc = 0;
                        for (int j = 0; j < ts.Count; ++j)
                        {
                            vo <<= 1;
                            if (convertOutput(ts[j].Output) != 0)
                            {
                                vo |= 1;
                            }
                            ++cc;
                            if (cc == 8)
                            {
                                size += 1;
                                cc = 0;
                                vo = 0;
                            }
                        }
                        if (cc > 0)
                        {
                            size += 1;
                        }
                    }
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        var next = (int)ts[j].Input;
                        size += SizeVInt((uint)(next - prev));
                        if (!compressOutput || convertOutput(ts[j].Output) != 0)
                        {
                            size += SizeVInt((uint)convertOutput(ts[j].Output));
                        }
                        size += SizeVInt((uint)(rename[ts[j].To]));
                        prev = next;
                    }
                }
                else
                {
                    size += SizeVInt(IsFinal(states[i].Id) ? 1u : 0u);
                }
            }

            var result = new byte[size];
            var writeIndex = 0;
            writeIndex += WriteVInt((uint)(rename[Initial]), result, writeIndex);
            for (int i = 0; i < states.Count; ++i)
            {
                if (trans.TryGetValue(states[i].Id, out var ts) && (ts.Count > 0))
                {
                    writeIndex += WriteVInt(((uint)ts.Count << 1) | (IsFinal(states[i].Id) ? 1u : 0u), result, writeIndex);

                    var compressOutput = ts.Count > 1; // false
                    if (compressOutput)
                    {
                        var vo = 0;
                        var cc = 0;
                        for (int j = 0; j < ts.Count; ++j)
                        {
                            vo <<= 1;
                            if (convertOutput(ts[j].Output) != 0)
                            {
                                vo |= 1;
                            }
                            ++cc;
                            if (cc == 8)
                            {
                                result[writeIndex] = (byte)vo;
                                writeIndex += 1;
                                cc = 0;
                                vo = 0;
                            }
                        }
                        if (cc > 0)
                        {
                            result[writeIndex] = (byte)(vo << 8 - cc);
                            writeIndex += 1;
                        }
                    }
                    var prev = 0;
                    for (int j = 0; j < ts.Count; ++j)
                    {
                        var next = (int)ts[j].Input;
                        writeIndex += WriteVInt((uint)(next - prev), result, writeIndex);
                        if (!compressOutput || convertOutput(ts[j].Output) != 0)
                        {
                            writeIndex += WriteVInt((uint)convertOutput(ts[j].Output), result, writeIndex);
                        }
                        writeIndex += WriteVInt((uint)(rename[ts[j].To]), result, writeIndex);
                        prev = next;
                    }
                }
                else
                {
                    writeIndex += WriteVInt(IsFinal(states[i].Id) ? 1u : 0u, result, writeIndex);
                }
            }
            if (writeIndex != result.Length)
            {
                throw new Exception("What is going on?");
            }
            return result;
        }

        public static FST<T> FromBytesCompressed(byte[] data, Func<int, T> convertOutput)
        {
            var fst = new FST<T>();
            var readIndex = 0;
            var sId = 0;
            readIndex += ReadVInt(data, readIndex, out var initial);
            fst.Initial = (int)initial;
            while (readIndex != data.Length)
            {
                readIndex += ReadVInt(data, readIndex, out var v);
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
                    var compressOutput = tsCount > 1; // false
                    var ctrl = new byte[(tsCount + 7) / 8];
                    if (compressOutput)
                    {
                        for (int h = 0; h < ctrl.Length; ++h)
                        {
                            ctrl[h] = data[readIndex];
                            ++readIndex;
                        }
                    }
                    int prev = 0;
                    int m = 0x80;
                    int j = 0;
                    for (int i = 0; i < tsCount; ++i)
                    {
                        readIndex += ReadVInt(data, readIndex, out var input);
                        uint output = 0;
                        if (!compressOutput || (ctrl[j] & m) == m)
                        {
                            readIndex += ReadVInt(data, readIndex, out output);
                        }
                        m >>= 1;
                        if (m == 0)
                        {
                            j++;
                            m = 0x80;
                        }
                        readIndex += ReadVInt(data, readIndex, out var toId);

                        fst.AddTransition(sId, (char)(input + prev), (int)toId, convertOutput((int)output));
                        prev = (int)(input + prev);
                    }
                }

                ++sId;
            }
            return fst;
        }

        private static int ReadVInt(byte[] buffer, int startIndex, out uint value)
        {
            var i = startIndex;
            var shift = 0;
            value = 0;
            while ((buffer[i] & 0x80) > 0)
            {
                value |= (uint)((buffer[i++] & 0x7F) << shift);
                shift += 7;
            }
            value |= (uint)(buffer[i++] << shift);
            return i - startIndex;
        }

        private static int ReadInt(byte[] buffer, int startIndex, out int value)
        {
            value = BitConverter.ToInt32(buffer, startIndex);
            return sizeof(int);
        }

        private static int WriteInt(int value, byte[] buffer, int startIndex)
        {
            Array.Copy(BitConverter.GetBytes(value), 0, buffer, startIndex, sizeof(int));
            return sizeof(int);
        }

        private static int SizeVInt(uint value)
        {
            var i = 1;
            while (value > 127)
            {
                value >>= 7;
                ++i;
            }
            return i;
        }

        private static int WriteVInt(uint value, byte[] buffer, int startIndex)
        {
            var i = startIndex;
            while (value > 127)
            {
                buffer[i++] = (byte)(value | 0x80);
                value >>= 7;
            }
            buffer[i++] = (byte)value;
            return i - startIndex;
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
            if (IsFinal(s))
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
            return 13 * (11 * (7 * (From.GetHashCode() ^
                   To.GetHashCode()) ^
                   Input.GetHashCode()) ^
                   Output.GetHashCode());
        }

        public bool Equals(Arc<T> other)
        {
            return From.Equals(other.From) &&
                   To.Equals(other.From) &&
                   Input.Equals(other.Input) &&
                   Output.Equals(other.Input);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            return Equals((Arc<T>)obj);
        }
    }
}

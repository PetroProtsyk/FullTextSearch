using System;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Persistance
{
    public interface IDataSerializer<T>
    {
        byte[] GetBytes(T value);

        T GetValue(byte[] bytes);

        T GetValue(byte[] bytes, int startIndex);
    }

    public interface IFixedSizeDataSerializer<T> : IDataSerializer<T>
    {
        int Size { get; }
    }

    public sealed class NoValue
    {
        public static readonly NoValue Instance = new NoValue();

        private NoValue() {}
    }

    public static class DataSerializer
    {
        private static readonly Dictionary<Type, Func<object>> factories = new Dictionary<Type, Func<object>>()
                                                                            {
                                                                               {typeof(byte), () => new ByteDataSerializer() },
                                                                               {typeof(char), () => new CharDataSerializer()},
                                                                               {typeof(int), () => new IntDataSerializer()},
                                                                               {typeof(long), () => new LongDataSerializer()},
                                                                               {typeof(ulong), () => new ULongDataSerializer()},
                                                                               {typeof(string), () => new StringDataSerializer()},
                                                                               {typeof(Guid), () => new GuidDataSerializer()},
                                                                               {typeof(NoValue), () => new NoValueSerializer()}
                                                                            };

        public static IDataSerializer<T> GetDefault<T>()
        {
            lock (factories)
            {
                Func<object> factory;
                if (factories.TryGetValue(typeof(T), out factory))
                {
                    return (IDataSerializer<T>)factory();
                }
            }
            throw new NotSupportedException($"No default serializer for type {typeof(T)}");
        }

        public static void Register<T>(Func<IDataSerializer<T>> factory)
        {
            lock (factories)
            {
                factories.Add(typeof(T), factory);
            }
        }
    }

    internal class ByteDataSerializer : IFixedSizeDataSerializer<byte>
    {
        public int Size => 1;

        public byte[] GetBytes(byte value)
        {
            return new byte[1] { value };
        }

        public byte GetValue(byte[] bytes)
        {
            return bytes[0];
        }

        public byte GetValue(byte[] bytes, int startIndex)
        {
            return bytes[startIndex];
        }
    }

    internal class StringDataSerializer : IDataSerializer<string>
    {
        public byte[] GetBytes(string value)
        {
            // TODO: Save string length?
            return Encoding.UTF8.GetBytes(value);
        }

        public string GetValue(byte[] bytes)
        {
            return GetValue(bytes, 0);
        }

        public string GetValue(byte[] bytes, int startIndex)
        {
            return Encoding.UTF8.GetString(bytes, startIndex, bytes.Length - startIndex);
        }
    }

    internal class IntDataSerializer : IFixedSizeDataSerializer<int>
    {
        public int Size => sizeof(int);

        public byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(value);
        }

        public int GetValue(byte[] bytes)
        {
            return GetValue(bytes, 0);
        }

        public int GetValue(byte[] bytes, int startIndex)
        {
            return BitConverter.ToInt32(bytes, startIndex);
        }
    }

    internal class LongDataSerializer : IFixedSizeDataSerializer<long>
    {
        public int Size => sizeof(long);

        public byte[] GetBytes(long value)
        {
            return BitConverter.GetBytes(value);
        }

        public long GetValue(byte[] bytes)
        {
            return GetValue(bytes, 0);
        }

        public long GetValue(byte[] bytes, int startIndex)
        {
            return BitConverter.ToInt64(bytes, startIndex);
        }
    }

    internal class ULongDataSerializer : IFixedSizeDataSerializer<ulong>
    {
        public int Size => sizeof(ulong);

        public byte[] GetBytes(ulong value)
        {
            return BitConverter.GetBytes(value);
        }

        public ulong GetValue(byte[] bytes)
        {
            return GetValue(bytes, 0);
        }

        public ulong GetValue(byte[] bytes, int startIndex)
        {
            return BitConverter.ToUInt64(bytes, startIndex);
        }
    }

    internal class GuidDataSerializer : IFixedSizeDataSerializer<Guid>
    {
        public int Size => 16;

        public byte[] GetBytes(Guid value)
        {
            var result =  value.ToByteArray();
            return result;
        }

        public Guid GetValue(byte[] bytes)
        {
            return GetValue(bytes, 0);
        }

        public Guid GetValue(byte[] bytes, int startIndex)
        {
            return new Guid(BitConverter.ToUInt32(bytes, startIndex),
                            BitConverter.ToUInt16(bytes, startIndex + sizeof(uint)),
                            BitConverter.ToUInt16(bytes, startIndex + sizeof(uint) + sizeof(ushort)),
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort)],
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort) + 1],
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort) + 2],
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort) + 3],
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort) + 4],
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort) + 5],
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort) + 6],
                            bytes[startIndex + sizeof(uint) + 2 * sizeof(ushort) + 7]);
        }
    }

    internal class CharDataSerializer : IFixedSizeDataSerializer<char>
    {
        public int Size => sizeof(char);

        public byte[] GetBytes(char value)
        {
            return BitConverter.GetBytes(value);
        }

        public char GetValue(byte[] bytes)
        {
            return GetValue(bytes, 0);
        }

        public char GetValue(byte[] bytes, int startIndex)
        {
            return BitConverter.ToChar(bytes, startIndex);
        }
    }

    internal class NoValueSerializer : IFixedSizeDataSerializer<NoValue>
    {
        public byte[] GetBytes(NoValue value)
        {
            return new byte[0];
        }

        public NoValue GetValue(byte[] bytes)
        {
            return NoValue.Instance;
        }

        public NoValue GetValue(byte[] bytes, int startIndex)
        {
            return NoValue.Instance;
        }

        public int Size
        {
            get { return 0; }
        }
    }
}

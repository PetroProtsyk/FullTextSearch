using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Protsyk.PMS.FullText.Core.Common.Persistance
{
    public interface IDataSerializer<T>
    {
        byte[] GetBytes(T value);

        int GetByteSize(T value);

        T GetValue(ReadOnlySpan<byte> bytes);
    }

    public interface IFixedSizeDataSerializer<T> : IDataSerializer<T>
    {
        int Size { get; }
    }

    public sealed class NoValue
    {
        public static readonly NoValue Instance = new NoValue();

        private NoValue() { }
    }

    public static class DataSerializer
    {
        private static readonly Dictionary<Type, Func<object>> factories = new()
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

    internal sealed class ByteDataSerializer : IFixedSizeDataSerializer<byte>
    {
        public int Size => 1;

        public byte[] GetBytes(byte value)
        {
            return new byte[1] { value };
        }

        public int GetByteSize(byte value) => Size;

        public byte GetValue(ReadOnlySpan<byte> bytes)
        {
            return bytes[0];
        }
    }

    internal sealed class StringDataSerializer : IDataSerializer<string>
    {
        public byte[] GetBytes(string value)
        {
            // TODO: Save string length?
            return Encoding.UTF8.GetBytes(value);
        }

        public int GetByteSize(string value)
        {
            return Encoding.UTF8.GetByteCount(value);
        }

        public string GetValue(ReadOnlySpan<byte> bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    internal sealed class IntDataSerializer : IFixedSizeDataSerializer<int>
    {
        public int Size => sizeof(int);

        public byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(value);
        }

        public int GetByteSize(int value) => Size;

        public int GetValue(ReadOnlySpan<byte> bytes)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }
    }

    internal sealed class LongDataSerializer : IFixedSizeDataSerializer<long>
    {
        public int Size => sizeof(long);

        public byte[] GetBytes(long value)
        {
            return BitConverter.GetBytes(value);
        }

        public int GetByteSize(long value)
        {
            return Size;
        }

        public long GetValue(ReadOnlySpan<byte> bytes)
        {
            return BinaryPrimitives.ReadInt64LittleEndian(bytes);
        }
    }

    internal sealed class ULongDataSerializer : IFixedSizeDataSerializer<ulong>
    {
        public int Size => sizeof(ulong);

        public byte[] GetBytes(ulong value)
        {
            return BitConverter.GetBytes(value);
        }

        public int GetByteSize(ulong value)
        {
            return Size;
        }

        public ulong GetValue(ReadOnlySpan<byte> bytes)
        {
            return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }
    }

    internal sealed class GuidDataSerializer : IFixedSizeDataSerializer<Guid>
    {
        public int Size => 16;

        public byte[] GetBytes(Guid value)
        {
            var result =  value.ToByteArray();
            return result;
        }

        public int GetByteSize(Guid value) => Size;

        public Guid GetValue(ReadOnlySpan<byte> bytes)
        {
            return new Guid(BinaryPrimitives.ReadUInt32LittleEndian(bytes),
                            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(sizeof(uint))),
                            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(sizeof(uint) + sizeof(ushort))),
                            bytes[sizeof(uint) + 2 * sizeof(ushort)],
                            bytes[sizeof(uint) + 2 * sizeof(ushort) + 1],
                            bytes[sizeof(uint) + 2 * sizeof(ushort) + 2],
                            bytes[sizeof(uint) + 2 * sizeof(ushort) + 3],
                            bytes[sizeof(uint) + 2 * sizeof(ushort) + 4],
                            bytes[sizeof(uint) + 2 * sizeof(ushort) + 5],
                            bytes[sizeof(uint) + 2 * sizeof(ushort) + 6],
                            bytes[sizeof(uint) + 2 * sizeof(ushort) + 7]);
        }
    }

    internal sealed class CharDataSerializer : IFixedSizeDataSerializer<char>
    {
        public int Size => sizeof(char);

        public byte[] GetBytes(char value)
        {
            return BitConverter.GetBytes(value);
        }

        public int GetByteSize(char value) => Size;

        public char GetValue(ReadOnlySpan<byte> bytes)
        {
            return BitConverter.ToChar(bytes);
        }
    }

    internal sealed class NoValueSerializer : IFixedSizeDataSerializer<NoValue>
    {
        public int Size => 0;

        public byte[] GetBytes(NoValue value)
        {
            return Array.Empty<byte>();
        }

        public int GetByteSize(NoValue value)
        {
            return Size;
        }

        public NoValue GetValue(ReadOnlySpan<byte> bytes)
        {
            return NoValue.Instance;
        }
    }
}

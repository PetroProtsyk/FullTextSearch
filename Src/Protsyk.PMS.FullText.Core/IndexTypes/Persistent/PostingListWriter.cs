using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Text;

using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core;

public class PostingListWriter : IOccurrenceWriter
{
    #region Fields
    public static readonly string Id = "Text";

    internal static ReadOnlySpan<byte> EmptyContinuationAddress => " -> FFFFFFFF"u8;

    private static readonly int MemoryBufferThreshold = 65_536;

    private readonly IPersistentStorage persistentStorage;
    private readonly ArrayBufferWriter<byte> buffer;

    private long count;
    private PostingListAddress? currentList;
    #endregion

    public PostingListWriter(string folder, string fileNamePostingLists)
        : this(new FileStorage(Path.Combine(folder, fileNamePostingLists)))
    {
    }

    public PostingListWriter(IPersistentStorage storage)
    {
        this.persistentStorage = storage;
        this.buffer = new ArrayBufferWriter<byte>(MemoryBufferThreshold);
    }

    #region API
    public void StartList(string token)
    {
        if (currentList != null)
        {
            throw new InvalidOperationException("Previous list was not finished");
        }

        count = 0;
        buffer.Clear();
        currentList = new PostingListAddress(persistentStorage.Length);
    }

    public void AddOccurrence(Occurrence occurrence)
    {
        if (currentList is null)
        {
            throw new InvalidOperationException("Previous list was started");
        }

        if (count != 0)
        {
            buffer.Write(";"u8);
        }

        buffer.Write("["u8);
        Utf8Formatter.TryFormat(occurrence.DocumentId, buffer.GetSpan(32), out int bytesWritten);
        buffer.Advance(bytesWritten);

        buffer.Write(","u8);
        Utf8Formatter.TryFormat(occurrence.FieldId, buffer.GetSpan(32), out bytesWritten);
        buffer.Advance(bytesWritten);

        buffer.Write(","u8);
        Utf8Formatter.TryFormat(occurrence.TokenId, buffer.GetSpan(32), out bytesWritten);
        buffer.Advance(bytesWritten);

        buffer.Write("]"u8);

        ++count;

        if (buffer.WrittenCount + 128 >= MemoryBufferThreshold)
        {
            persistentStorage.Append(buffer.WrittenSpan);
            buffer.Clear();
        }
    }

    public PostingListAddress EndList()
    {
        if (currentList == null)
        {
            throw new InvalidOperationException("Previous list was started");
        }

        if (buffer.WrittenCount > 0)
        {
            persistentStorage.Append(buffer.WrittenSpan);
            buffer.Clear();
        }

        // This posting list does not have continuation
        persistentStorage.Append(EmptyContinuationAddress);

        var listEnd = persistentStorage.Length;

        persistentStorage.AppendUtf8Bytes(Environment.NewLine);

        var result = currentList.Value;

        currentList = null;
        count = 0;

        return result;
    }

    public void UpdateNextList(PostingListAddress address, PostingListAddress nextList)
    {
        var offset = FindContinuationOffset(address);
        if (offset < 0)
        {
            throw new InvalidOperationException("Continuation is not found. The list might already have it");
        }
        var data = Encoding.UTF8.GetBytes($" -> {nextList.Offset:X8}");
        persistentStorage.WriteAll(offset, data);
    }

    private long FindContinuationOffset(PostingListAddress address)
    {
        var offset = address.Offset;
        var buffer = new byte[PostingListReader.ReadBufferSize];

        while (true)
        {
            int read = persistentStorage.Read(offset, buffer);
            if (read == 0)
            {
                break;
            }

            offset += read;

            for (int i = 0; i < read; ++i)
            {
                if (buffer[i] == EmptyContinuationAddress[0])
                {
                    var readNext = persistentStorage.Read(offset - read + i, buffer.AsSpan(0, EmptyContinuationAddress.Length));
                    var nextOffsetText = Encoding.UTF8.GetString(buffer, 4, 8);
                    var nextOffset = Convert.ToInt32(nextOffsetText, 16);
                    if (nextOffset < 0)
                    {
                        return offset - read + i;
                    }

                    offset = nextOffset;
                    break;
                }
            }
        }

        return -1;
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
        persistentStorage?.Dispose();
    }
    #endregion
}

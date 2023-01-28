using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core.Collections;

/// <summary>
/// Persistent List
/// </summary>
/// <typeparam name="TValue"></typeparam>
public class PersistentList<TValue> : IDisposable
{
    #region Fields
    private readonly IPersistentStorage persistentStorage;
    private readonly long headerSize;
    private readonly int recordSize;
    private readonly byte[] buffer;
    private readonly IFixedSizeDataSerializer<TValue> serializer;

    private long bufferFileOffset;
    private int bufferDataSize;
    private bool dataChanged;
    #endregion

    #region Methods
    public PersistentList(IPersistentStorage persistentStorage)
    {
        this.serializer = (IFixedSizeDataSerializer<TValue>)DataSerializer.GetDefault<TValue>();
        this.persistentStorage = persistentStorage;
        this.headerSize = sizeof(long);
        this.recordSize = serializer.Size;
        this.buffer = new byte[recordSize * 1000];
        this.bufferFileOffset = long.MinValue;
        this.bufferDataSize = 0;
    }

    private int GetFileBufferForIndex(long index)
    {
        var offset = index * recordSize + headerSize;

        if (!(bufferFileOffset <= offset && (offset + recordSize) <= (bufferFileOffset + bufferDataSize)))
        {
            FlushBuffer();

            int read = persistentStorage.Read(offset, buffer);
            Array.Clear(buffer, read, buffer.Length - read);

            bufferFileOffset = offset;
            bufferDataSize = buffer.Length; //TODO: Extend file size to cover buffer?
            dataChanged = false;
        }

        checked
        {
            return (int)(offset - bufferFileOffset);
        }
    }

    private void FlushBuffer()
    {
        if (dataChanged)
        {
            persistentStorage.WriteAll(bufferFileOffset, buffer.AsSpan(0, bufferDataSize));
            dataChanged = false;
        }
    }
    #endregion

    #region API
    public TValue this[long index]
    {
        get
        {
            int offset = GetFileBufferForIndex(index);
            return serializer.GetValue(buffer.AsSpan(offset));
        }
        set
        {
            int offset = GetFileBufferForIndex(index);

            var valueBytes = serializer.GetBytes(value);

            Array.Copy(valueBytes, 0, buffer, offset, valueBytes.Length);
            if (offset + valueBytes.Length > bufferDataSize)
            {
                bufferDataSize = offset + valueBytes.Length;
            }

            dataChanged = true;
        }
    }

    public void Flush()
    {
        FlushBuffer();
        persistentStorage.Flush();
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
        FlushBuffer();
        persistentStorage?.Dispose();
    }
    #endregion
}

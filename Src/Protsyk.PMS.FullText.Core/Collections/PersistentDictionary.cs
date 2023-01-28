using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace Protsyk.PMS.FullText.Core.Collections;

public class PersistentDictionary<TValue> : IDisposable
{
    #region Fields
    private readonly IDataSerializer<TValue> valueSerializer;
    private readonly IPersistentStorage dataStorage;
    private readonly PersistentList<long> linearIndex;

    private byte[] valueBuffer;
    #endregion

    #region Properties

    #endregion

    #region Constructors

    public PersistentDictionary()
        : this(new MemoryStorage(), new MemoryStorage()) { }

    public PersistentDictionary(IPersistentStorage dataStorage, IPersistentStorage indexStorage)
    {
        this.valueSerializer = DataSerializer.GetDefault<TValue>();

        this.dataStorage = dataStorage;
        this.linearIndex = new PersistentList<long>(indexStorage);

        if (this.dataStorage.Length == 0)
        {
            this.dataStorage.WriteAll(0, stackalloc byte[2] { 20, 18 });
        }
    }

    #endregion

    #region Api
    public TValue this[long index]
    {
        get
        {
            var offset = linearIndex[index];
            if (offset == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int dataSize = dataStorage.ReadInt32LittleEndian(offset);

            //TODO: Improve serializer, no need to reallocate
            if (valueBuffer == null || valueBuffer.Length != dataSize)
            {
                valueBuffer = new byte[dataSize];
            }

            dataStorage.ReadAll(offset + 4, valueBuffer);

            return valueSerializer.GetValue(valueBuffer);
        }
        set
        {
            var offset = linearIndex[index];
            if (offset == 0)
            {
                offset = dataStorage.Length;
                linearIndex[index] = dataStorage.Length;
            }

            var data = valueSerializer.GetBytes(value);

            dataStorage.WriteInt32LittleEndian(offset, data.Length);
            dataStorage.WriteAll(offset + sizeof(int), data);
        }
    }
    #endregion

    #region IDisposable
    public void Dispose()
    {
        dataStorage?.Dispose();
        linearIndex?.Dispose();
    }

    #endregion
}

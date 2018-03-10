using System;
using Protsyk.PMS.FullText.Core.Common.Persistance;

namespace PMS.Common.Collections.List
{
    public class PersistentDictionary<TValue> : IDisposable
    {
        #region Fields
        private readonly IDataSerializer<TValue> valueSerializer;
        private readonly IPersistentStorage dataStorage;
        private readonly PersistentList<long> linearIndex;
        private readonly byte[] sizeBuffer;

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
            this.sizeBuffer = new byte[sizeof(int)];

            this.dataStorage = dataStorage;
            this.linearIndex = new PersistentList<long>(indexStorage);

            if (this.dataStorage.Length == 0)
            {
                this.dataStorage.WriteAll(0, new byte[] { 20, 18 }, 0, 2);
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

                dataStorage.ReadAll(offset, sizeBuffer, 0, sizeBuffer.Length);
                int dataSize = BitConverter.ToInt32(sizeBuffer, 0);

                //TODO: Improve serializer, no need to reallocate
                if (valueBuffer == null || valueBuffer.Length != dataSize)
                {
                    valueBuffer = new byte[dataSize];
                }

                dataStorage.ReadAll(offset + sizeBuffer.Length, valueBuffer, 0, valueBuffer.Length);

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
                var dataSize = BitConverter.GetBytes(data.Length);

                dataStorage.WriteAll(offset, dataSize, 0, dataSize.Length);
                dataStorage.WriteAll(offset + dataSize.Length, data, 0, data.Length);
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
}

﻿using Be.Windows.Forms;
using fNbt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TryashtarUtils.Utility;

namespace NbtStudio
{
    // implementations for allowing tags to be edited by the hex window
    public static class ByteProviders
    {
        public static IByteTransformer GetByteProvider(NbtTag tag)
        {
            if (tag is NbtByteArray ba)
                return new ByteArrayByteProvider(ba);
            if (tag is NbtIntArray ia)
                return new IntArrayByteProvider(ia);
            if (tag is NbtLongArray la)
                return new LongArrayByteProvider(la);
            if (tag is NbtList list)
            {
                if (list.ListType == NbtTagType.Byte)
                    return new ByteListByteProvider(list);
                if (list.ListType == NbtTagType.Short)
                    return new ShortListByteProvider(list);
                if (list.ListType == NbtTagType.Int)
                    return new IntListByteProvider(list);
                if (list.ListType == NbtTagType.Long)
                    return new LongListByteProvider(list);
            }
            throw new ArgumentException($"Can't get a byte provider from {tag.TagType}");
        }

        public static bool HasProvider(NbtTag tag)
        {
            if (NbtUtil.IsArrayType(tag.TagType))
                return true;
            if (tag is NbtList list)
            {
                return list.ListType switch
                {
                    NbtTagType.Byte or
                    NbtTagType.Short or
                    NbtTagType.Int or
                    NbtTagType.Long => true,
                    _ => false,
                };
            }
            return false;
        }
    }

    public interface IByteTransformer : IByteProvider
    {
        int BytesPerValue { get; }
        IEnumerable<byte> CurrentBytes { get; }
        void WriteBytes(long initial_index, IEnumerable<byte> bytes);
        void SetBytes(IEnumerable<byte> bytes);
        ICommand Apply();
    }

    // base implementation that handles all kinds of tags
    // derived class just needs to define the single interop between raw bytes and tag data
    public abstract class NbtByteProvider : IByteTransformer
    {
        protected readonly NbtTag Tag;
        private readonly List<byte> Bytes = new();
        public IEnumerable<byte> CurrentBytes => Bytes.AsReadOnly();
        private bool HasChanged = false;
        public NbtByteProvider(NbtTag tag)
        {
            Tag = tag;
            Bytes.AddRange(GetBytesFromTag());
        }
        public event EventHandler LengthChanged;
        public event EventHandler Changed;

        protected abstract IEnumerable<byte> GetBytesFromTag();
        protected abstract ICommand SetBytesToTag(List<byte> bytes);
        public abstract int BytesPerValue { get; }

        protected void OnLengthChanged()
        {
            LengthChanged?.Invoke(this, EventArgs.Empty);
        }

        protected void OnChanged()
        {
            HasChanged = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public long Length => Bytes.Count;
        public void ApplyChanges()
        {
            Apply().Execute();
            DebugLog.WriteLine("ApplyChanges() called, that is probably bad!");
        }

        public ICommand Apply()
        {
            HasChanged = false;
            return SetBytesToTag(Bytes);
        }

        public void DeleteBytes(long index, long length)
        {
            int internal_index = (int)Math.Max(0, index);
            int internal_length = (int)Math.Min((int)Length, length);
            Bytes.RemoveRange(internal_index, internal_length);
            OnLengthChanged();
            OnChanged();
        }

        public void InsertBytes(long index, byte[] bs)
        {
            Bytes.InsertRange((int)index, bs);
            OnLengthChanged();
            OnChanged();
        }

        public void WriteByte(long index, byte value)
        {
            Bytes[(int)index] = value;
            OnChanged();
        }

        public void WriteBytes(long initial_index, IEnumerable<byte> bytes)
        {
            var overwritable = bytes.Take((int)(Length - initial_index)).GetEnumerator();
            var append = bytes.Skip((int)(Length - initial_index));
            for (int i = (int)initial_index; i < Length; i++)
            {
                Bytes[i] = overwritable.Current;
                overwritable.MoveNext();
            }
            if (append.Any())
            {
                Bytes.AddRange(append);
                OnLengthChanged();
            }
            OnChanged();
        }

        public void SetBytes(IEnumerable<byte> bytes)
        {
            Bytes.Clear();
            Bytes.AddRange(bytes);
            OnLengthChanged();
            OnChanged();
        }

        public byte ReadByte(long index) => Bytes[(int)index];

        public bool HasChanges() => HasChanged;
        public bool SupportsDeleteBytes() => true;
        public bool SupportsInsertBytes() => true;
        public bool SupportsWriteByte() => true;
    }

    public class ByteArrayByteProvider : NbtByteProvider
    {
        protected new NbtByteArray Tag => (NbtByteArray)base.Tag;
        public ByteArrayByteProvider(NbtByteArray tag) : base(tag) { }
        public override int BytesPerValue => sizeof(byte);

        protected override IEnumerable<byte> GetBytesFromTag()
        {
            return Tag.Value;
        }

        protected override ICommand SetBytesToTag(List<byte> bytes)
        {
            return new ChangeValueCommand(Tag, bytes.ToArray());
        }
    }

    public class IntArrayByteProvider : NbtByteProvider
    {
        protected new NbtIntArray Tag => (NbtIntArray)base.Tag;
        public IntArrayByteProvider(NbtIntArray tag) : base(tag) { }
        public override int BytesPerValue => sizeof(int);

        protected override IEnumerable<byte> GetBytesFromTag()
        {
            return DataUtils.ToByteArray(Tag.Value);
        }

        protected override ICommand SetBytesToTag(List<byte> bytes)
        {
            return new ChangeValueCommand(Tag, DataUtils.ToIntArray(bytes.ToArray()));
        }
    }

    public class LongArrayByteProvider : NbtByteProvider
    {
        protected new NbtLongArray Tag => (NbtLongArray)base.Tag;
        public LongArrayByteProvider(NbtLongArray tag) : base(tag) { }
        public override int BytesPerValue => sizeof(long);

        protected override IEnumerable<byte> GetBytesFromTag()
        {
            return DataUtils.ToByteArray(Tag.Value);
        }

        protected override ICommand SetBytesToTag(List<byte> bytes)
        {
            return new ChangeValueCommand(Tag, DataUtils.ToLongArray(bytes.ToArray()));
        }
    }

    public abstract class NbtListByteProvider : NbtByteProvider
    {
        protected new NbtList Tag => (NbtList)base.Tag;
        public NbtListByteProvider(NbtList list) : base(list) { }
    }

    public class ByteListByteProvider : NbtListByteProvider
    {
        public ByteListByteProvider(NbtList list) : base(list) { }
        public override int BytesPerValue => sizeof(byte);

        protected override IEnumerable<byte> GetBytesFromTag()
        {
            return Tag.Tags.Cast<NbtByte>().Select(x => x.Value);
        }

        protected override ICommand SetBytesToTag(List<byte> bytes)
        {
            return new MergedCommand($"Replace byte tags of {CommandExtensions.Describe(Tag)}",
                new ClearCommand(Tag),
                new AddRangeCommand(Tag, bytes.Select(x => new NbtByte(x)))
            );
        }
    }

    public class ShortListByteProvider : NbtListByteProvider
    {
        public ShortListByteProvider(NbtList list) : base(list) { }
        public override int BytesPerValue => sizeof(short);

        protected override IEnumerable<byte> GetBytesFromTag()
        {
            var shorts = Tag.Tags.Cast<NbtShort>().Select(x => x.Value);
            return DataUtils.ToByteArray(shorts.ToArray());
        }

        protected override ICommand SetBytesToTag(List<byte> bytes)
        {
            var shorts = DataUtils.ToShortArray(bytes.ToArray());
            return new MergedCommand($"Replace short tags of {CommandExtensions.Describe(Tag)}",
                new ClearCommand(Tag),
                new AddRangeCommand(Tag, shorts.Select(x => new NbtShort(x)))
            );
        }
    }

    public class IntListByteProvider : NbtListByteProvider
    {
        public IntListByteProvider(NbtList list) : base(list) { }
        public override int BytesPerValue => sizeof(int);

        protected override IEnumerable<byte> GetBytesFromTag()
        {
            var ints = Tag.Tags.Cast<NbtInt>().Select(x => x.Value);
            return DataUtils.ToByteArray(ints.ToArray());
        }

        protected override ICommand SetBytesToTag(List<byte> bytes)
        {
            var ints = DataUtils.ToIntArray(bytes.ToArray());
            return new MergedCommand($"Replace int tags of {CommandExtensions.Describe(Tag)}",
                new ClearCommand(Tag),
                new AddRangeCommand(Tag, ints.Select(x => new NbtInt(x)))
            );
        }
    }

    public class LongListByteProvider : NbtListByteProvider
    {
        public LongListByteProvider(NbtList list) : base(list) { }
        public override int BytesPerValue => sizeof(long);

        protected override IEnumerable<byte> GetBytesFromTag()
        {
            var longs = Tag.Tags.Cast<NbtLong>().Select(x => x.Value);
            return DataUtils.ToByteArray(longs.ToArray());
        }

        protected override ICommand SetBytesToTag(List<byte> bytes)
        {
            var longs = DataUtils.ToLongArray(bytes.ToArray());
            return new MergedCommand($"Replace long tags of {CommandExtensions.Describe(Tag)}",
                new ClearCommand(Tag),
                new AddRangeCommand(Tag, longs.Select(x => new NbtLong(x)))
            );
        }
    }
}

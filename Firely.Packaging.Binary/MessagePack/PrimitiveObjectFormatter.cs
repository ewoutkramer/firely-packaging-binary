// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Buffers;
using MessagePack;
using System.Collections;
using P = Hl7.Fhir.ElementModel.Types;
using System.Globalization;
using System.Text;

namespace Firely.Packaging.Binary.MessagePack
{
    public static class PrimitiveObjectFormatterCore
    {
        public static byte[] SerializeToBytes(object value) => SerializeToMemory(value).ToArray();

        public static ReadOnlyMemory<byte> SerializeToMemory(object value)
        {
            ArrayBufferWriter<byte> serialized = new ArrayBufferWriter<byte>();
            MessagePackWriter writer = new MessagePackWriter(serialized);
            SerializeToWriter(ref writer, value);
            writer.Flush();
            return serialized.WrittenMemory;
        }

        internal static void SerializeToWriter(ref MessagePackWriter writer, object value)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            switch (value)
            {
                case bool b:
                    writer.Write(b);
                    return;
                case sbyte sb:
                    writer.WriteInt8(sb);
                    return;
                case byte by:
                    writer.WriteUInt8(by);
                    return;
                case short i16:
                    writer.WriteInt16(i16);
                    return;
                case ushort ui16:
                    writer.WriteUInt16(ui16);
                    return;
                case int i32:
                    writer.WriteInt32(i32);
                    return;
                case uint ui32:
                    writer.WriteUInt32(ui32);
                    return;
                case long i64:
                    writer.WriteInt64(i64);
                    return;
                case ulong ui64:
                    writer.WriteUInt64(ui64);
                    return;
                case float sg:
                    writer.Write(sg);
                    return;
                case double dbl:
                    writer.Write(dbl);
                    return;
                case DateTime dt:
                    writer.Write(dt);
                    return;
                case string s:
                    writer.Write(s);
                    return;
                case byte[] ba:
                    writer.Write(ba);
                    return;
                case IDictionary<string, object> dict:
                    writer.WriteMapHeader(dict.Count);
                    foreach (var item in dict)
                    {
                        SerializeToWriter(ref writer, item.Key);
                        SerializeToWriter(ref writer, item.Value);
                    }
                    return;
                case ICollection coll:
                    writer.WriteArrayHeader(coll.Count);
                    foreach (var item in coll)
                    {
                        SerializeToWriter(ref writer, item);
                    }
                    return;
                case decimal dec:
                    writer.WriteExtensionFormat(createDecimalExt(dec));
                    return;
                case P.Date pd:
                    writer.WriteExtensionFormat(createSystemDateExt(pd));
                    return;
                case P.DateTime pdt:
                    writer.WriteExtensionFormat(createSystemDateTimeExt(pdt));
                    return;
                case P.Time pt:
                    writer.WriteExtensionFormat(createSystemTimeExt(pt));
                    return;
                default:
                    throw new NotSupportedException($"Serialization of type '{value.GetType()}' is not supported in MessagePack serialization.");
            }
        }

        private static ExtensionResult createDecimalExt(decimal dec)
        {
            var serialized = dec.ToString(CultureInfo.InvariantCulture);
            var bytes = Encoding.UTF8.GetBytes(serialized);
            return new ExtensionResult(EXT_TYPE_DECIMAL, bytes.AsMemory());
        }

        private static ExtensionResult createSystemDateExt(P.Date date)
        {
            var serialized = date.ToString();
            var bytes = Encoding.UTF8.GetBytes(serialized);
            return new ExtensionResult(EXT_TYPE_DATE, bytes.AsMemory());
        }

        private static ExtensionResult createSystemDateTimeExt(P.DateTime dateTime)
        {
            var serialized = dateTime.ToString();
            var bytes = Encoding.UTF8.GetBytes(serialized);
            return new ExtensionResult(EXT_TYPE_DATETIME, bytes.AsMemory());
        }

        private static ExtensionResult createSystemTimeExt(P.Time time)
        {
            var serialized = time.ToString();
            var bytes = Encoding.UTF8.GetBytes(serialized);
            return new ExtensionResult(EXT_TYPE_TIME, bytes.AsMemory());
        }

        public const sbyte EXT_TYPE_DECIMAL = 1;
        public const sbyte EXT_TYPE_DATE = 2;
        public const sbyte EXT_TYPE_DATETIME = 3;
        public const sbyte EXT_TYPE_TIME = 4;

        public static object Deserialize(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            MessagePackType type = reader.NextMessagePackType;

            switch (type)
            {
                case MessagePackType.Integer:
                    var code = reader.NextCode;
                    if (code >= MessagePackCode.MinNegativeFixInt && code <= MessagePackCode.MaxNegativeFixInt)
                    {
                        return reader.ReadSByte();
                    }
                    else if (code >= MessagePackCode.MinFixInt && code <= MessagePackCode.MaxFixInt)
                    {
                        return reader.ReadByte();
                    }
                    else if (code == MessagePackCode.Int8)
                    {
                        return reader.ReadSByte();
                    }
                    else if (code == MessagePackCode.Int16)
                    {
                        return reader.ReadInt16();
                    }
                    else if (code == MessagePackCode.Int32)
                    {
                        return reader.ReadInt32();
                    }
                    else if (code == MessagePackCode.Int64)
                    {
                        return reader.ReadInt64();
                    }
                    else if (code == MessagePackCode.UInt8)
                    {
                        return reader.ReadByte();
                    }
                    else if (code == MessagePackCode.UInt16)
                    {
                        return reader.ReadUInt16();
                    }
                    else if (code == MessagePackCode.UInt32)
                    {
                        return reader.ReadUInt32();
                    }
                    else if (code == MessagePackCode.UInt64)
                    {
                        return reader.ReadUInt64();
                    }

                    throw new FormatException($"Encountered unrecognized integer pack code '{code}'.");
                case MessagePackType.Boolean:
                    return reader.ReadBoolean();
                case MessagePackType.Float:
                    return reader.NextCode == MessagePackCode.Float32 ? reader.ReadSingle() : (object)reader.ReadDouble();
                case MessagePackType.String:
                    return reader.ReadString();
                case MessagePackType.Binary:
                    // We must copy the sequence returned by ReadBytes since the reader's sequence is only valid during deserialization.
                    return reader.ReadBytes()?.ToArray();
                case MessagePackType.Extension:
                    ExtensionResult ext = reader.ReadExtensionFormat();
                    return ext.TypeCode switch
                    {
                        EXT_TYPE_DATE => parseSystemDateExt(ext),
                        EXT_TYPE_DATETIME => parseSystemDateTimeExt(ext),
                        EXT_TYPE_TIME => parseSystemTimeExt(ext),
                        EXT_TYPE_DECIMAL => parseDecimalExt(ext),
                        _ => throw new FormatException($"Encountered unrecognized extension pack code '{ext.TypeCode}'.")
                    };
                case MessagePackType.Array:
                    return deserializeArray(ref reader);
                case MessagePackType.Map:
                    return deserializeMap(ref reader);
                case MessagePackType.Nil:
                    reader.ReadNil();
                    return null;
                default:
                    throw new FormatException($"Encountered unrecognized pack type '{type}'.");
            }
        }

        private static decimal parseDecimalExt(ExtensionResult ext)
        {
            var serialized = Encoding.UTF8.GetString(ext.Data.ToArray());
            return decimal.Parse(serialized, CultureInfo.InvariantCulture);
        }

        private static P.Time parseSystemTimeExt(ExtensionResult ext)
        {
            var serialized = Encoding.UTF8.GetString(ext.Data.ToArray());
            return P.Time.Parse(serialized);
        }

        private static P.DateTime parseSystemDateTimeExt(ExtensionResult ext)
        {
            var serialized = Encoding.UTF8.GetString(ext.Data.ToArray());
            return P.DateTime.Parse(serialized);
        }

        private static P.Date parseSystemDateExt(ExtensionResult ext)
        {
            var serialized = Encoding.UTF8.GetString(ext.Data.ToArray());
            return P.Date.Parse(serialized);
        }

        private static object deserializeMap(ref MessagePackReader reader)
        {
            int length = reader.ReadMapHeader();
            IDictionary<string, object> dictionary = new ExpandoObject();
            for (int i = 0; i < length; i++)
            {
                var key = Deserialize(ref reader);
                if (key is string keystr)
                {
                    var value = Deserialize(ref reader);
                    dictionary.Add(keystr, value);
                }
                else
                    throw new InvalidOperationException($"Invalid key type '{key.GetType()}'");
            }

            return dictionary;
        }

        private static object deserializeArray(ref MessagePackReader reader)
        {
            var length = reader.ReadArrayHeader();
            if (length == 0)
            {
                return Array.Empty<object>();
            }

            var array = new object[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = Deserialize(ref reader);
            }

            return array;
        }
    }
}

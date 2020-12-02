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

        public static object Deserialize(byte[] data) => Deserialize(new ReadOnlyMemory<byte>(data));

        public static object Deserialize(ReadOnlyMemory<byte> data)
        {
            var reader = new MessagePackReader(data);
            return Deserialize(ref reader);
        }

        internal static object Deserialize(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            MessagePackType type = reader.NextMessagePackType;

            return type switch
            {
                MessagePackType.Integer =>
                    reader.NextCode switch
                    {
                        >= MessagePackCode.MinNegativeFixInt and <= MessagePackCode.MaxNegativeFixInt => reader.ReadSByte(),
                        >= MessagePackCode.MinFixInt and <= MessagePackCode.MaxFixInt => reader.ReadByte(),
                        MessagePackCode.Int8 => reader.ReadSByte(),
                        MessagePackCode.Int16 => reader.ReadInt16(),
                        MessagePackCode.Int32 => reader.ReadInt32(),
                        MessagePackCode.Int64 => reader.ReadInt64(),
                        MessagePackCode.UInt8 => reader.ReadByte(),
                        MessagePackCode.UInt16 => reader.ReadUInt16(),
                        MessagePackCode.UInt32 => reader.ReadUInt32(),
                        MessagePackCode.UInt64 => reader.ReadUInt64(),
                        var unrecognized => throw new FormatException($"Encountered unrecognized integer pack code '{unrecognized}'.")
                    },
                MessagePackType.Boolean => reader.ReadBoolean(),
                MessagePackType.Float => reader.NextCode == MessagePackCode.Float32 ?
                        reader.ReadSingle() : (object)reader.ReadDouble(),
                MessagePackType.String => reader.ReadString(),
                MessagePackType.Binary => reader.ReadBytes()?.ToArray(),
                MessagePackType.Extension => readExtension(ref reader),
                MessagePackType.Array => deserializeArray(ref reader),
                MessagePackType.Map => deserializeMap(ref reader),
                MessagePackType.Nil => readNill(ref reader),
                var unrecognized => throw new FormatException($"Encountered unrecognized pack type '{unrecognized}'.")
            };

            static object readExtension(ref MessagePackReader reader)
            {
                ExtensionResult ext = reader.ReadExtensionFormat();
                return ext.TypeCode switch
                {
                    EXT_TYPE_DATE => parseSystemDateExt(ext),
                    EXT_TYPE_DATETIME => parseSystemDateTimeExt(ext),
                    EXT_TYPE_TIME => parseSystemTimeExt(ext),
                    EXT_TYPE_DECIMAL => parseDecimalExt(ext),
                    var unrecognized => throw new FormatException($"Encountered unrecognized extension pack code '{unrecognized}'.")
                };
            }

            static object readNill(ref MessagePackReader reader)
            {
                reader.ReadNil();
                return null;
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
            //IDictionary<string, object> dictionary = new Dictionary<string,object>();
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

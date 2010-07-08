using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind
{
	public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
	{
		public XmlSchema GetSchema()
		{
			return null;
		}

		public void ReadXml(XmlReader reader)
		{
			var serializer = new XmlSerializer(typeof(KeyValue));
			reader.Read();
			while (reader.NodeType != System.Xml.XmlNodeType.EndElement)
			{
				var kv = serializer.Deserialize(reader) as KeyValue;
				if (kv != null) Add(kv.Key, kv.Value);
			}
			reader.Read();
		}

		public void WriteXml(XmlWriter writer)
		{
			var serializer = new XmlSerializer(typeof(KeyValue));
			foreach (var key in Keys)
			{
				serializer.Serialize(writer, new KeyValue(key, this[key]));
			}
		}

		public class KeyValue
		{
			[XmlAttribute("key")]
			public TKey Key { get; set; }
			[XmlText]
			public TValue Value { get; set; }
			public KeyValue() { }
			public KeyValue(TKey key, TValue value) { Key = key; Value = value; }
		}
	}
}

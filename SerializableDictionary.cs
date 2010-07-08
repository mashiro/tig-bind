using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind
{
	/// <summary>
	/// SerializableDictionary の InnerClass にすると Mono でこける
	/// </summary>
	public class SerializableKeyValuePair<TKey, TValue>
	{
		[XmlAttribute("key")]
		public TKey Key { get; set; }
		[XmlText]
		public TValue Value { get; set; }
		public SerializableKeyValuePair() { }
		public SerializableKeyValuePair(TKey key, TValue value) { Key = key; Value = value; }
	}

	/// <summary>
	/// Serialize 可能な Dictionary
	/// </summary>
	/// <seealso cref="http://d.hatena.ne.jp/lord_hollow/20090602/p1"/>
	public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
	{
		private static XmlSerializer _serializer = new XmlSerializer(typeof(SerializableKeyValuePair<TKey, TValue>));

		public XmlSchema GetSchema()
		{
			return null;
		}

		public void ReadXml(XmlReader reader)
		{
			reader.Read();
			while (reader.NodeType != System.Xml.XmlNodeType.EndElement)
			{
				var kv = _serializer.Deserialize(reader) as SerializableKeyValuePair<TKey, TValue>;
				if (kv != null)
				{
					Add(kv.Key, kv.Value);
				}
			}
			reader.Read();
		}

		public void WriteXml(XmlWriter writer)
		{
			foreach (var kv in this)
			{
				_serializer.Serialize(writer, new SerializableKeyValuePair<TKey, TValue>(kv.Key, kv.Value));
			}
		}
	}
}
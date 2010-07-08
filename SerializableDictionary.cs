using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind
{
	/// <summary>
	/// シリアライズ可能な Dictionary
	/// </summary>
	/// <seealso cref="http://d.hatena.ne.jp/lord_hollow/20090602/p1"/>
	public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
	{
		private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(KeyValuePair));

		public System.Xml.Schema.XmlSchema GetSchema()
		{
			return null;
		}

		public void ReadXml(System.Xml.XmlReader reader)
		{
			reader.Read();
			while (reader.NodeType != System.Xml.XmlNodeType.EndElement)
			{
				var kv = _serializer.Deserialize(reader) as KeyValuePair;
				if (kv != null) Add(kv.Key, kv.Value);
			}
			reader.Read();
		}

		public void WriteXml(System.Xml.XmlWriter writer)
		{
			foreach (var key in Keys)
			{
				_serializer.Serialize(writer, new KeyValuePair(key, this[key]));
			}
		}

		public class KeyValuePair
		{
			[XmlAttribute("key")]
			public TKey Key { get; set; }
			[XmlText()]
			public TValue Value { get; set; }
			public KeyValuePair() { }
			public KeyValuePair(TKey key, TValue value) { Key = key; Value = value; }
		}
	}
}
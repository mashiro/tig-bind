using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace Spica.Xml.Feed
{
	public class InvalidFeedException : Exception
	{
		public InvalidFeedException() : base("指定されたフィードは有効ではありません。") { }
		public InvalidFeedException(string message) : base(message) { }
		public InvalidFeedException(string message, Exception inner) : base(message, inner) { }
	}

	public static class FeedDocument
	{
		private static XmlSerializerFactory _serializerFactory = new XmlSerializerFactory();
		private static List<Type> _serializableDocuments = new List<Type>();

		static FeedDocument()
		{
			_serializableDocuments.Add(typeof(Atom10Document));
			_serializableDocuments.Add(typeof(Rss20Document));
			_serializableDocuments.Add(typeof(Rss10Document));
		}

		public static IFeedDocument Load(Stream stream)
		{
			return Load(XmlReader.Create(stream));
		}

		public static IFeedDocument Load(TextReader reader)
		{
			return Load(XmlReader.Create(reader));
		}

		public static IFeedDocument Load(XmlReader reader)
		{
			foreach (Type documentType in _serializableDocuments)
			{
				XmlSerializer serializer = null;
				lock (_serializerFactory) { serializer = _serializerFactory.CreateSerializer(documentType); }
				if (serializer.CanDeserialize(reader))
				{
					return serializer.Deserialize(reader) as IFeedDocument;
				}
			}

			throw new InvalidFeedException();
		}
	}
}
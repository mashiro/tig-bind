using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
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

		public static IFeedDocument Load(string address, ICredentials credentials)
		{
			return Load(new Uri(address), credentials);
		}

		public static IFeedDocument Load(Uri address, ICredentials credentials)
		{
			using (WebClient webClient = CreateWebClient(credentials))
			{
				using (Stream stream = webClient.OpenRead(address))
				{
					return Load(stream);
				}
			}
		}

		public static IFeedDocument Load(Stream stream)
		{
			return Load(XmlReader.Create(stream));
		}

		public static IFeedDocument Load(XmlReader reader)
		{
			foreach(Type documentType in _serializableDocuments)
			{
				XmlSerializer serializer = _serializerFactory.CreateSerializer(documentType);
				if (serializer.CanDeserialize(reader))
				{
					return serializer.Deserialize(reader) as IFeedDocument;
				}
			}

			throw new InvalidFeedException();
		}

		public static WebClient CreateWebClient(ICredentials credentials)
		{
			WebClient webClient = new WebClient();
			webClient.Headers["Accept"] = "application/xml, text/xml";
			webClient.Headers["User-Agent"] = "Mozilla/4.0 (compatible; MSIE 6.0; Windows XP)";
			webClient.Encoding = Encoding.UTF8;

			if (credentials != null)
				webClient.Credentials = credentials;

			return webClient;
		}
	}
}

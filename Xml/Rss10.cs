using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Spica.Xml.Feed
{
	public static class Rss10Namespaces
	{
		public const String RDF = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
		public const String Default = "http://purl.org/rss/1.0/";
		public const String Content = "http://purl.org/rss/1.0/modules/content/";
		public const String Taxo = "http://purl.org/rss/1.0/modules/taxonomy/";
		public const String OpenSearch = "http://a9.com/-/spec/opensearchrss/1.0/";
		public const String DC = "http://purl.org/dc/elements/1.1/";
		public const String Hatena = "http://www.hatena.ne.jp/info/xmlns#";
		public const String Media = "http://search.yahoo.com/mrss";
	}

	[XmlRoot("RDF", Namespace = Rss10Namespaces.RDF)]
	public class Rss10Document : IFeedDocument
	{
		[XmlElement("channel", Namespace = Rss10Namespaces.Default)]
		public Rss10Channel _channel;

		[XmlIgnore()]
		public Uri Link { get { return _channel.Link; } }

		[XmlIgnore()]
		public string Title { get { return _channel.Title; } }

		[XmlIgnore()]
		public string Description { get { return _channel.Description; } }


		[XmlElement("item", Namespace = Rss10Namespaces.Default)]
		public List<Rss10Item> _items;
		[XmlIgnore()]

		public List<IFeedItem> Items { get { return new List<IFeedItem>(_items.ToArray()); } }


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}

	public class Rss10Channel
	{
		[XmlElement("link")]
		public String _link;

		[XmlIgnore()]
		public Uri Link { get { return FeedUtility.ParseUri(_link); } }


		[XmlElement("title")]
		public String _title;

		[XmlIgnore()]
		public String Title { get { return _title; } }


		[XmlElement("description")]
		public String _description;

		[XmlIgnore()]
		public String Description { get { return _description; } }


		[XmlElement("date", Namespace = Rss20Namespaces.DC)]
		public String _dc_date;

		[XmlIgnore()]
		public DateTime PublishDate { get { return FeedUtility.ParseDateTime(_dc_date); } }


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}

	public class Rss10Item : IFeedItem
	{
		[XmlElement("creator", Namespace = Rss20Namespaces.DC)]
		public String _dc_creator;

		[XmlIgnore()]
		public string Author { get { return _dc_creator; } }


		[XmlElement("link")]
		public String _link;

		[XmlIgnore()]
		public Uri Link { get { return FeedUtility.ParseUri(_link); } }


		[XmlElement("title")]
		public String _title;

		[XmlIgnore()]
		public string Title { get { return _title; } }


		[XmlElement("description")]
		public String _description;

		[XmlElement("encoded", Namespace = Rss20Namespaces.Content)]
		public String _content_encoded;

		[XmlIgnore()]
		public String Description { get { return _description ?? _content_encoded; } }


		[XmlElement("date", Namespace = Rss20Namespaces.DC)]
		public String _dc_date;		

		[XmlIgnore()]
		public DateTime PublishDate { get { return FeedUtility.ParseDateTime(_dc_date); } }


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}
}

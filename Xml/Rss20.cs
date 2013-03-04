using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Spica.Xml.Feed
{
	public static class Rss20Namespaces
	{
		public const String Content = "http://purl.org/rss/1.0/modules/content/";
		public const String DC = "http://purl.org/dc/elements/1.1/";
	}

	[XmlRoot("rss")]
	public class Rss20Document : IFeedDocument
	{
		[XmlElement("channel")]
		public Rss20Channel _channel;

		[XmlIgnore()]
		public String Title { get { return _channel._title; } }

		[XmlIgnore()]
		public String Description { get { return _channel._description; } }

		[XmlIgnore()]
		public Uri Link { get { return _channel.Link; } }

		[XmlIgnore()]
		public List<IFeedItem> Items { get { return new List<IFeedItem>(_channel._items.ToArray()); } }


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}

	public class Rss20Channel
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


		[XmlElement("item")]
		public List<Rss20Item> _items;


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}

	public class Rss20Item : IFeedItem
	{
		[XmlElement("author")]
		public String _author;

		[XmlElement("creator", Namespace = Rss20Namespaces.DC)]
		public String _dc_creator;

		[XmlIgnore()]
		public String Author { get { return _author ?? _dc_creator; } }


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

		[XmlElement("encoded", Namespace = Rss20Namespaces.Content)]
		public String _content_encoded;

		[XmlIgnore()]
		public String Description { get { return _description ?? _content_encoded; } }


		[XmlElement("pubDate")]
		public String _pubDate;

		[XmlElement("date", Namespace = Rss20Namespaces.DC)]
		public String _dc_date;

		[XmlIgnore()]
		public DateTime PublishDate { get { return FeedUtility.ParseDateTime(_pubDate ?? _dc_date); } }


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}
}

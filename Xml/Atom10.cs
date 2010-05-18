using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Spica.Xml.Feed
{
	public static class Atom10Namespaces
	{
		public const String Default = "http://www.w3.org/2005/Atom";
	}

	[XmlRoot("feed", Namespace = Atom10Namespaces.Default)]
	public class Atom10Document : IFeedDocument
	{
		[XmlElement("link")]
		public List<Atom10Link> _link;

		[XmlIgnore()]
		public Uri Link { get { return _link.Count > 0 ? _link[0].HRef : null; } }


		[XmlElement("title")]
		public String _title;

		[XmlIgnore()]
		public String Title { get { return _title; } }


		[XmlElement("subtitle")]
		public String _subtitle;

		[XmlIgnore()]
		public String Description { get { return _subtitle; } }


		[XmlElement("entry")]
		public List<Atom10Item> _items;

		[XmlIgnore()]
		public List<IFeedItem> Items { get { return new List<IFeedItem>(_items.ToArray()); } }


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}

	public class Atom10Item : IFeedItem
	{
		[XmlElement("author")]
		public Atom10Person _author;

		[XmlIgnore()]
		public String Author { get { return _author.Name; } }


		[XmlElement("link")]
		public List<Atom10Link> _link;

		[XmlIgnore()]
		public Uri Link { get { return _link.Count > 0 ? _link[0].HRef : null; } }


		[XmlElement("title")]
		public String _title;

		[XmlIgnore()]
		public String Title { get { return _title; } }


		[XmlElement("summary")]
		public String _summary;

		[XmlElement("content")]
		public String _content;

		[XmlIgnore()]
		public String Description { get { return _summary ?? _content; } }


		[XmlElement("published")]
		public String _published;

		[XmlIgnore()]
		public DateTime PublishDate { get { return DateTime.Parse(_published); } }


		public override string ToString()
		{
			return Title ?? base.ToString();
		}
	}

	public class Atom10Person
	{
		[XmlElement("name")]
		public String _name;

		[XmlIgnore()]
		public String Name { get { return _name; } }


		[XmlElement("url")]
		public String _uri;

		[XmlIgnore()]
		public Uri Uri { get { return FeedUtility.ParseUri(_uri); } }


		[XmlElement("email")]
		public String _email;

		[XmlIgnore()]
		public String Email { get { return _email; } }


		public override string ToString()
		{
			return Name ?? base.ToString();
		}
	}

	public class Atom10Link
	{
		[XmlAttribute("href")]
		public String _href;

		[XmlIgnore()]
		public Uri HRef { get { return FeedUtility.ParseUri(_href); } }


		[XmlAttribute("rel")]
		public String _rel;

		[XmlIgnore()]
		public String Rel { get { return _rel; } }


		[XmlAttribute("type")]
		public String _type;

		[XmlIgnore()]
		public String Type { get { return _type; } }


		[XmlAttribute("hreflang")]
		public String _hreflang;

		[XmlIgnore()]
		public String HrefLang { get { return _hreflang; } }


		[XmlAttribute("title")]
		public String _title;

		[XmlIgnore()]
		public String Title { get { return _title; } }


		[XmlAttribute("length")]
		public String _length;

		[XmlIgnore()]
		public String Length { get { return _length; } }


		public override string ToString()
		{
			return _href ?? base.ToString();
		}
	}
}

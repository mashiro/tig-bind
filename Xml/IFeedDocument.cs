using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spica.Xml.Feed
{
	/// <summary>
	/// Feed Document Interface
	/// </summary>
	public interface IFeedDocument
	{
		Uri Link { get; }
		String Title { get; }
		String Description { get; }
		List<IFeedItem> Items { get; }
	}

	/// <summary>
	/// Feed Item Interface
	/// </summary>
	public interface IFeedItem
	{
		String Author { get; }
		Uri Link { get; }
		String Title { get; }
		String Description { get; }
		DateTime PublishDate { get; }
	}
}
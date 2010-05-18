using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Net;

namespace Spica.Xml.Feed
{
	public class FeedUtility
	{
		public static DateTime ParseDateTime(String dateTimeString)
		{
			DateTime dateTime;
			if (!DateTime.TryParse(dateTimeString, out dateTime))
				dateTime = DateTime.Now;

			return dateTime;
		}

		public static Uri ParseUri(String uriString)
		{
			if (String.IsNullOrEmpty(uriString))
				return null;

			return new Uri(uriString);
		}
	}
}

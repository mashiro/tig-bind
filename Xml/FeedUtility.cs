using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.Globalization;

namespace Spica.Xml.Feed
{
	public class FeedUtility
	{
		public static DateTime ParseDateTime(String dateTimeString)
		{
			DateTime dateTime;
			if (!DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
				dateTime = DateTime.Now;

			return dateTime;
		}

		public static Uri ParseUri(String uriString)
		{
			Uri uri;
			if (!Uri.TryCreate(uriString, UriKind.Absolute, out uri))
				return null;

			return uri;
		}
	}
}

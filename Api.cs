using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.IO;

namespace Spica.Applications.TwitterIrcGateway.AddIns.OtherSource
{
	public abstract class ApiBase
	{
		private static XmlSerializerFactory _serializerFactory = new XmlSerializerFactory();
		public String Username { get; set; }
		public String Password { get; set; }

		public ApiBase()
		{
		}

		public ApiBase(String username, String password)
			: this()
		{
			Username = username;
			Password = password;
		}

		protected String Get(String url, NameValueCollection options)
		{
			WebClient webClient = CreateWebClient();
			return webClient.DownloadString(String.Format("{0}?{1}", url, BuildQueryString(options)));
		}

		protected String Post(String url, NameValueCollection options)
		{
			WebClient webClient = CreateWebClient();
			return webClient.UploadString(url, BuildQueryString(options));
		}

		protected WebClient CreateWebClient()
		{
			WebClient webClient = new WebClient();
			webClient.Credentials = new NetworkCredential(Username, Password);
			webClient.Encoding = Encoding.UTF8;
			webClient.Headers[HttpRequestHeader.UserAgent] = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)";
			webClient.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
			webClient.Headers[HttpRequestHeader.Accept] = "text/xml, application/xml";
			return webClient;
		}

		protected String BuildQueryString(NameValueCollection options)
		{
			StringBuilder sb = new StringBuilder();
			foreach (String key in options.AllKeys)
			{
				if (sb.Length != 0)
					sb.Append("&");

				String value = options[key];
				if (String.IsNullOrEmpty(value))
					sb.Append(key);
				else
					sb.AppendFormat("{0}={1}", key, value);
			}

			return sb.ToString();
		}

		protected static T Deserialize<T>(String data)
			where T : class
		{
			return Deserialize<T>(new StringReader(data));
		}

		protected static T Deserialize<T>(TextReader reader)
			where T : class
		{
			XmlSerializer xmlSerializer = null;
			lock (_serializerFactory) { xmlSerializer = _serializerFactory.CreateSerializer(typeof(T)); }
			return xmlSerializer.Deserialize(reader) as T;
		}
	}
}

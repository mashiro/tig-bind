using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.IO;
using System.IO.Compression;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind
{
	/// <summary>
	/// BASIC 認証でいろいろするための簡単な枠組み
	/// </summary>
	public abstract class ApiBase
	{
		private static XmlSerializerFactory _serializerFactory = new XmlSerializerFactory();
		public String Username { get; set; }
		public String Password { get; set; }

		protected String Get(String url, NameValueCollection options)
		{
			if (options != null && options.Count > 0)
				url = String.Format("{0}?{1}", url, BuildQueryString(options));
			HttpWebRequest webRequest = CreateHttpWebRequest(url, "GET");
			HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse;
			using (StreamReader sr = new StreamReader(GetResponseStream(webResponse)))
			{
				return sr.ReadToEnd();
			}
		}

		protected String Post(String url, NameValueCollection options)
		{
			HttpWebRequest webRequest = CreateHttpWebRequest(url, "POST");
			using (Stream stream = webRequest.GetRequestStream())
			{
				Byte[] postData = Encoding.UTF8.GetBytes(BuildQueryString(options));
				stream.Write(postData, 0, postData.Length);
			}
			HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse;
			using (StreamReader sr = new StreamReader(GetResponseStream(webResponse)))
			{
				return sr.ReadToEnd();
			}
		}

		protected String BuildQueryString(NameValueCollection options)
		{
			if (options == null)
				return String.Empty;

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

		private HttpWebRequest CreateHttpWebRequest(String url, String method)
		{
			var webRequest = HttpWebRequest.Create(url) as HttpWebRequest;
			webRequest.Credentials = new NetworkCredential(Username, Password);
			webRequest.Method = method;
			webRequest.UserAgent = String.Format("{0}/{1}", GetType().Assembly.GetName().Name, GetType().Assembly.GetName().Version);
			webRequest.ContentType = "application/x-www-form-urlencoded";
			webRequest.Accept = "text/xml, application/xml";
			webRequest.Headers["Accept-Encoding"] = "gzip";
			return webRequest;
		}

		private Stream GetResponseStream(WebResponse webResponse)
		{
			var httpWebResponse = webResponse as HttpWebResponse;
			if (httpWebResponse == null)
				return webResponse.GetResponseStream();
			if (String.Compare(httpWebResponse.ContentEncoding, "gzip", true) == 0)
				return new GZipStream(webResponse.GetResponseStream(), CompressionMode.Decompress);
			return webResponse.GetResponseStream();
		}

		#region Deserialize
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
		#endregion
	}
}

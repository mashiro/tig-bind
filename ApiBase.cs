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
		public Encoding Encoding { get; set; }

		public ApiBase()
		{
			Username = String.Empty;
			Password = String.Empty;
			Encoding = Encoding.UTF8;
		}

		public String Get(String url, NameValueCollection options)
		{
			if (options != null && options.Count > 0)
				url += "?" + BuildQueryString(options);

			HttpWebRequest webRequest = CreateHttpWebRequest(url, "GET");
			HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse;
			using (StreamReader streamReader = new StreamReader(GetResponseStream(webResponse), Encoding))
			{
				return streamReader.ReadToEnd();
			}
		}

		public String Post(String url, NameValueCollection options)
		{
			HttpWebRequest webRequest = CreateHttpWebRequest(url, "POST");
#if true
			using (Stream stream = webRequest.GetRequestStream())
			{
				Byte[] postData = Encoding.GetBytes(BuildQueryString(options));
				stream.Write(postData, 0, postData.Length);
			}
#else
			using (StreamWriter streamWriter = new StreamWriter(webRequest.GetRequestStream(), Encoding))
			{
				String postData = BuildQueryString(options);
				streamWriter.Write(postData);
			}
#endif
			HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse;
			using (StreamReader streamReader = new StreamReader(GetResponseStream(webResponse), Encoding))
			{
				return streamReader.ReadToEnd();
			}
		}

		protected String BuildQueryString(NameValueCollection options)
		{
			if (options == null || options.Count == 0)
				return String.Empty;

			return String.Join("&", options.AllKeys.Select(key =>
			{
				String value = options[key];
				if (String.IsNullOrEmpty(key))
					return value;
				else
					return String.Format("{0}={1}", key, value);
			}).ToArray());
		}

		private HttpWebRequest CreateHttpWebRequest(String url, String method)
		{
			var asmName = GetType().Assembly.GetName();
			var webRequest = HttpWebRequest.Create(url) as HttpWebRequest;
			webRequest.Method = method;
			webRequest.UserAgent = String.Format("{0}/{1}", asmName.Name, asmName.Version);
			webRequest.ContentType = "application/x-www-form-urlencoded";
			webRequest.Accept = "text/xml, application/xml";
			webRequest.Headers["Accept-Encoding"] = "gzip, deflate";
			if (!String.IsNullOrEmpty(Username) && !String.IsNullOrEmpty(Password))
				webRequest.Credentials = new NetworkCredential(Username, Password);
			return webRequest;
		}

		private Stream GetResponseStream(WebResponse webResponse)
		{
			var httpWebResponse = webResponse as HttpWebResponse;
			if (httpWebResponse == null)
				return webResponse.GetResponseStream();
			if (String.Compare(httpWebResponse.ContentEncoding, "gzip", true) == 0)
				return new GZipStream(webResponse.GetResponseStream(), CompressionMode.Decompress);
			else if (String.Compare(httpWebResponse.ContentEncoding, "deflate", true) == 0)
				return new DeflateStream(webResponse.GetResponseStream(), CompressionMode.Decompress);
			return webResponse.GetResponseStream();
		}

		#region Deserialize
		protected static T Deserialize<T>(String data)
			where T : class
		{
			using (StringReader stringReader = new StringReader(data))
			{
				return Deserialize<T>(stringReader);
			}
		}

		protected static T Deserialize<T>(TextReader reader)
			where T : class
		{
			XmlSerializer xmlSerializer = CreateSerializer<T>();
			return xmlSerializer.Deserialize(reader) as T;
		}

		private static XmlSerializer CreateSerializer<T>()
		{
			lock (_serializerFactory)
			{
				return _serializerFactory.CreateSerializer(typeof(T));
			}
		}
		#endregion
	}

	public class SimpleApi : ApiBase
	{
	}
}

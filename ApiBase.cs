using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Globalization;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind
{
	public abstract class ApiBase
	{
		public enum Method { Get, Post };
		private const Int32 DefaultTimeout = 100 * 1000;

		private static XmlSerializerFactory _serializerFactory = new XmlSerializerFactory();
		public Encoding Encoding { get; set; }
		public Boolean EnableCompression { get; set; }

		public ApiBase()
		{
			Encoding = Encoding.UTF8;
			EnableCompression = false;
		}

		public String Get(String url, NameValueCollection options = null, Int32 timeout = DefaultTimeout)
		{
			using (var stream = Open(url, Method.Get, options, timeout))
			using (var streamReader = new StreamReader(stream, Encoding))
			{
				return streamReader.ReadToEnd();
			}
		}

		public String Post(String url, NameValueCollection options = null, Int32 timeout = DefaultTimeout)
		{
			using (var stream = Open(url, Method.Post, options, timeout))
			using (var streamReader = new StreamReader(stream, Encoding))
			{
				return streamReader.ReadToEnd();
			}
		}

		public Stream Open(String url, Method method, NameValueCollection options = null, Int32 timeout = DefaultTimeout)
		{
			var query = BindUtility.BuildQueryString(options, Encoding);
			if (method == Method.Get && !String.IsNullOrEmpty(query))
				url += "?" + query;

			var webRequest = GetHttpWebRequest(url, method.ToString().ToUpper());
			webRequest.Timeout = timeout;

			if (method == Method.Post)
			{
				var postData = Encoding.GetBytes(query);
				webRequest.ContentLength = postData.Length;
				using (var stream = webRequest.GetRequestStream())
				{
					stream.Write(postData, 0, postData.Length);
				}
			}

			var webResponse = GetHttpWebResponse(webRequest);
			return GetResponseStream(webResponse);
		}

		protected virtual void OnGetHttpWebRequest(HttpWebRequest request) { }
		protected virtual void OnGetHttpWebResponse(HttpWebResponse response) { }

		#region Internal
		private HttpWebRequest GetHttpWebRequest(String url, String method)
		{
			var asmName = GetType().Assembly.GetName();
			var webRequest = HttpWebRequest.Create(url) as HttpWebRequest;
			webRequest.Method = method;
			webRequest.UserAgent = String.Format("{0}/{1}", asmName.Name, asmName.Version);
			webRequest.Accept = "text/html, text/xml, application/xml";
			webRequest.ContentType = "application/x-www-form-urlencoded";
			if (EnableCompression)
				webRequest.Headers["Accept-Encoding"] = "gzip, deflate";

			OnGetHttpWebRequest(webRequest);
			return webRequest;
		}

		private HttpWebResponse GetHttpWebResponse(HttpWebRequest request)
		{
			var webResponse = request.GetResponse() as HttpWebResponse;

			OnGetHttpWebResponse(webResponse);
			return webResponse;
		}

		private Stream GetResponseStream(WebResponse webResponse)
		{
			var httpWebResponse = webResponse as HttpWebResponse;
			if (httpWebResponse == null)
				return webResponse.GetResponseStream();
			if (EnableCompression)
			{
				if (String.Compare(httpWebResponse.ContentEncoding, "gzip", true) == 0)
					return new GZipStream(webResponse.GetResponseStream(), CompressionMode.Decompress);
				else if (String.Compare(httpWebResponse.ContentEncoding, "deflate", true) == 0)
					return new DeflateStream(webResponse.GetResponseStream(), CompressionMode.Decompress);
			}
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

		#endregion
	}

	public class SimpleApi : ApiBase
	{
	}

	public class BasicAuthApi : SimpleApi
	{
		public String Username { get; set; }
		public String Password { get; set; }

		public BasicAuthApi()
			: this(String.Empty, String.Empty)
		{
		}

		public BasicAuthApi(String username, String password)
		{
			Username = username;
			Password = password;
		}

		protected override void OnGetHttpWebRequest(HttpWebRequest request)
		{
			base.OnGetHttpWebRequest(request);
			if (!String.IsNullOrEmpty(Username) && !String.IsNullOrEmpty(Password))
				request.Credentials = new NetworkCredential(Username, Password);
		}
	}

	public class CookieAuthApi : SimpleApi
	{
		private String _cookie = String.Empty;

		protected override void OnGetHttpWebRequest(HttpWebRequest request)
		{
			base.OnGetHttpWebRequest(request);
			request.UserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)";
			if (!String.IsNullOrEmpty(_cookie))
				request.Headers[HttpRequestHeader.Cookie] = _cookie;
		}

		protected override void OnGetHttpWebResponse(HttpWebResponse response)
		{
			base.OnGetHttpWebResponse(response);
			var setCookie = response.Headers[HttpResponseHeader.SetCookie];
			if (!String.IsNullOrEmpty(setCookie))
			{
				// see: http://neue.cc/2009/12/17_230.html
				var cookies = Regex.Split(setCookie, "(?<!expires=.{3}),")
					.Select(s => s.Split(';').First().Split('='))
					.Select(xs => new { Name = xs.First(), Value = string.Join("=", xs.Skip(1).ToArray()) })
					.Select(a => a.Name + "=" + a.Value)
					.ToArray();
				_cookie = String.Join(";", cookies);
			}
		}
	}
}

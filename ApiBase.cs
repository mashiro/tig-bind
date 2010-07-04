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
		private static XmlSerializerFactory _serializerFactory = new XmlSerializerFactory();
		public Encoding Encoding { get; set; }
		public Boolean EnableCompression { get; set; }

		public ApiBase()
		{
			Encoding = Encoding.UTF8;
			EnableCompression = false;
		}

		public String Get(String url, NameValueCollection options)
		{
			if (options != null && options.Count > 0)
				url += "?" + BuildQueryString(options);

			var webRequest = GetHttpWebRequest(url, "GET");
			var webResponse = GetHttpWebResponse(webRequest);
			using (var streamReader = new StreamReader(GetResponseStream(webResponse), Encoding))
			{
				return streamReader.ReadToEnd();
			}
		}

		public String Post(String url, NameValueCollection options)
		{
			var webRequest = GetHttpWebRequest(url, "POST");
			var postData = Encoding.GetBytes(BuildQueryString(options));
			webRequest.ContentLength = postData.Length;
			using (Stream stream = webRequest.GetRequestStream())
			{
				stream.Write(postData, 0, postData.Length);
			}

			var webResponse = GetHttpWebResponse(webRequest);
			using (var streamReader = new StreamReader(GetResponseStream(webResponse), Encoding))
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
				var encodedKey = UrlEncode(key, Encoding);
				var encodedValue = UrlEncode(options[key], Encoding);
				if (String.IsNullOrEmpty(encodedKey))
					return encodedValue;
				else
					return String.Format("{0}={1}", encodedKey, encodedValue);
			}).ToArray());
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

		#region Utility
		/// <summary>
		/// Urlエンコードします
		/// </summary>
		/// <seealso cref="http://d.hatena.ne.jp/kazuv3/20080605/1212656674"/>
		public static string UrlEncode(string s, Encoding enc)
		{
			StringBuilder rt = new StringBuilder();
			foreach (byte i in enc.GetBytes(s))
				if (i == 0x20)
					rt.Append('+');
				else if (i >= 0x30 && i <= 0x39 || i >= 0x41 && i <= 0x5a || i >= 0x61 && i <= 0x7a)
					rt.Append((char)i);
				else
					rt.Append("%" + i.ToString("X2"));
			return rt.ToString();
		}

		/// <summary>
		/// Urlデコードします
		/// </summary>
		/// <seealso cref="http://d.hatena.ne.jp/kazuv3/20080605/1212656674"/>
		public static string UrlDecode(string s, Encoding enc)
		{
			List<byte> bytes = new List<byte>();
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if (c == '%')
					bytes.Add((byte)int.Parse(s[++i].ToString() + s[++i].ToString(), NumberStyles.HexNumber));
				else if (c == '+')
					bytes.Add((byte)0x20);
				else
					bytes.Add((byte)c);
			}
			return enc.GetString(bytes.ToArray(), 0, bytes.Count);
		}
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
		{
			Username = String.Empty;
			Password = String.Empty;
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
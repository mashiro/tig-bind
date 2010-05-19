using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.IO;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind
{
	/// <summary>
	/// BASIC 認証でいろいろするための簡単な枠組み
	/// </summary>
	public abstract class ApiBase : IDisposable
	{
		private static XmlSerializerFactory _serializerFactory = new XmlSerializerFactory();
		private WebClient _webClient = null;
		private String _username = String.Empty;
		private String _password = String.Empty;

		public WebClient WebClient { get { return _webClient; } set { _webClient = value; } }
		public String Username { get { return _username; } set { _username = value; UpdateCredential(); } }
		public String Password { get { return _password; } set { _password = value; UpdateCredential(); } }

		public ApiBase()
		{
			_webClient = new WebClient();
			_webClient.Encoding = Encoding.UTF8;
			_webClient.Headers[HttpRequestHeader.UserAgent] = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)";
			_webClient.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
			_webClient.Headers[HttpRequestHeader.Accept] = "text/xml, application/xml";
		}

		public ApiBase(String username, String password)
			: this()
		{
			_username = username;
			_password = password;
			UpdateCredential();
		}

		public void Dispose()
		{
			if (_webClient != null)
			{
				_webClient.Dispose();
				_webClient = null;
			}
		}

		private void UpdateCredential()
		{
			_webClient.Credentials = new NetworkCredential(Username, Password);
		}

		protected String Get(String url, NameValueCollection options)
		{
			return _webClient.DownloadString(String.Format("{0}?{1}", url, BuildQueryString(options)));
		}

		protected String Post(String url, NameValueCollection options)
		{
			return _webClient.UploadString(url, BuildQueryString(options));
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

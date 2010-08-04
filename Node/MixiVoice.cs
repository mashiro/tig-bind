using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind.Node
{
	[XmlType("MixiVoice")]
	public class BindMixiVoiceNode : BindTimerNodeBase
	{
		[Browsable(false)]
		public String Email { get; set; }

		[Browsable(false)]
		public String Password { get; set; }

		[Description("コンテンツが流れるチャンネル名を指定します")]
		public String ChannelName { get; set; }

		[Browsable(false)]
		public SerializableDictionary<String, String> Patterns { get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public MixiVoice.Api Api { get; set; }

		private Boolean _loggedIn = false;
		private Boolean _hasSiteInfo = false;
		private DateTime _since = DateTime.MinValue;

		public override String GetChannelName() { return ChannelName; }
		public override String GetNodeName() { return "MixiVoice"; }
		public override Type GetContextType() { return typeof(BindMixiVoiceEditContext); }

		public BindMixiVoiceNode()
		{
			Interval = 600;
			Email = String.Empty;
			Password = String.Empty;
			ChannelName = "#" + GetNodeName();
			Patterns = new SerializableDictionary<String, String>();
		}

		public override void Initialize(BindAddIn addIn)
		{
			base.Initialize(addIn);

			Api = CreateApi<MixiVoice.Api>();
			UpdateSiteInfo(false);
			Login();

			// SiteInfo更新前は必ず起動に失敗するので明示的に起動する。
			Update();
		}

		public void Login()
		{
			if (!_loggedIn && IsValid())
			{
				Api.Login(Email, BindUtility.Decrypt(Password));
				_loggedIn = true;
			}
		}

		public Boolean UpdateSiteInfo(Boolean force)
		{
			if (force) Patterns.Clear();
			_hasSiteInfo = Api.UpdateSiteInfo(Patterns);
			return _hasSiteInfo;
		}

		public void Reset()
		{
			_since = DateTime.MinValue;
		}

		public override string ToString()
		{
			return String.Format("{0} ({1})", Email, Interval);
		}

		protected override bool IsValid()
		{
			return base.IsValid()
				&& !String.IsNullOrEmpty(Email)
				&& !String.IsNullOrEmpty(Password)
				&& _hasSiteInfo;
		}

		/// <summary>
		/// メッセージ受信時の処理
		/// </summary>
		public override void OnMessageReceived(BindPrivMessageReceivedEventArgs e)
		{
			e.Cancel = true;

			try
			{
				if (IsValid())
				{
					Api.AddVoice(e.Message.Content);
				}
			}
			catch (Exception)
			{
				SendMessage(BindAddIn.DefaultSenderNick, "メッセージの送信に失敗しました。", true);
			}
		}

		/// <summary>
		/// タイマーのコールバック処理
		/// </summary>
		protected override void OnTimerCallback(Boolean isFirstTime)
		{
			try
			{
				var statuses = Api.GetRecentVoice()
					.Where(v => v.PostTime > _since)
					.OrderBy(v => v.PostTime)
					.ToList();
				if (statuses.Count > 0)
				{
					foreach (var status in statuses)
					{
						Send(status, isFirstTime);
					}

					_since = statuses.Last().PostTime;
				}
			}
			catch (Exception ex)
			{
				SendException(ex);
			}
		}

		private void Send(MixiVoice.Status status, Boolean isFirstTime)
		{
			var content = AddIn.ApplyDateTime(status.Text, status.PostTime, isFirstTime);
			var sender = status.Name;
			SendMessage(sender, content, isFirstTime);
			AddIn.SleepClientMessageWait();
		}
	}

	public class BindMixiVoiceEditContext : BindEditContextBase
	{
		public new BindMixiVoiceNode Node
		{
			get { return base.Node as BindMixiVoiceNode; }
			set { base.Node = value; }
		}

		[Description("ボイスの取得を試みます")]
		public void Test()
		{
			CreateGroup(Node.ChannelName);
			Node.Reset();
			Node.Force();
			Console.NotifyMessage("メモの取得を試みます");
		}

		[Description("メールアドレスを設定します")]
		public void Email(String s)
		{
			if (!String.IsNullOrEmpty(s))
			{
				Node.Email = s;
				Node.Login();
			}
			Console.NotifyMessage(String.Format("Email = {0}", Node.Email));
		}

		[Description("パスワードを設定します")]
		public void Password(String s)
		{
			if (!String.IsNullOrEmpty(s))
			{
				Node.Password = BindUtility.Encrypt(s);
				Node.Login();
			}
			Console.NotifyMessage(String.Format("Password = {0}", BindUtility.Decrypt(Node.Password)));
		}

		[Description("SiteInfo を更新します。")]
		public void UpdateSiteInfo()
		{
			if (Node.UpdateSiteInfo(true))
				Console.NotifyMessage("SiteInfo を更新しました。");
			else
				Console.NotifyMessage("SiteInfo の更新に失敗しました。");
		}

		protected override void OnPreSaveConfig()
		{
			base.OnPreSaveConfig();
		}

		protected override void OnPostSaveConfig()
		{
			// チャンネルを作成
			CreateGroup(Node.ChannelName);

			// タイマーの状態を更新
			Node.Update();

			base.OnPostSaveConfig();
		}
	}

	[Serializable]
	public class MixiVoiceException : Exception
	{
		public MixiVoiceException() { }
		public MixiVoiceException(string message) : base(message) { }
		public MixiVoiceException(string message, Exception inner) : base(message, inner) { }
		protected MixiVoiceException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	namespace MixiVoice
	{
		public class Api : CookieAuthApi
		{
			private Dictionary<String, Regex> Regexes { get; set; }
			private String _postKey = String.Empty;

			public Api()
			{
				Encoding = Encoding.GetEncoding("euc-jp");
				Regexes = new Dictionary<String, Regex>();
			}

			private IEnumerable<String> GetKeys()
			{
				return new String[] 
				{
					"post_key",
					"voice",
					"name",
					"text",
					"post_time",
				};
			}

			public void Login(String email, String password)
			{
				var result = Post("https://mixi.jp/login.pl", new NameValueCollection()
				{
					{ "email", email },
					{ "password", password },
					{ "next_url", "/home.pl" },
				});
			}

			public void AddVoice(String text)
			{
				if (!String.IsNullOrEmpty(_postKey))
					GetRecentVoice();

				var result = Post("http://mixi.jp/add_voice.pl", new NameValueCollection()
				{
					{ "body", text },
					{ "post_key", _postKey },
					{ "redirect", "recent_voice.pl" },
				});
			}

			public IEnumerable<Status> GetRecentVoice()
			{
				var response = Get("http://mixi.jp/recent_voice.pl", null);
				_postKey = Regexes["post_key"].Match(response).Groups[1].Value;

				return Regexes["voice"].Matches(response).Cast<Match>()
					.Select(m => m.Groups[1].Value)
					.Select(s => new Status()
					{
						Name = Regexes["name"].Match(s).Groups[1].Value.Trim(),
						PostTime = DateTime.ParseExact(Regexes["post_time"].Match(s).Groups[1].Value.Trim(), "yyyyMMddHHmmss", null),
						Text = BindUtility.RemoveHtmlTag(Regexes["text"].Match(s).Groups[1].Value.Trim()),
					});
			}

			public Boolean UpdateSiteInfo()
			{
				try
				{
					var json = Get("http://wedata.net/items/33692.json", null, 10 * 1000);
					var data = Regex.Match(json, "\"data\":\\s*{(.*?)}", RegexOptions.Singleline).Groups[1].Value;
					return GetKeys().All(key => UpdateSiteInfo(key, GetJsonValue(data, key)));
				}
				catch (Exception)
				{
					return false;
				}
			}

			public Boolean UpdateSiteInfo(IDictionary<String, String> patterns)
			{
				var result = GetKeys().All(key =>
				{
					String pattern = null;
					if (patterns.TryGetValue(key, out pattern))
						return UpdateSiteInfo(key, pattern);
					else
						return false;
				});

				if (!result)
				{
					// 1件でも取得できなかったらwedataから全件更新
					result = UpdateSiteInfo();
					foreach (var kv in Regexes)
					{
						patterns[kv.Key] = kv.Value.ToString();
					}
				}

				return result;
			}

			private Boolean UpdateSiteInfo(String key, String pattern)
			{
				Regexes[key] = new Regex(pattern, RegexOptions.Singleline);
				return true;
			}

			private String GetJsonValue(String json, String name)
			{
				return Regex.Match(
					json.Replace("\\\\", "\\"),
					String.Format("\"{0}\": \"(.*?)\",?\n", name)).Groups[1].Value;
			}
		}

		public class Status
		{
			public DateTime PostTime { get; set; }
			public String Name { get; set; }
			public String Text { get; set; }
		}
	}
}

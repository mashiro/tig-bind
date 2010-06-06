using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind.Node
{
	[XmlType("Wassr")]
	public class BindWassrNode : BindTimerNodeBase
	{
		[Browsable(false)]
		public String Username { get; set; }

		[Browsable(false)]
		public String Password { get; set; }

		[Description("コンテンツが流れるチャンネル名を指定します")]
		public String ChannelName { get; set; }

		private ITypableMapGenericRepositoryFactory<Wassr.Status> _typableMapFactory;
		private TypableMapGenericCommandProcessor<Wassr.Status> _typableMapCommands;
		private DateTime _lastAccessDateTime = DateTime.MinValue;

		public override String GetChannelName() { return ChannelName; }
		public override String GetNodeName() { return "Wassr"; }
		public override Type GetContextType() { return typeof(BindWassrEditContext); }

		public BindWassrNode()
		{
			Interval = 90;
			Username = String.Empty;
			Password = String.Empty;
			ChannelName = "#" + GetNodeName();
		}

		public override void Initialize(BindAddIn addIn)
		{
			base.Initialize(addIn);

			_typableMapFactory = new TypableMapGenericMemoryRepositoryFactory<Wassr.Status>();
			_typableMapCommands = new TypableMapGenericCommandProcessor<Wassr.Status>(_typableMapFactory, AddIn.CurrentSession, AddIn.CurrentSession.Config.TypableMapKeySize, this);
			_typableMapCommands.AddCommand(new PermalinkCommand<Wassr.Status>(s => s.Link));
			_typableMapCommands.AddCommand(new HomelinkCommand<Wassr.Status>(s => String.Format("http://wassr.jp/user/{0}", s.UserLoginId)));
			_typableMapCommands.AddCommand(new Wassr.ReCommand());
		}

		public Wassr.Api CreateApi()
		{
			return new Wassr.Api()
			{
				Username = Username,
				Password = BindUtility.Decrypt(Password),
				EnableCompression = AddIn.EnableCompression,
			};
		}

		public void Reset()
		{
			_lastAccessDateTime = DateTime.MinValue;
		}

		public override string ToString()
		{
			return String.Format("{0} ({1})", Username, Interval);
		}

		protected override bool IsValid()
		{
			return base.IsValid()
				&& !String.IsNullOrEmpty(Username)
				&& !String.IsNullOrEmpty(Password);
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
					if (AddIn.EnableTypableMap && _typableMapCommands.Process(e.Message))
						return;
				}

				var api = CreateApi();
				api.Update(e.Message.Content, null);
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
				var api = CreateApi();
				var statuses = api.GetFriendsTimeline().Status
					.Where(s => s.CreatedAt > _lastAccessDateTime)
					.OrderBy(s => s.CreatedAt)
					.ToList();

				if (statuses.Count > 0)
				{
					foreach (var status in statuses)
					{
						Send(status, isFirstTime);
					}

					_lastAccessDateTime = statuses.Last().CreatedAt;
				}
			}
			catch (Exception ex)
			{
				SendException(ex);
			}
		}

		private void Send(Wassr.Status status, Boolean isFirstTime)
		{
			StringBuilder sb = new StringBuilder();
			if (!String.IsNullOrEmpty(status.ReplyUserLoginId))
				sb.AppendFormat("@{0} ", status.ReplyUserLoginId);
			sb.Append(status.Text);

			String content = AddIn.ApplyTypableMap(sb.ToString(), status, _typableMapCommands.TypableMap);
			content = AddIn.ApplyDateTime(content, status.CreatedAt, isFirstTime);
			SendMessage(status.UserLoginId, content, isFirstTime);

			AddIn.SleepClientMessageWait();
		}
	}

	public class BindWassrEditContext : BindEditContextBase
	{
		public new BindWassrNode Node { get { return base.Node as BindWassrNode; } set { base.Node = value; } }

		[Description("タイムラインの取得を試みます")]
		public void Test()
		{
			CreateGroup(Node.ChannelName);
			Node.Reset();
			Node.Force();
			Console.NotifyMessage("タイムラインの取得を試みます");
		}

		[Description("ユーザ名を設定します")]
		public void Username(String s)
		{
			if (!String.IsNullOrEmpty(s))
				Node.Username = s;
			Console.NotifyMessage(String.Format("Username = {0}", Node.Username));
		}

		[Description("パスワードを設定します")]
		public void Password(String s)
		{
			if (!String.IsNullOrEmpty(s))
				Node.Password = BindUtility.Encrypt(s);
			Console.NotifyMessage(String.Format("Password = {0}", BindUtility.Decrypt(Node.Password)));
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

	namespace Wassr
	{
		public class WassrException : Exception
		{
			public WassrException() { }
			public WassrException(string message) : base(message) { }
			public WassrException(string message, Exception inner) : base(message, inner) { }
			protected WassrException(
			  System.Runtime.Serialization.SerializationInfo info,
			  System.Runtime.Serialization.StreamingContext context)
				: base(info, context) { }
		}

		public class Api : ApiBase
		{
			public Statuses GetFriendsTimeline()
			{
				String data = Get("http://api.wassr.jp/statuses/friends_timeline.xml", null);
				return Deserialize<Statuses>(data);
			}

			public void Update(String status, String replyStatusRId)
			{
				NameValueCollection options = new NameValueCollection();
				options.Add("status", Uri.EscapeDataString(status));
				if (!String.IsNullOrEmpty(replyStatusRId))
					options.Add("reply_status_rid", replyStatusRId);
				String data = Post("http://api.wassr.jp/statuses/update.json", options);
				if (data.IndexOf("\"error\":") > -1)
					throw new WassrException();
			}

			private static DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0);
			public static DateTime EpochToDateTime(Double epoch)
			{
				return _epoch.AddSeconds(epoch).ToLocalTime();
			}
		}

		#region Model
		[XmlRoot("statuses")]
		public class Statuses
		{
			[XmlElement("status")]
			public List<Status> Status { get; set; }
		}

		public class Status
		{
			[XmlElement("user_login_id")]
			public String UserLoginId { get; set; }

			[XmlElement("areacode")]
			public String AreaCode { get; set; }

			[XmlElement("photo_thumbnail_url")]
			public String PhotoThumbnailUrl { get; set; }

			[XmlElement("html")]
			public String Html { get; set; }

			[XmlElement("reply_status_url")]
			public String ReplyStatusUrl { get; set; }

			[XmlElement("text")]
			public String Text { get; set; }

			[XmlElement("id")]
			public String Id { get; set; }

			[XmlElement("link")]
			public String Link { get; set; }

			[XmlElement("reply_user_login_id")]
			public String ReplyUserLoginId { get; set; }

			[XmlElement("epoch")]
			public Double Epoch { get; set; }
			public DateTime CreatedAt { get { return Api.EpochToDateTime(Epoch); } }

			[XmlElement("rid")]
			public String RId { get; set; }

			[XmlElement("photo_rl")]
			public String PhotoUrl { get; set; }

			[XmlElement("reply_message")]
			public String ReplyMessage { get; set; }

			[XmlElement("reply_user_nick")]
			public String ReplyUserNick { get; set; }

			[XmlElement("slurl")]
			public String SlUrl { get; set; }

			[XmlElement("areaname")]
			public String AreaName { get; set; }

			[XmlElement("user")]
			public User User { get; set; }
		}

		public class User
		{
			[XmlElement("protected")]
			public Boolean Protected { get; set; }

			[XmlElement("profile_image_url")]
			public String ProfileImageUrl { get; set; }

			[XmlElement("screen_name")]
			public String ScreenName { get; set; }
		}
		#endregion

		#region Command
		public class ReCommand : ITypableMapGenericCommand<Wassr.Status>
		{
			public string CommandName { get { return "re"; } }

			public bool Process(TypableMapGenericCommandProcessor<Wassr.Status> processor, PrivMsgMessage msg, Wassr.Status value, string args)
			{
				var session = processor.Session;
				if (args.Trim() == String.Empty)
				{
					session.SendChannelMessage(msg.Receiver, Server.ServerNick, "返信に空メッセージの送信はできません。", true, false, false, true);
					return true;
				}

				var node = processor.State as BindWassrNode;

				// エコーバック
				String replyMsg = String.Format("@{0} {1}", value.UserLoginId, args);
				session.SendChannelMessage(msg.Receiver, node.Username, replyMsg, true, false, false, false);

				// 返信
				var api = node.CreateApi();
				api.Update(args, value.RId);

				return true;
			}
		}
		#endregion
	}
}

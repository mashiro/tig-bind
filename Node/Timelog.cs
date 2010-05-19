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
	[XmlType("Timelog")]
	public class BindTimelogNode : BindTimerNodeBase
	{
		[Browsable(false)]
		public String Username { get; set; }

		[Browsable(false)]
		public String Password { get; set; }

		[Description("コンテンツが流れるチャンネル名を指定します")]
		public String ChannelName { get; set; }

		[Description("一回の取得につき何件取得するかを指定します (10-50)")]
		public Int32 FetchCount { get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Timelog.Api Api { get; private set; }

		private ITypableMapGenericRepositoryFactory<Timelog.Entry> _typableMapFactory;
		private TypableMapGenericCommandProcessor<Timelog.Entry> _typableMapCommands;
		private DateTime _since = DateTime.MinValue;

		public override String GetChannelName() { return ChannelName; }
		public override String GetNodeName() { return "Timelog"; }
		public override Type GetContextType() { return typeof(BindTimelogEditContext); }

		public BindTimelogNode()
		{
			Interval = 90;
			Username = String.Empty;
			Password = String.Empty;
			ChannelName = "#" + GetNodeName();
			FetchCount = 10;			
		}

		public override void Initialize(BindAddIn addIn)
		{
			base.Initialize(addIn);

			Api = new Timelog.Api() { Username = Username, Password = BindUtility.Decrypt(Password) };
			_typableMapFactory = new TypableMapGenericMemoryRepositoryFactory<Timelog.Entry>();
			_typableMapCommands = new TypableMapGenericCommandProcessor<Timelog.Entry>(_typableMapFactory, AddIn.CurrentSession, AddIn.CurrentSession.Config.TypableMapKeySize, this);
			_typableMapCommands.AddCommand(new PermalinkCommand<Timelog.Entry>(e => String.Format("http://timelog.jp/msg/?{0}", e.Id)));
			_typableMapCommands.AddCommand(new HomelinkCommand<Timelog.Entry>(e => String.Format("http://{0}.timelog.jp", e.Author.Id)));
			_typableMapCommands.AddCommand(new Timelog.ReCommand());
		}

		public void Reset()
		{
			_since = DateTime.MinValue;
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
		public override void OnMessageReceived(StatusUpdateEventArgs e)
		{
			e.Cancel = true;

			try
			{
				if (IsValid())
				{
					if (AddIn.EnableTypableMap && _typableMapCommands.Process(e.ReceivedMessage))
						return;

					Api.New(e.Text, null);
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
				var memos = Api.GetMemos(FetchCount, _since);
				var entries = memos.Entry.OrderBy(e => e.Modified).ToList();
				if (entries.Count > 0)
				{
					foreach (var entry in entries)
					{
						Send(entry, isFirstTime);
					}

					_since = entries.Last().Modified;
					_since = _since.AddMinutes(1); // 同じのをなんども返すのでちょっと加算
				}
			}
			catch (Exception ex)
			{
				SendException(ex);
			}
		}

		private void Send(Timelog.Entry entry, Boolean isFirstTime)
		{
			// そのまんま流すといろいろ足りないので適当に整形
			StringBuilder sb = new StringBuilder();
			if (!String.IsNullOrEmpty(entry.ToId)) sb.AppendFormat("@{0} ", entry.ToId);
			sb.Append(entry.Memo.Trim(new Char[] { '\r', '\n', ' ' })); // 行末の改行とかがうざいので取り除く
			if (!String.IsNullOrEmpty(entry.Tag)) sb.AppendFormat(" [{0}]", entry.Tag);

			String content = AddIn.ApplyTypableMap(sb.ToString(), entry, _typableMapCommands.TypableMap);
			content = AddIn.ApplyDateTime(content, entry.Modified, isFirstTime);
			SendMessage(entry.Author.Id, content, isFirstTime);

			AddIn.SleepClientMessageWait();
		}
	}

	public class BindTimelogEditContext : BindEditContextBase
	{
		public new BindTimelogNode Node { get { return base.Node as BindTimelogNode; } set { base.Node = value; } }

		[Description("メモの取得を試みます")]
		public void Test()
		{
			CreateGroup(Node.ChannelName);
			Node.Reset();
			Node.Force();
			Console.NotifyMessage("メモの取得を試みます");
		}

		[Description("ユーザ名を設定します")]
		public void Username(String s)
		{
			if (!String.IsNullOrEmpty(s))
			{
				Node.Username = s;
				Node.Api.Username = s;
			}
			Console.NotifyMessage(String.Format("Username = {0}", Node.Username));
		}

		[Description("パスワードを設定します")]
		public void Password(String s)
		{
			if (!String.IsNullOrEmpty(s))
			{
				Node.Password = BindUtility.Encrypt(s);
				Node.Api.Password = s;
			}
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

	namespace Timelog
	{
		public class TimelogException : Exception
		{
			public TimelogException() { }
			public TimelogException(string message) : base(message) { }
			public TimelogException(string message, Exception inner) : base(message, inner) { }
			protected TimelogException(
			  System.Runtime.Serialization.SerializationInfo info,
			  System.Runtime.Serialization.StreamingContext context)
				: base(info, context) { }
		}

		public class Api : ApiBase
		{
			public static DateTime ParseDateTime(String str)
			{
				return DateTime.ParseExact(str, "yyyy/MM/dd H:mm:ss", null);
			}

			public Memos GetMemos(Int32 count, DateTime since)
			{
				NameValueCollection options = new NameValueCollection();
				options.Add("fmt", "xml");
				options.Add("cnt", count.ToString());
				if (since != DateTime.MinValue)
					options.Add("since", since.ToString("yyyyMMddHHmm"));
				String data = Get("http://api.timelog.jp/friends_msg.asp", options);
				return Deserialize<Memos>(data);
			}

			public void New(String text, String reMsgId)
			{
				NameValueCollection options = new NameValueCollection();
				options.Add("text", Uri.EscapeDataString(text));
				if (!String.IsNullOrEmpty(reMsgId))
					options.Add("remsgid", reMsgId);
#if DEBUG
				String data = Post("http://api.timelog.jp/newtest.asp", options);
#else
				String data = Post("http://api.timelog.jp/new.asp", options);
#endif
				// 空っぽだったら失敗
				if (!String.IsNullOrEmpty(data))
					throw new TimelogException();
			}
		}

		#region Model
		[XmlRoot("memos")]
		public class Memos
		{
			[XmlElement("title")]
			public String Title { get; set; }

			[XmlElement("link")]
			public Link Link { get; set; }

			[XmlElement("modified")]
			public String _modified;
			public DateTime Modified { get { return Timelog.Api.ParseDateTime(_modified); } }

			[XmlElement("author")]
			public Author Author { get; set; }

			[XmlElement("entry")]
			public List<Entry> Entry { get; set; }
		}

		public class Author
		{
			[XmlElement("id")]
			public String Id { get; set; }

			[XmlElement("name")]
			public String Name { get; set; }

			[XmlElement("image")]
			public Image Image { get; set; }

			[XmlElement("fromid")]
			public String FromId { get; set; }

			[XmlElement("fromname")]
			public String FromName { get; set; }
		}

		public class Image
		{
			[XmlElement("normal")]
			public String Normal { get; set; }

			[XmlElement("thumb")]
			public String Thumb { get; set; }

			[XmlElement("small")]
			public String Small { get; set; }
		}

		public class Entry
		{
			[XmlElement("id")]
			public String Id { get; set; }

			[XmlElement("dispflag")]
			public Int32 DispFlag { get; set; }

			[XmlElement("memo")]
			public String Memo { get; set; }

			[XmlElement("toid")]
			public String ToId { get; set; }

			[XmlElement("toname")]
			public String ToName { get; set; }

			[XmlElement("replyid")]
			public String ReplyId { get; set; }

			[XmlElement("tag")]
			public String Tag { get; set; }

			[XmlElement("author")]
			public Author Author { get; set; }

			[XmlElement("link")]
			public Link Link { get; set; }

			[XmlElement("modified")]
			public String _modified;
			public DateTime Modified { get { return Timelog.Api.ParseDateTime(_modified); } }

			[XmlElement("star")]
			public Int32 Star { get; set; }
		}

		public class Link
		{
			[XmlAttribute("rel")]
			public String Rel { get; set; }

			[XmlAttribute("type")]
			public String Type { get; set; }

			[XmlAttribute("href")]
			public String HRef { get; set; }
		}
		#endregion

		#region Command
		public class ReCommand : ITypableMapGenericCommand<Timelog.Entry>
		{
			public string CommandName { get { return "re"; } }

			public bool Process(TypableMapGenericCommandProcessor<Entry> processor, PrivMsgMessage msg, Entry value, string args)
			{
				var session = processor.Session;
				if (args.Trim() == String.Empty)
				{
					session.SendChannelMessage(msg.Receiver, Server.ServerNick, "返信に空メッセージの送信はできません。", true, false, false, true);
					return true;
				}

				var node = processor.State as BindTimelogNode;

				// エコーバック
				String replyMsg = String.Format("@{0} {1}", value.Author.Id, args);
				session.SendChannelMessage(msg.Receiver, node.Username, replyMsg, true, false, false, false);

				// 返信
				node.Api.New(replyMsg, value.Id);

				return true;
			}
		}
		#endregion
	}
}
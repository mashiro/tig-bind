using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Spica.Applications.TwitterIrcGateway.AddIns.OtherSource
{
	public class OtherSourceTimelogItem : OtherSourceTimerItemBase, IMessageReceivable
	{
		[Browsable(false)]
		public String Username { get; set; }

		[Browsable(false)]
		public String Password { get; set; }

		[Description("コンテンツが流れるチャンネル名を指定します")]
		public String ChannelName { get; set; }

		[Description("一回の取得につき何件取得するかを指定します (10-50)")]
		public Int32 FetchCount { get; set; }

		private ITypableMapGenericRepositoryFactory<Timelog.Entry> _typableMapFactory;
		private TypableMapGenericCommandProcessor<Timelog.Entry> _typableMapCommands;		
		private Boolean _isFirstTime = true;
		private DateTime _since = DateTime.MinValue;

		internal override string SourceName { get { return "Timelog"; } }
		internal override Type ContextType { get { return typeof(OtherSourceTimelogEditContext); } }

		public OtherSourceTimelogItem()
		{
			Interval = 60 * 10;
			Username = String.Empty;
			Password = String.Empty;
			ChannelName = "#" + SourceName;
			FetchCount = 10;
		}

		public override void Initialize(OtherSourceAddIn addIn)
		{
			base.Initialize(addIn);

			_typableMapFactory = new TypableMapGenericMemoryRepositoryFactory<Timelog.Entry>();
			_typableMapCommands = new TypableMapGenericCommandProcessor<Timelog.Entry>(_typableMapFactory, AddIn.CurrentSession, AddIn.CurrentSession.Config.TypableMapKeySize);
		}

		public override string ToShortString()
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
		/// タイマーのコールバック処理
		/// </summary>
		protected override void OnTimerCallback()
		{
			try
			{
				Timelog.Api api = new Timelog.Api() { Username = Username, Password = OtherSourceUtility.Decrypt(Password) };
				var memos = api.GetMemos(FetchCount, _since);
				foreach (var entry in memos.Entry.Reverse<Timelog.Entry>())
				{
					SendEntry(entry, _isFirstTime);
				}

				if (memos.Entry.Count > 0)
				{
					_since = memos.Entry.Select(e => e.Modified).Max();
					_since = _since.AddMinutes(1); // 同じのをなんども返すのでちょっと加算
				}

				_isFirstTime = false;
			}
			catch (Exception ex)
			{
				SendException(ChannelName, ex);
			}
		}

		private void SendEntry(Timelog.Entry entry, Boolean notice)
		{
			String content = AddIn.ApplyTypableMap(entry.Memo, entry, _typableMapCommands.TypableMap);
			SendMessage(ChannelName, entry.Author.Id, content, notice);

			AddIn.ClientMessageWait();
		}

		#region IMessageReceivable
		public String GetChannelName()
		{
			return ChannelName;
		}

		public void MessageReceived(StatusUpdateEventArgs e)
		{
			e.Cancel = true;

			if (IsValid())
			{
				Timelog.Api api = new Timelog.Api() { Username = Username, Password = OtherSourceUtility.Decrypt(Password) };
				api.New(e.Text);
			}
		}
		#endregion
	}

	public class OtherSourceTimelogEditContext : OtherSourceEditContextBase
	{
		public new OtherSourceTimelogItem Item { get { return base.Item as OtherSourceTimelogItem; } set { base.Item = value; } }

		[Description("メモの取得を試みます")]
		public void Test()
		{
			CreateGroup(Item.ChannelName);
			Item.Force();
			Console.NotifyMessage("メモの取得を試みます");
		}

		[Description("ユーザ名を設定します")]
		public void Username(String s)
		{
			Item.Username = s;
			Console.NotifyMessage(String.Format("Username = {0}", Item.Username));
		}

		[Description("パスワードを設定します")]
		public void Password(String s)
		{
			Item.Password = OtherSourceUtility.Encrypt(s);
			Console.NotifyMessage(String.Format("Password = {0}", OtherSourceUtility.Decrypt(Item.Password)));
		}

		protected override void OnPreSaveConfig()
		{
			base.OnPreSaveConfig();
		}

		protected override void OnPostSaveConfig()
		{
			// チャンネルを作成
			CreateGroup(Item.ChannelName);

			// タイマーの状態を更新
			Item.Update();

			base.OnPostSaveConfig();
		}
	}

	namespace Timelog
	{
		public class TypableMapTimelogRepositoryFactory : TypableMapGenericMemoryRepositoryFactory<Timelog.Entry>
		{
		}

		public class TypableMapTimelogCommandProcessor : TypableMapGenericCommandProcessor<Timelog.Entry>
		{
			public TypableMapTimelogCommandProcessor(ITypableMapGenericRepositoryFactory<Timelog.Entry> factory, Session session, Int32 keySize)
				: base(factory, session, keySize)
			{
			}
		}

		public static class Utility
		{
			public static DateTime ParseDateTime(String str)
			{
				return DateTime.ParseExact(str, "yyyy/MM/dd H:mm:ss", null);
			}
		}

		public class Api : ApiBase
		{
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

			public void New(String text)
			{
				NameValueCollection options = new NameValueCollection();
				options.Add("text", Uri.EscapeDataString(text));
#if DEBUG
				String data = Post("http://api.timelog.jp/newtest.asp", options);
#else
				String data = Post("http://api.timelog.jp/new.asp", options);

#endif
				// 成功してもOKしか返さない
			}
		}

		[XmlRoot("memos")]
		public class Memos
		{
			[XmlElement("title")]
			public String Title { get; set; }

			[XmlElement("link")]
			public Link Link { get; set; }

			[XmlElement("modified")]
			public String _modified;
			public DateTime Modified { get { return Timelog.Utility.ParseDateTime(_modified); } }

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
			public DateTime Modified { get { return Timelog.Utility.ParseDateTime(_modified); } }

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
	}
}

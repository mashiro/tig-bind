using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using Spica.Xml.Feed;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Tunnel.Item
{
	public class TunnelFeedItem : TunnelTimerItemBase, IMessageReceivable
	{
		[Description("フィードの URL を指定します")]
		public String Url { get; set; }

		[Description("コンテンツの形式を指定します (書式指定可)")]
		public String ContentFormat { get; set; }

		[Description("コンテンツを送るユーザ名を指定します (書式指定可)")]
		public String SenderNick { get; set; }

		[Description("コンテンツが流れるチャンネル名を指定します")]
		public String ChannelName { get; set; }

		[Description("改行コードの除去を有効化または無効化します")]
		public Boolean EnableRemoveLineBreak { get; set; }

		[Description("HTML タグの除去を有効化または無効化します")]
		public Boolean EnableRemoveHtmlTag { get; set; }

		[Browsable(false)]
		[XmlElement(IsNullable = true)]
		public String Username { get; set; }

		[Browsable(false)]
		[XmlElement(IsNullable = true)]
		public String Password { get; set; }

		[Browsable(false)]
		public DateTime LastPublishDate { get; set; }

		private static readonly Regex _regexHtmlTag = new Regex(@"<[^>]*>");

		internal override string SourceName { get { return "Feed"; } }
		internal override Type ContextType { get { return typeof(TunnelFeedEditContext); } }

		public TunnelFeedItem()
		{
			Interval = 60 * 60;
			Url = String.Empty;
			ContentFormat = "${title} ${link}";
			SenderNick = SourceName;
			ChannelName = "#" + SourceName;
			EnableRemoveLineBreak = false;
			EnableRemoveHtmlTag = false;
			Username = String.Empty;
			Password = String.Empty;
			LastPublishDate = DateTime.MinValue;
		}

		public override string ToShortString()
		{
			return String.Format("{0} ({1})", Url, Interval);
		}

		protected override bool IsValid()
		{
			return base.IsValid()
				&& !String.IsNullOrEmpty(Url);
		}

		/// <summary>
		/// タイマーのコールバック処理
		/// </summary>
		protected override void OnTimerCallback()
		{
			try
			{
				// ユーザ名とパスワードが設定されているなら BASIC 認証
				NetworkCredential credential = null;
				if (!String.IsNullOrEmpty(Username) && !String.IsNullOrEmpty(Password))
				{
					String password = TunnelUtility.Decrypt(Password);
					credential = new NetworkCredential(Username, password);
				}

				// フィードを取得
				IFeedDocument doc = FeedDocument.Load(Url, credential);
				var updates = doc.Items.Where(item => item.PublishDate > LastPublishDate).ToList();
				if (updates.Count > 0)
				{
					// 最終更新日時を更新
					LastPublishDate = updates.Max(item => item.PublishDate);
					AddIn.SaveConfig(false);

					// 日時で昇順にソート
					updates.Sort((a, b) => Comparer<DateTime>.Default.Compare(a.PublishDate, b.PublishDate));
					foreach (var item in updates)
					{
						// 送信
						SendFeedItem(doc, item);
					}
				}
			}
			catch (Exception ex)
			{
				SendException(ChannelName, ex);
			}
		}

		/// <summary>
		/// フィードのエントリを送信する。
		/// </summary>
		private void SendFeedItem(IFeedDocument doc, IFeedItem item)
		{
			String replacedSender = ReplaceFormattedString(SenderNick, doc, item);
			String replacedContent = ReplaceFormattedString(ContentFormat, doc, item);
			replacedContent = AddIn.ApplyTypableMap(replacedContent, FeedItemToStatus(item));
			SendMessage(ChannelName, replacedSender, replacedContent, false);

			AddIn.ClientMessageWait();
		}

		/// <summary>
		/// 書式付き文字列を値で置き換える
		/// </summary>
		private String ReplaceFormattedString(String str, IFeedDocument doc, IFeedItem item)
		{
			Func<String, String> conv = s =>
			{
				if (String.IsNullOrEmpty(s))
					return String.Empty;

				// 改行コードを削除
				if (EnableRemoveLineBreak)
					s = TunnelUtility.RemoveLineBreak(s);

				// HTMLタグを削除
				if (EnableRemoveHtmlTag)
					s = _regexHtmlTag.Replace(s, String.Empty);

				// HTML デコード
				return Utility.UnescapeCharReference(s);
			};

			if (doc != null)
			{
				str = ReplacePlaceholder(str, "feed_title", conv(doc.Title));
				str = ReplacePlaceholder(str, "feed_link", conv(doc.Link.ToString()));
				str = ReplacePlaceholder(str, "feed_description", conv(doc.Description));
			}
			if (item != null)
			{
				str = ReplacePlaceholder(str, "author", conv(item.Author));
				str = ReplacePlaceholder(str, "link", conv(item.Link.ToString()));
				str = ReplacePlaceholder(str, "title", conv(item.Title));
				str = ReplacePlaceholder(str, "description", conv(item.Description));
				str = ReplacePlaceholder(str, "publish_date", conv(item.PublishDate.ToString()));
			}

			return str;
		}

		/// <summary>
		/// プレイスホルダーを置き換える
		/// </summary>
		private String ReplacePlaceholder(String input, String placeholder, String replacement)
		{
			// ${xxx}
			return input.Replace(String.Format("${{{0}}}", placeholder), replacement);
		}

		/// <summary>
		/// FeedItem から Status に無理矢理変換
		/// </summary>
		private Status FeedItemToStatus(IFeedItem item)
		{
			return new Status()
			{
				CreatedAt = item.PublishDate,
				Source = item.Link.ToString(),
				Text = item.Description,
				User = new User()
				{
					Name = item.Title,
					ScreenName = item.Author,
				},
			};
		}

		#region IMessageReceivable
		public String GetChannelName()
		{
			return ChannelName;
		}

		public void MessageReceived(StatusUpdateEventArgs e)
		{
			// とりあえず殺す
			e.Cancel = true;
		}
		#endregion
	}

	public class TunnelFeedEditContext : TunnelEditContextBase
	{
		private const String FormatMessage = @"${feed_title} - フィードのタイトル
${feed_link} - フィードのリンク
${feed_description} - フィードの説明
${author} - 記事の著者
${link} - 記事のリンク
${title} - 記事のタイトル
${description} - 記事の説明
${publish_date} - 記事の公開された日時";

		private Boolean _urlChanged = false;
		public new TunnelFeedItem Item { get { return base.Item as TunnelFeedItem; } set { base.Item = value; } }

		protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
		{
			if (config is TunnelFeedItem)
			{
				if (memberInfo.Name == "Url")
					_urlChanged = true;
			}
		}

		[Description("フィードの取得を試みます")]
		public void Test()
		{
			CreateGroup(Item.ChannelName);
			Item.LastPublishDate = DateTime.MinValue;
			Item.Force();
			Console.NotifyMessage("フィードの取得を試みます");
		}

		[Description("書式指定子の一覧を表示します")]
		public void ShowFormat()
		{
			Console.NotifyMessage(FormatMessage);
		}

		[Description("BASIC 認証に使用するユーザ名を設定します")]
		public void Username(String s)
		{
			Item.Username = s;
			Console.NotifyMessage(String.Format("Username = {0}", Item.Username));
		}

		[Description("BASIC 認証に使用するパスワードを設定します")]
		public void Password(String s)
		{
			Item.Password = TunnelUtility.Encrypt(s);
			Console.NotifyMessage(String.Format("Password = {0}", TunnelUtility.Decrypt(Item.Password)));
		}

		protected override void OnPreSaveConfig()
		{
			base.OnPreSaveConfig();

			if (_urlChanged)
				Item.LastPublishDate = DateTime.MinValue;
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
}

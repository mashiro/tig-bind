using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Spica.Xml.Feed;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind.Node
{
	[XmlType("Feed")]
	public class BindFeedNode : BindTimerNodeBase
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

		private DateTime _lastPublishDate = DateTime.MinValue;

		public override String GetChannelName() { return ChannelName; }
		public override String GetNodeName() { return "Feed"; }
		public override Type GetContextType() { return typeof(BindFeedEditContext); }

		public BindFeedNode()
		{
			Interval = 60 * 60;
			Url = String.Empty;
			ContentFormat = "${title} ${link}";
			SenderNick = GetNodeName();
			ChannelName = "#" + GetNodeName();
			EnableRemoveLineBreak = false;
			EnableRemoveHtmlTag = false;
			Username = String.Empty;
			Password = String.Empty;
		}

		public BasicAuthApi CreateApi()
		{
			return new BasicAuthApi()
			{
				Username = Username,
				Password = BindUtility.Decrypt(Password),
				EnableCompression = AddIn.EnableCompression,
			};
		}

		public void Reset()
		{
			_lastPublishDate = DateTime.MinValue;
		}

		public override string ToString()
		{
			return String.Format("{0} ({1})", Url, Interval);
		}

		protected override bool IsValid()
		{
			return base.IsValid()
				&& !String.IsNullOrEmpty(Url);
		}

		/// <summary>
		/// メッセージ受信時の処理
		/// </summary>
		public override void OnMessageReceived(BindPrivMessageReceivedEventArgs e)
		{
			// とりあえず殺す
			e.Cancel = true;
		}

		/// <summary>
		/// タイマーのコールバック処理
		/// </summary>
		protected override void OnTimerCallback(Boolean isFirstTime)
		{
			try
			{
				// フィードを取得
				var api = CreateApi();
				IFeedDocument doc = FeedDocument.Load(new StringReader(api.Get(Url, null)));
				if (doc != null)
				{
					var items = doc.Items
						.Where(item => item.PublishDate > _lastPublishDate)
						.OrderBy(item => item.PublishDate)
						.ToList();

					if (items.Count > 0)
					{
						foreach (var item in items)
						{
							// 送信
							Send(doc, item, isFirstTime);
						}

						// 最終更新日時を更新
						_lastPublishDate = items.Last().PublishDate;
					}
				}
			}
			catch (Exception ex)
			{
				SendException(ex);
			}
		}

		/// <summary>
		/// フィードのエントリを送信する。
		/// </summary>
		private void Send(IFeedDocument doc, IFeedItem item, Boolean isFirstTime)
		{
			String replacedSender = ReplaceFormattedString(SenderNick, doc, item);
			String replacedContent = ReplaceFormattedString(ContentFormat, doc, item);
			replacedContent = AddIn.ApplyTypableMap(replacedContent, FeedItemToStatus(item));
			replacedContent = AddIn.ApplyDateTime(replacedContent, item.PublishDate, isFirstTime);
			SendMessage(replacedSender, replacedContent, isFirstTime);

			AddIn.SleepClientMessageWait();
		}

		/// <summary>
		/// 書式付き文字列を値で置き換える
		/// </summary>
		private String ReplaceFormattedString(String str, IFeedDocument doc, IFeedItem item)
		{
			Func<Object, String> conv = obj =>
			{
				var s = obj != null ? obj.ToString() : String.Empty;
				if (String.IsNullOrEmpty(s))
					return String.Empty;

				// 改行コードを削除
				if (EnableRemoveLineBreak)
					s = BindUtility.RemoveLineBreak(s);

				// HTMLタグを削除
				if (EnableRemoveHtmlTag)
					s = BindUtility.RemoveHtmlTag(s);

				// HTML デコード
				return Utility.UnescapeCharReference(s);
			};

			if (doc != null)
			{
				str = ReplacePlaceholder(str, "feed_title", conv(doc.Title));
				str = ReplacePlaceholder(str, "feed_link", conv(doc.Link));
				str = ReplacePlaceholder(str, "feed_description", conv(doc.Description));
			}
			if (item != null)
			{
				str = ReplacePlaceholder(str, "author", conv(item.Author));
				str = ReplacePlaceholder(str, "link", conv(item.Link));
				str = ReplacePlaceholder(str, "title", conv(item.Title));
				str = ReplacePlaceholder(str, "description", conv(item.Description));
				str = ReplacePlaceholder(str, "publish_date", conv(item.PublishDate));
			}

			return str;
		}

		/// <summary>
		/// プレイスホルダーを置き換える
		/// </summary>
		private String ReplacePlaceholder(String input, String placeholder, String replacement)
		{
			// ${xxx}
			return input.Replace("${" + placeholder + "}", replacement);
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
	}

	public class BindFeedEditContext : BindEditContextBase
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
		public new BindFeedNode Node { get { return base.Node as BindFeedNode; } set { base.Node = value; } }

		protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
		{
			if (config is BindFeedNode)
			{
				if (memberInfo.Name == "Url")
					_urlChanged = true;
			}
		}

		[Description("フィードの取得を試みます")]
		public void Test()
		{
			CreateGroup(Node.ChannelName);
			Node.Reset();
			Node.Force();
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
			if (!String.IsNullOrEmpty(s))
				Node.Username = s;
			Console.NotifyMessage(String.Format("Username = {0}", Node.Username));
		}

		[Description("BASIC 認証に使用するパスワードを設定します")]
		public void Password(String s)
		{
			if (!String.IsNullOrEmpty(s))
				Node.Password = BindUtility.Encrypt(s);
			Console.NotifyMessage(String.Format("Password = {0}", BindUtility.Decrypt(Node.Password)));
		}

		protected override void OnPreSaveConfig()
		{
			base.OnPreSaveConfig();

			if (_urlChanged)
				Node.Reset();
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
}

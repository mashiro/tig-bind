﻿using System;
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
using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Spica.Applications.TwitterIrcGateway.AddIns.OtherSource
{
	public class OtherSourceListsItem : OtherSourceTimerItemBase, IMessageReceivable
	{
		[Description("ユーザ名、またはユーザ ID を指定します")]
		public String UserId { get; set; }

		[Description("リスト名、またはリスト ID を指定します")]
		public String ListId { get; set; }

		[Description("コンテンツが流れるチャンネル名を指定します")]
		public String ChannelName { get; set; }

		[Description("タイムラインの一回の取得につき何件取得するかを指定します")]
		public Int32 FetchCount { get; set; }

		[Description("ステータスを複製するかを有効化または無効化します")]
		public Boolean Duplicate { get; set; }

		private Boolean _isFirstTime = true;
		private Int64 _sinceId = 1;

		internal override string SourceName { get { return "Lists"; } }
		internal override Type ContextType { get { return typeof(OtherSourceListsEditContext); } }

		public OtherSourceListsItem()
		{
			Interval = 90;
			UserId = String.Empty;
			ListId = String.Empty;
			ChannelName = "#" + SourceName;
			FetchCount = 50;
			Duplicate = false;
		}

		public override string ToShortString()
		{
			return String.Format("{0}/{1} ({2})", UserId, ListId, Interval);
		}

		/// <summary>
		/// タイマーのコールバック処理
		/// </summary>
		protected override void OnTimerCallback()
		{
			try
			{
				Statuses statuses = AddIn.GetListsStatuses(UserId, ListId, _sinceId, FetchCount);
				Array.Reverse(statuses.Status);

				foreach (Status status in statuses.Status)
				{
					SendStatus(status, _isFirstTime);
				}

				if (statuses.Status != null && statuses.Status.Length > 0)
					_sinceId = statuses.Status.Select(s => s.Id).Max();

				_isFirstTime = false;
			}
			catch (Exception ex)
			{
				SendException(ChannelName, ex);
			}
		}

		/// <summary>
		/// ステータスを送信します。
		/// </summary>
		private void SendStatus(Status status, Boolean notice)
		{
			String content = AddIn.ApplyTypableMap(status.Text, status);
			foreach (String line in OtherSourceUtility.SplitLineBreak(content))
			{
				SendMessage(ChannelName, status.User.ScreenName, line, notice);
			}

			if (Duplicate)
			{
				AddIn.ProcessTimelineStatus(status);
			}
		}

		/// <summary>
		/// 指定したリストのステータスを取得します。
		/// </summary>
		private Statuses GetListsStatuses(String userId, String listId, Int64 sinceId, Int32 count)
		{
			String url = String.Format("/{0}/lists/{1}/statuses.xml?since_id={2}&per_page={3}", userId, listId, sinceId, count);
			String responseBody = AddIn.CurrentSession.TwitterService.GET(url);

			Statuses statuses;
			if (NilClasses.CanDeserialize(responseBody))
			{
				statuses = new Statuses();
				statuses.Status = new Status[0];
			}
			else
			{
				statuses = Statuses.Serializer.Deserialize(new StringReader(responseBody)) as Statuses;
				if (statuses == null || statuses.Status == null)
				{
					statuses = new Statuses();
					statuses.Status = new Status[0];
				}
			}

			return statuses;
		}

		#region IMessageReceivable
		public String GetChannelName()
		{
			return ChannelName;
		}

		public String GetSenderNick()
		{
			return String.Empty;
		}

		public void MessageReceived(StatusUpdateEventArgs e)
		{
			// チャンネル宛じゃないのはDMになってしまうので殺す。
			if (!e.ReceivedMessage.Receiver.StartsWith("#"))
				e.Cancel = true;
		}
		#endregion
	}

	public class OtherSourceListsEditContext : OtherSourceEditContextBase
	{
		public new OtherSourceListsItem Item { get { return base.Item as OtherSourceListsItem; } set { base.Item = value; } }

		[Description("リストの取得を試みます")]
		public void Test()
		{
			Item.Force();
			Console.NotifyMessage("リストの取得を試みます");
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
}

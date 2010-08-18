using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind.Node
{
	[XmlType("Search")]
	public class BindSearchNode : BindTimerNodeBase
	{
		[Description("検索条件を指定します")]
		public String Query { get; set; }

		[Description("コンテンツが流れるチャンネル名を指定します")]
		public String ChannelName { get; set; }

		[Description("ステータスを複製するかを有効化または無効化します")]
		public Boolean Duplicate { get; set; }

		private Int64? _sinceId = null;

		public override String GetChannelName() { return ChannelName; }
		public override String GetNodeName() { return "Search"; }
		public override Type GetContextType() { return typeof(BindSearchEditContext); }

		public BindSearchNode()
		{
			Interval = 300;
			Query = String.Empty;
			ChannelName = "#" + GetNodeName();
			Duplicate = false;
		}

		public override string ToString()
		{
			return String.Format("{0}", Query);
		}

		public void Reset()
		{
			_sinceId = null;
		}

		protected override bool IsValid()
		{
			return base.IsValid()
				&& !String.IsNullOrEmpty(Query);
		}

		/// <summary>
		/// メッセージ受信時の処理
		/// </summary>
		public override void OnMessageReceived(BindPrivMessageReceivedEventArgs e)
		{
			// そのまま Twitter に流す。
		}

		/// <summary>
		/// タイマーのコールバック処理
		/// </summary>
		protected override void OnTimerCallback(Boolean isFirstTime)
		{
			try
			{
				var api = CreateApi<Search.Api>();
				var statuses = api.Search(Query, sinceId: _sinceId);
				foreach (var status in statuses.Results.OrderBy(s => s.CreatedAt))
				{
					Send(status, isFirstTime);
				}

				_sinceId = statuses.MaxId;
			}
			catch (Exception ex)
			{
				SendException(ex);
			}
		}

		/// <summary>
		/// ステータスを送信します。
		/// </summary>
		private void Send(Search.Status status, Boolean isFirstTime)
		{
			var twitterStatus = ToTwitterStatus(status);
			var text = AddIn.ApplyTypableMap(twitterStatus.Text, twitterStatus);
			text = AddIn.ApplyDateTime(text, twitterStatus.CreatedAt, isFirstTime);
			SendMessage(status.FromUser, text, isFirstTime);

			if (Duplicate)
			{
				Boolean friendsCheckRequired = false;
				AddIn.CurrentSession.ProcessTimelineStatus(twitterStatus, ref friendsCheckRequired);
			}

			AddIn.SleepClientMessageWait();
		}

		private Status ToTwitterStatus(Search.Status status)
		{
			return new Status()
			{
				Id = status.Id,
				CreatedAt = status.CreatedAt,
				Text = status.Text,
				Source = status.Source,
				InReplyToStatusId = status.ToUserId,
				User = new User()
				{
					Id = Int32.Parse(status.FromUserId),
					ScreenName = status.FromUser,
					ProfileImageUrl = status.ProfileImageUrl,
				},
			};
		}
	}

	public class BindSearchEditContext : BindEditContextBase
	{
		public new BindSearchNode Node { get { return base.Node as BindSearchNode; } set { base.Node = value; } }

		[Description("検索を試みます")]
		public void Test()
		{
			CreateGroup(Node.ChannelName);
			Node.Reset();
			Node.Force();
			Console.NotifyMessage("検索を試みます");
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

	namespace Search
	{
		public class Api : SimpleApi
		{
			private static readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(Statuses));

			public Statuses Search(
				String query,
				String lang = null,
				Int32? rpp = null,
				Int32? page = null,
				Int64? maxId = null,
				Int64? sinceId = null,
				Boolean? showUser = null,
				String resultType = null)
			{
				var options = new NameValueCollection();
				options["q"] = query;
				options["lang"] = lang;
				options["rpp"] = rpp.ToString();
				options["page"] = page.ToString();
				options["max_id"] = maxId.ToString();
				options["since_id"] = sinceId.ToString();
				options["show_user"] = showUser.ToString().ToLower();
				options["result_type"] = resultType;

				using (var stream = Open("http://search.twitter.com/search.json", Method.Get, options))
				{
					return _serializer.ReadObject(stream) as Statuses;
				}
			}
		}

		[DataContract()]
		public class Statuses
		{
			[DataMember(Name = "results")]
			public List<Status> Results { get; set; }

			[DataMember(Name = "max_id")]
			public Int64 MaxId { get; set; }

			[DataMember(Name = "since_id")]
			public Int64 SinceId { get; set; }

			[DataMember(Name = "refresh_url")]
			public String RefreshUrl { get; set; }

			[DataMember(Name = "next_page")]
			public String NextPage { get; set; }

			[DataMember(Name = "results_per_page")]
			public Int32 ResultsPerPage { get; set; }

			[DataMember(Name = "page")]
			public Int32 Page { get; set; }

			[DataMember(Name = "completed_in")]
			public Double CompletedIn { get; set; }

			[DataMember(Name = "query")]
			public String Query { get; set; }
		}

		[DataContract()]
		public class Status
		{
			[DataMember(Name = "id")]
			public Int64 Id { get; set; }

			[DataMember(Name = "created_at")]
			private String _createdAt = null;
			public DateTime CreatedAt { get { return DateTime.Parse(_createdAt, CultureInfo.InvariantCulture.DateTimeFormat); } }

			[DataMember(Name = "text")]
			public String Text { get; set; }

			[DataMember(Name = "profile_image_url")]
			public String ProfileImageUrl { get; set; }

			[DataMember(Name = "from_user_id")]
			public String FromUserId { get; set; }

			[DataMember(Name = "from_user")]
			public String FromUser { get; set; }

			[DataMember(Name = "to_user_id")]
			public String ToUserId { get; set; }

			[DataMember(Name = "to_user")]
			public String ToUser { get; set; }

			[DataMember(Name = "iso_language_code")]
			public String ISOLanguageCode { get; set; }

			[DataMember(Name = "source")]
			public String Source { get; set; }
		}
	}
}

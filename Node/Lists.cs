using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Globalization;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind.Node
{
    [XmlType("Lists")]
    public class BindListsNode : BindTimerNodeBase
    {
        [Description("ユーザ名を指定します")]
        public String UserId { get; set; }

        [Description("リスト名を指定します")]
        public String ListId { get; set; }

        [Description("コンテンツが流れるチャンネル名を指定します")]
        public String ChannelName { get; set; }

        [Description("タイムラインの一回の取得につき何件取得するかを指定します")]
        public Int32 FetchCount { get; set; }

        [Description("ステータスを複製するかを有効化または無効化します")]
        public Boolean Duplicate { get; set; }

        private Int64 _sinceId = 1;

        public override String GetChannelName() { return ChannelName; }
        public override String GetNodeName() { return "Lists"; }
        public override Type GetContextType() { return typeof(BindListsEditContext); }

        public BindListsNode()
        {
            Interval = 90;
            UserId = String.Empty;
            ListId = String.Empty;
            ChannelName = "#" + GetNodeName();
            FetchCount = 50;
            Duplicate = false;
        }

        public override string ToString()
        {
            return String.Format("{0}/{1}", UserId, ListId);
        }

        public void Reset()
        {
            _sinceId = 1;
        }

        protected override bool IsValid()
        {
            return base.IsValid()
                && !String.IsNullOrEmpty(UserId)
                && !String.IsNullOrEmpty(ListId);
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
                Statuses tmp = GetListsStatuses(UserId, ListId, _sinceId, FetchCount);
                var statuses = tmp.Status.OrderBy(s => s.CreatedAt).ToList();
                if (statuses.Count > 0)
                {
                    foreach (var status in statuses)
                    {
                        Send(status, isFirstTime);
                    }

                    _sinceId = statuses.Last().Id;
                }
            }
            catch (Exception ex)
            {
                SendException(ex);
            }
        }

        /// <summary>
        /// ステータスを送信します。
        /// </summary>
        private void Send(Status status, Boolean isFirstTime)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(AddIn.ApplyTypableMap(status.Text, status));

            String content = AddIn.ApplyDateTime(sb.ToString(), status.CreatedAt, isFirstTime);
            SendMessage(status.User.ScreenName, content, isFirstTime);

            if (Duplicate)
            {
                Boolean friendsCheckRequired = false;
                AddIn.CurrentSession.ProcessTimelineStatus(status, ref friendsCheckRequired);
            }

            AddIn.SleepClientMessageWait();
        }

        /// <summary>
        /// 指定したリストのステータスを取得します。
        /// </summary>
        private Statuses GetListsStatuses(String userId, String listId, Int64 sinceId, Int32 count)
        {
            String url = String.Format("/lists/statuses.json?slug={0}&owner_screen_name={1}&since_id={2}&count={3}&include_entities=true", listId, userId, sinceId, count);
            String responseBody = AddIn.CurrentSession.TwitterService.GETv1_1(url, "/lists/statuses");

            Statuses statuses = new Statuses();
            statuses.Status = JsonConvert.DeserializeObject<List<Status>>(responseBody).ToArray();

            return statuses;
        }
    }

    internal class TwitterDateTimeConverter : IsoDateTimeConverter
    {
        public TwitterDateTimeConverter()
        {
            DateTimeFormat = "ddd MMM dd HH:mm:ss +0000 yyyy";
            DateTimeStyles = DateTimeStyles.AssumeUniversal;
            Culture = CultureInfo.GetCultureInfo("en-us");
        }
    }

    public class BindListsEditContext : BindEditContextBase
    {
        public new BindListsNode Node { get { return base.Node as BindListsNode; } set { base.Node = value; } }

        [Description("リストの取得を試みます")]
        public void Test()
        {
            CreateGroup(Node.ChannelName);
            Node.Reset();
            Node.Force();
            Console.NotifyMessage("リストの取得を試みます");
        }

        [Description("リストの一覧を取得します")]
        public void List(String screenName = null)
        {
            String url = "/lists/list.json";
            if (!String.IsNullOrEmpty(screenName))
                url += String.Format("?screen_name={0}", screenName);
            String responseBody = AddIn.CurrentSession.TwitterService.GETv1_1(url, "/lists/list");
            List<Model.List> lists = JsonConvert.DeserializeObject<List<Model.List>>(responseBody);

            foreach(var list in lists)
            {
                Console.NotifyMessage(String.Format("{0}: {1}", list.Slug, list.Description));
            }
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

    namespace Model
    {
        public class List
        {
            [JsonProperty("slug")]
            public String Slug { get; set; }

            [JsonProperty("name")]
            public String Name { get; set; }

            [JsonProperty("full_name")]
            public String FullName { get; set; }

            [JsonProperty("description")]
            public String Description { get; set; }

            [JsonProperty("created_at")]
            [JsonConverter(typeof(TwitterDateTimeConverter))]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("uri")]
            public String Uri { get; set; }

            [JsonProperty("subscriber_count")]
            public Int32 SubscriberCount { get; set; }

            [JsonProperty("member_count")]
            public Int32 MemberCount { get; set; }

            [JsonProperty("id")]
            public Int64 Id { get; set; }

            [JsonProperty("mode")]
            public String Mode { get; set; }
        }
    }
}
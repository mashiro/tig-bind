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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
                var result = Search(Query, sinceId: _sinceId);
                foreach (var status in result.Statuses.OrderBy(s => s.CreatedAt))
                {
                    Send(status, isFirstTime);
                }

                _sinceId = result.Metadata.MaxId;
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
            var text = AddIn.ApplyTypableMap(status.Text, status);
            text = AddIn.ApplyDateTime(text, status.CreatedAt, isFirstTime);
            SendMessage(status.User.ScreenName, text, isFirstTime);

            if (Duplicate)
            {
                Boolean friendsCheckRequired = false;
                AddIn.CurrentSession.ProcessTimelineStatus(status, ref friendsCheckRequired);
            }

            AddIn.SleepClientMessageWait();
        }

        public SearchResult Search(
            String query,
            String lang = null,
            String locale = null,
            Int32? count = null,
            Int64? maxId = null,
            Int64? sinceId = null,
            String resultType = null)
        {
            var options = new NameValueCollection();
            options["q"] = query;
            options["lang"] = lang;
            options["locale"] = locale;
            options["count"] = count.ToString();
            options["max_id"] = maxId.ToString();
            options["since_id"] = sinceId.ToString();
            options["result_type"] = resultType;
            options["include_entities"] = "true";

            var queryString = BindUtility.BuildQueryString(options);
            var url = "/search/tweets.json";
            if (!String.IsNullOrEmpty(queryString))
                url += "?" + queryString;

            var responseBody = AddIn.CurrentSession.TwitterService.GETv1_1(url, "/search/tweets");
            return JsonConvert.DeserializeObject<SearchResult>(responseBody);
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


    public class SearchResult
    {
        [JsonProperty("statuses")]
        public List<Status> Statuses { get; set; }

        [JsonProperty("search_metadata")]
        public SearchMetadata Metadata { get; set; }
    }

    public class SearchMetadata
    {
        [JsonProperty("max_id")]
        public Int64 MaxId { get; set; }

        [JsonProperty("since_id")]
        public Int64 SinceId { get; set; }

        [JsonProperty("refresh_url")]
        public String RefreshUrl { get; set; }

        [JsonProperty("next_results")]
        public String NextResults { get; set; }

        [JsonProperty("count")]
        public Int32 Count { get; set; }

        [JsonProperty("completed_in")]
        public Single CompletedIn { get; set; }

        [JsonProperty("query")]
        public String Query { get; set; }
    }
}
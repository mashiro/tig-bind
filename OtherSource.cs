using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap;

namespace Spica.Applications.TwitterIrcGateway.AddIns.OtherSource
{
	#region Interface
	public interface IMessageReceivable
	{
		String GetChannelName();
		void MessageReceived(StatusUpdateEventArgs e);
	}
	#endregion

	#region Configuration
	[XmlType("Item")]
	public abstract class OtherSourceItemBase : IConfiguration
	{
		[Description("ソースを有効化または無効化します")]
		public Boolean Enabled { get; set; }

		internal OtherSourceAddIn AddIn { get; set; }
		internal abstract Type ContextType { get; }
		internal abstract String SourceName { get; }

		public OtherSourceItemBase()
		{
			Enabled = true;
		}

		public virtual void Initialize(OtherSourceAddIn addIn)
		{
			AddIn = addIn;
		}

		public virtual void Uninitialize()
		{
		}

		public override string ToString()
		{
			return ToShortString();
		}

		public virtual string ToShortString()
		{
			return String.Empty;
		}

		public virtual string ToLongString()
		{
			// [*] Feed - http://example.com (3600)
			// [ ] Lists - UserId/ListId (60)
			return String.Format("[{0}] {1} - {2}", Enabled ? "*" : " ", SourceName, ToShortString());
		}

		protected void SendMessage(String receiver, String sender, String content, Boolean notice)
		{
			if (String.IsNullOrEmpty(receiver))
				receiver = AddIn.CurrentSession.CurrentNick;

			IRCMessage message = null;
			if (notice)
				message = new NoticeMessage(receiver, content);
			else
				message = new PrivMsgMessage(receiver, content);

			message.SenderNick = sender;
			message.SenderHost = String.Format("{0}@{1}", SourceName.ToLowerInvariant(), Server.ServerName);
			AddIn.CurrentSession.Send(message);
		}

		protected void SendException(String receiver, Exception exception)
		{
			if (!AddIn.Config.IgnoreWatchError)
			{
				SendMessage(receiver, OtherSourceAddIn.DefaultSenderNick, OtherSourceUtility.RemoveLineBreak(exception.Message), true);
			}
		}
	}

	public abstract class OtherSourceTimerItemBase : OtherSourceItemBase
	{
		[Description("ソースをチェックする間隔を秒単位で指定します")]
		public Int32 Interval { get; set; }

		private static readonly Random _random = new Random();
		private Timer _timer = null;
		private Object _timerSync = new object();

		public OtherSourceTimerItemBase()
		{
			Interval = 90;
		}

		public override void Initialize(OtherSourceAddIn addIn)
		{
			base.Initialize(addIn);
			Update();
		}

		public override void Uninitialize()
		{
			Stop();
			base.Uninitialize();
		}

		#region Timer
		/// <summary>
		/// タイマーの状態を更新します。
		/// </summary>
		public void Update()
		{
			if (Enabled)
				Start();
			else
				Stop();
		}

		/// <summary>
		/// タイマーを開始します。
		/// </summary>
		public void Start()
		{
			if (_timer == null)
				_timer = new Timer(OnTimerCallbackInternal, null, Timeout.Infinite, Timeout.Infinite);

			// HACK: MONO のバグ対策に dueTime を適度にずらす
			lock (_random)
			{
				Int32 intervalMillSec = Interval * 1000;
				Int32 randomDueTime = _random.Next(0, 3000);
				_timer.Change(randomDueTime, intervalMillSec);
			}
		}

		/// <summary>
		/// タイマーを停止します。
		/// </summary>
		public void Stop()
		{
			if (_timer != null)
			{
				_timer.Dispose();
				_timer = null;
			}
		}

		/// <summary>
		/// タイマーのコールバック処理を強制的に実行させます。
		/// </summary>
		public void Force()
		{
			ThreadPool.QueueUserWorkItem((state) => { OnTimerCallbackInternal(state); });
		}

		/// <summary>
		/// タイマーのコールバック処理
		/// </summary>
		private void OnTimerCallbackInternal(Object state)
		{
			if (Monitor.TryEnter(_timerSync))
			{
				try
				{
					OnTimerCallback();
				}
				finally
				{
					Monitor.Exit(_timerSync);
				}
			}
		}

		/// <summary>
		/// タイマーのコールバック処理
		/// </summary>
		protected abstract void OnTimerCallback();
		#endregion
	}

	public class OtherSourceConfiguration : IConfiguration
	{
		[Description("TypableMapを有効化または無効化します")]
		public Boolean EnableTypableMap { get; set; }

		[Description("チャンネル作成時のモードを指定します")]
		public String InitialModes { get; set; }

		[Description("エラーを無視するかどうかを指定します")]
		public Boolean IgnoreWatchError { get; set; }

		[Browsable(false)]
		public List<OtherSourceItemBase> Items { get; set; }

		public OtherSourceConfiguration()
		{
			EnableTypableMap = false;
			InitialModes = "+pni";
			Items = new List<OtherSourceItemBase>();
		}
	}
	#endregion

	#region Context
	[Description("ソースの設定を行うコンテキストに切り替えます")]
	public class OtherSourceContext : Context
	{
		private OtherSourceAddIn AddIn { get { return CurrentSession.AddInManager.GetAddIn<OtherSourceAddIn>(); } }

		public override IConfiguration[] Configurations { get { return new IConfiguration[] { AddIn.Config }; } }
		protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
		{
			if (config is OtherSourceConfiguration)
			{
				AddIn.Config = config as OtherSourceConfiguration;
				AddIn.SaveConfig(true);
			}
		}

		[Description("指定したソースを強制的に更新します")]
		public void UpdateForce(String arg)
		{
			FindAt(arg, item =>
			{
				if (item is OtherSourceTimerItemBase)
				{
					var timerItem = item as OtherSourceTimerItemBase;
					timerItem.Force();
				}

				Console.NotifyMessage(String.Format("ソース {0} を更新しました。", item));
			});
		}

		[Description("存在するソースをすべて表示します")]
		public void List()
		{
			if (AddIn.Config.Items.Count == 0)
			{
				Console.NotifyMessage("ソースは現在設定されていません。");
				return;
			}

			for (Int32 i = 0; i < AddIn.Config.Items.Count; ++i)
			{
				var item = AddIn.Config.Items[i];
				Console.NotifyMessage(String.Format("{0}: {1}", i, item.ToLongString()));
			}
		}

		[Description("指定したソースを有効化します")]
		public void Enable(String arg)
		{
			SwitchEnable(arg, true);
		}

		[Description("指定したソースを無効化します")]
		public void Disable(String arg)
		{
			SwitchEnable(arg, false);
		}

		[Description("指定したソースを削除します")]
		public void Remove(String arg)
		{
			FindAt(arg, item =>
			{
				item.Uninitialize();
				AddIn.Config.Items.Remove(item);
				AddIn.SaveConfig(true);
				Console.NotifyMessage(String.Format("ソース {0} を削除しました。", item));
			});
		}

		[Description("指定したソースを編集します")]
		public void Edit(String arg)
		{
			FindAt(arg, item =>
			{
				// コンテキストを追加
				OtherSourceEditContextBase context = Console.GetContext(item.ContextType, CurrentServer, CurrentSession) as OtherSourceEditContextBase;
				context.Item = item;
				context.IsNew = false;
				Console.PushContext(context);
			});
		}

		[Description("ソースを新規追加します")]
		public void New(String itemTypeName)
		{
			Type itemType = Type.GetType(String.Format("{0}.OtherSource{1}Item", GetType().Namespace, itemTypeName), false, true);
			if (itemType == null || !itemType.IsSubclassOf(typeof(OtherSourceItemBase)))
			{
				Console.NotifyMessage("不明なソースの種類が指定されました。");
				return;
			}

			// 設定を作成
			OtherSourceItemBase item = Activator.CreateInstance(itemType) as OtherSourceItemBase;
			item.AddIn = AddIn;

			// コンテキストを追加
			OtherSourceEditContextBase context = Console.GetContext(item.ContextType, CurrentServer, CurrentSession) as OtherSourceEditContextBase;
			context.Item = item;
			context.IsNew = true;
			Console.PushContext(context);
		}

		private void SwitchEnable(String arg, Boolean enable)
		{
			FindAt(arg, item =>
			{
				item.Enabled = enable;
				AddIn.SaveConfig(true);
				Console.NotifyMessage(String.Format("ソース {0} を{1}化しました。", item, (enable ? "有効" : "無効")));

				if (item is OtherSourceTimerItemBase)
					(item as OtherSourceTimerItemBase).Update();
			});
		}

		private void FindAt(String arg, Action<OtherSourceItemBase> action)
		{
			Int32 index;
			if (Int32.TryParse(arg, out index))
			{
				if (index < AddIn.Config.Items.Count && index > -1)
				{
					action(AddIn.Config.Items[index]);
				}
				else
				{
					Console.NotifyMessage("存在しないソースが指定されました。");
				}
			}
			else
			{
				Console.NotifyMessage("ソースの指定が正しくありません。");
			}
		}
	}

	public abstract class OtherSourceEditContextBase : Context
	{
		protected OtherSourceAddIn AddIn { get { return CurrentSession.AddInManager.GetAddIn<OtherSourceAddIn>(); } }
		public OtherSourceItemBase Item { get; set; }
		public Boolean IsNew { get; set; }
		public override IConfiguration[] Configurations { get { return new IConfiguration[] { Item }; } }
		public override string ContextName { get { return (IsNew ? "New" : "Edit") + Item.SourceName; } }

		[Description("ソースの設定を保存してコンテキストを終了します")]
		public void Save()
		{
			OnPreSaveConfig();

			// 状態を保存
			if (IsNew) AddIn.Config.Items.Add(Item);
			AddIn.SaveConfig(true);

			OnPostSaveConfig();

			Console.NotifyMessage(String.Format("ソースの設定を{0}しました。", (IsNew ? "新規作成" : "保存")));
			Exit();
		}

		/// <summary>
		/// 設定保存前の処理
		/// </summary>
		protected virtual void OnPreSaveConfig()
		{
		}

		/// <summary>
		/// 設定保存後の処理
		/// </summary>
		protected virtual void OnPostSaveConfig()
		{
		}

		/// <summary>
		/// グループを作成します。
		/// </summary>
		/// <param name="groupName">グループ名</param>
		protected void CreateGroup(String groupName)
		{
			CreateGroup(groupName, AddIn.Config.InitialModes);
		}

		/// <summary>
		/// グループを作成します。
		/// </summary>
		/// <param name="groupName">グループ名</param>
		/// <param name="modes">モード</param>
		protected void CreateGroup(String groupName, String modes)
		{
			if (!String.IsNullOrEmpty(groupName) && !CurrentSession.Groups.ContainsKey(groupName))
			{
				// グループを作成する
				Misuzilla.Applications.TwitterIrcGateway.Group group = new Misuzilla.Applications.TwitterIrcGateway.Group(groupName);
				foreach (ChannelMode mode in ChannelMode.Parse(modes ?? String.Empty))
					group.ChannelModes.Add(mode);
				CurrentSession.Groups.Add(groupName, group);
				CurrentSession.JoinChannel(CurrentSession, group);
				Console.NotifyMessage(String.Format("グループ名 {0} を作成しました。", groupName));
				CurrentSession.SaveGroups();
			}
		}
	}
	#endregion

	public class OtherSourceAddIn : AddInBase
	{
		public const String DefaultSenderNick = "$OtherSource";
		public const String DefaultChannelName = "#OtherSource";

		public OtherSourceConfiguration Config { get; set; }
		private TypableMapCommandProcessor _typableMapCommands = null;
		private DateTime _lastSaveTime = DateTime.Now;

		// TODO: 無理矢理過ぎるのでどうにかしたい
		internal new Server CurrentServer { get { return base.CurrentServer; } }
		internal new Session CurrentSession { get { return base.CurrentSession; } }

		public OtherSourceAddIn()
		{
		}

		public override void Initialize()
		{
			base.Initialize();

			// 設定を読み込みと関連付け
			Config = CurrentSession.AddInManager.GetConfig<OtherSourceConfiguration>();
			foreach (var item in Config.Items)
			{
				item.Initialize(this);
			}

			CurrentSession.UpdateStatusRequestReceived += new EventHandler<StatusUpdateEventArgs>(CurrentSession_UpdateStatusRequestReceived);
			CurrentSession.AddInsLoadCompleted += (sender, e) =>
			{
				// コンテキストを登録
				CurrentSession.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<OtherSourceContext>();

				// TypableMapが使えるかどうか
				var typableMapSupport = CurrentSession.AddInManager.GetAddIn<TypableMapSupport>();
				if (typableMapSupport != null)
					_typableMapCommands = typableMapSupport.TypableMapCommands;
			};
		}

		public override void Uninitialize()
		{
			SaveConfig(true);

			foreach (var item in Config.Items)
			{
				item.Uninitialize();
			}

			base.Uninitialize();
		}

		private void CurrentSession_UpdateStatusRequestReceived(object sender, StatusUpdateEventArgs e)
		{
			String receiver = e.ReceivedMessage.Receiver;
			if (!String.IsNullOrEmpty(receiver))
			{
				foreach (var item in Config.Items)
				{
					if (item is IMessageReceivable)
					{
						var messageReceivable = item as IMessageReceivable;
						var target = messageReceivable.GetChannelName();
						if (String.Compare(target, receiver, true) == 0)
						{
							messageReceivable.MessageReceived(e);
						}
					}
				}
			}
		}

		/// <summary>
		/// 設定を保存します。
		/// </summary>
		/// <param name="force">強制的に書き換えるか</param>
		internal void SaveConfig(Boolean force)
		{
			// 前回の保存から1時間経過していたら保存する
			DateTime now = DateTime.Now;
			TimeSpan span = now - _lastSaveTime;
			if (force || span.TotalHours >= 1)
			{
				CurrentSession.AddInManager.SaveConfig(Config);
				_lastSaveTime = now;
			}
		}

		/// <summary>
		/// TypableMapの情報を付与
		/// </summary>
		internal String ApplyTypableMap(String str, Status status)
		{
			if (Config.EnableTypableMap)
			{
				if (_typableMapCommands != null)
				{
					String typableMapId = _typableMapCommands.TypableMap.Add(status);

					// TypableMapKeyColorNumber = -1 の場合には色がつかなくなる
					if (CurrentSession.Config.TypableMapKeyColorNumber < 0)
						return str + String.Format(" ({0})", typableMapId);
					else
						return str + String.Format(" \x03{0}({1})\x03", CurrentSession.Config.TypableMapKeyColorNumber, typableMapId);
				}
			}

			return str;
		}
	}
}

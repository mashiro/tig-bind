using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;
using System.Threading;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Reflection;
using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap;

namespace Spica.Applications.TwitterIrcGateway.AddIns.Bind
{
	#region Interface
	public interface IBindableNode
	{
		String GetChannelName();
		String GetNodeName();		
		Type GetContextType();
		void OnMessageReceived(StatusUpdateEventArgs e);
	}
	#endregion

	#region Configuration
	[XmlType("Item")]
	public abstract class BindableNodeBase : IConfiguration, IBindableNode
	{
		[Description("ノードを有効化または無効化します")]
		public Boolean Enabled { get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public BindAddIn AddIn { get; private set; }

		public BindableNodeBase()
		{
			Enabled = true;
		}

		public virtual void Initialize(BindAddIn addIn)
		{
			AddIn = addIn;
		}

		public virtual void Uninitialize()
		{
		}

		public abstract String GetChannelName();
		public abstract String GetNodeName();
		public abstract Type GetContextType();
		public abstract void OnMessageReceived(StatusUpdateEventArgs e);

		public string ToShortString()
		{
			// Feed - http://example.com (3600)
			// Lists - UserId/ListId (60)
			return String.Format("{0} - {1}", GetNodeName(), ToString());
		}

		public string ToLongString()
		{
			// [*] Feed - http://example.com (3600)
			// [ ] Lists - UserId/ListId (60)
			return String.Format("[{0}] {1}", Enabled ? "*" : " ", ToShortString());
		}

		/// <summary>
		/// バインドされたチャンネルにメッセージを送信します。
		/// </summary>
		protected void SendMessage(String sender, String content, Boolean notice)
		{
			var receiver = GetChannelName();
			if (String.IsNullOrEmpty(receiver))
				receiver = AddIn.CurrentSession.CurrentNick;

			foreach (String line in BindUtility.SplitLineBreak(content))
			{
				IRCMessage message = null;
				if (notice)
					message = new NoticeMessage(receiver, line);
				else
					message = new PrivMsgMessage(receiver, line);

				message.SenderNick = sender;
				message.SenderHost = String.Format("{0}@{1}", GetNodeName().ToLowerInvariant(), Server.ServerName);
				AddIn.CurrentSession.Send(message);
			}
		}

		/// <summary>
		/// バインドされたチャンネルに例外メッセージを送信します。
		/// </summary>
		protected void SendException(Exception exception)
		{
			if (!AddIn.Config.IgnoreWatchError)
			{
				SendMessage(BindAddIn.DefaultSenderNick, BindUtility.RemoveLineBreak(exception.Message), true);
			}
		}
	}

	public abstract class BindableTimerNodeBase : BindableNodeBase
	{
		[Description("ノードをチェックする間隔を秒単位で指定します")]
		public Int32 Interval { get; set; }

		private static readonly Random _random = new Random();
		private Timer _timer = null;
		private Object _timerSync = new object();

		public BindableTimerNodeBase()
		{
			Interval = 90;
		}

		public override void Initialize(BindAddIn addIn)
		{
			base.Initialize(addIn);
			Update();
		}

		public override void Uninitialize()
		{
			Stop();
			base.Uninitialize();
		}

		protected virtual Boolean IsValid()
		{
			return Enabled;
		}

		#region Timer
		/// <summary>
		/// タイマーの状態を更新します。
		/// </summary>
		public void Update()
		{
			if (IsValid())
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

	public class BindConfiguration : IConfiguration
	{
		[Description("チャンネル作成時のモードを指定します")]
		public String InitialModes { get; set; }

		[Description("エラーを無視するかどうかを指定します")]
		public Boolean IgnoreWatchError { get; set; }

		[Browsable(false)]
		public List<BindableNodeBase> Items { get; set; }

		public BindConfiguration()
		{
			InitialModes = "+pni";
			Items = new List<BindableNodeBase>();
		}
	}
	#endregion

	#region Context
	[Description("バインドするノードの設定を行うコンテキストに切り替えます")]
	public class BindContext : Context
	{
		private BindAddIn AddIn { get { return CurrentSession.AddInManager.GetAddIn<BindAddIn>(); } }

		public override IConfiguration[] Configurations { get { return new IConfiguration[] { AddIn.Config }; } }
		protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
		{
			if (config is BindConfiguration)
			{
				AddIn.Config = config as BindConfiguration;
				AddIn.SaveConfig();
			}
		}

		[Description("指定したノードを強制的に更新します")]
		public void UpdateForce(String arg)
		{
			FindAt(arg, item =>
			{
				if (item is BindableTimerNodeBase)
				{
					var timerItem = item as BindableTimerNodeBase;
					timerItem.Force();
				}

				Console.NotifyMessage(String.Format("ノード {0} を更新しました。", item.ToShortString()));
			});
		}

		[Description("存在するノードをすべて表示します")]
		public void List()
		{
			if (AddIn.Config.Items.Count == 0)
			{
				Console.NotifyMessage("ノードは現在設定されていません。");
				return;
			}

			for (Int32 i = 0; i < AddIn.Config.Items.Count; ++i)
			{
				var item = AddIn.Config.Items[i];
				Console.NotifyMessage(String.Format("{0}: {1}", i, item.ToLongString()));
			}
		}

		[Description("指定したノードを有効化します")]
		public void Enable(String arg)
		{
			SwitchEnable(arg, true);
		}

		[Description("指定したノードを無効化します")]
		public void Disable(String arg)
		{
			SwitchEnable(arg, false);
		}

		[Description("指定したノードを削除します")]
		public void Remove(String arg)
		{
			FindAt(arg, item =>
			{
				item.Uninitialize();
				AddIn.Config.Items.Remove(item);
				AddIn.SaveConfig();
				Console.NotifyMessage(String.Format("ノード {0} を削除しました。", item.ToShortString()));
			});
		}

		[Description("指定したノードを編集します")]
		public void Edit(String arg)
		{
			FindAt(arg, item =>
			{
				// コンテキストを作成
				BindEditContextBase context = Console.GetContext(item.GetContextType(), CurrentServer, CurrentSession) as BindEditContextBase;
				context.Item = item;
				context.IsNew = false;
				Console.PushContext(context);
			});
		}

		[Description("ノードを新規追加します")]
		public void New(String nodeName)
		{
			Type tunnelType;
			if (!AddIn.BindableNodes.TryGetValue(nodeName, out tunnelType))
			{
				String nodeNames = String.Join("|", AddIn.BindableNodes.Select(d => d.Key).ToArray());
				Console.NotifyMessage("不明なノードの種類が指定されました。");
				Console.NotifyMessage(String.Format("これらのノードが指定可能です。 [{0}]", nodeNames));
				return;
			}

			// ノードを作成
			BindableNodeBase item = Activator.CreateInstance(tunnelType) as BindableNodeBase;
			item.Initialize(AddIn);

			// コンテキストを作成
			BindEditContextBase context = Console.GetContext(item.GetContextType(), CurrentServer, CurrentSession) as BindEditContextBase;
			context.Item = item;
			context.IsNew = true;
			Console.PushContext(context);
		}

		private void SwitchEnable(String arg, Boolean enable)
		{
			FindAt(arg, item =>
			{
				item.Enabled = enable;

				if (item is BindableTimerNodeBase)
				{
					var timerItem = item as BindableTimerNodeBase;
					timerItem.Update();
				}

				AddIn.SaveConfig();
				Console.NotifyMessage(String.Format("ノード {0} を{1}化しました。", item.ToShortString(), (enable ? "有効" : "無効")));
			});
		}

		private void FindAt(String arg, Action<BindableNodeBase> action)
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
					Console.NotifyMessage("存在しないノードが指定されました。");
				}
			}
			else
			{
				Console.NotifyMessage("ノードの指定が正しくありません。");
			}
		}
	}

	public abstract class BindEditContextBase : Context
	{
		protected BindAddIn AddIn { get { return CurrentSession.AddInManager.GetAddIn<BindAddIn>(); } }
		public BindableNodeBase Item { get; set; }
		public Boolean IsNew { get; set; }
		public override IConfiguration[] Configurations { get { return new IConfiguration[] { Item }; } }
		public override string ContextName { get { return (IsNew ? "New" : "Edit") + Item.GetNodeName(); } }

		[Description("ノードの設定を保存してコンテキストを終了します")]
		public void Save()
		{
			OnPreSaveConfig();

			// 状態を保存
			if (IsNew) AddIn.Config.Items.Add(Item);
			AddIn.SaveConfig();

			OnPostSaveConfig();

			Console.NotifyMessage(String.Format("ノードの設定を{0}しました。", (IsNew ? "新規作成" : "保存")));
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

	public class BindAddIn : AddInBase
	{
		public const String DefaultSenderNick = "$Bind";
		public const String DefaultChannelName = "#Bind";

		public BindConfiguration Config { get; set; }
		public Dictionary<String, Type> BindableNodes { get; set; }
		private TypableMapCommandProcessor _typableMapCommands = null;

		// TODO: 無理矢理過ぎるのでどうにかしたい
		internal new Server CurrentServer { get { return base.CurrentServer; } }
		internal new Session CurrentSession { get { return base.CurrentSession; } }

		internal Boolean EnableTypableMap { get { return CurrentSession.Config.EnableTypableMap; } }

		public BindAddIn()
		{
			BindableNodes = new Dictionary<String, Type>(StringComparer.OrdinalIgnoreCase);
		}

		public override void Initialize()
		{
			base.Initialize();

			// アセンブリからノードを読み込む
			LoadBindableNodes();

			// 設定を読み込みと関連付け
			Config = CurrentSession.AddInManager.GetConfig<BindConfiguration>();
			foreach (var item in Config.Items)
			{
				item.Initialize(this);
			}

			CurrentSession.PreMessageReceived += new EventHandler<MessageReceivedEventArgs>(CurrentSession_PreMessageReceived);
			CurrentSession.AddInsLoadCompleted += (sender, e) =>
			{
				// コンテキストを登録
				CurrentSession.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<BindContext>();

				// TypableMapが使えるかどうか
				var typableMapSupport = CurrentSession.AddInManager.GetAddIn<TypableMapSupport>();
				if (typableMapSupport != null)
					_typableMapCommands = typableMapSupport.TypableMapCommands;
			};
		}

		public override void Uninitialize()
		{
			SaveConfig();

			foreach (var item in Config.Items)
			{
				item.Uninitialize();
			}

			base.Uninitialize();
		}

		/// <summary>
		/// IBindableNode を継承した型を読み込む
		/// </summary>
		private void LoadBindableNodes()
		{
			Assembly asm = Assembly.GetExecutingAssembly();
			Type nodeType = typeof(IBindableNode);
			foreach (Type type in asm.GetTypes())
			{
				if (nodeType.IsAssignableFrom(type) && !type.IsAbstract && type.IsClass)
				{
					// いちいちインスタンス化してるのがちょっとあれ
					IBindableNode node = Activator.CreateInstance(type) as IBindableNode;
					BindableNodes[node.GetNodeName()] = type;
				}
			}
		}

		/// <summary>
		/// バインドされたチャンネル宛のメッセージだったら奪い取る
		/// </summary>
		private void CurrentSession_PreMessageReceived(object sender, MessageReceivedEventArgs e)
		{
			PrivMsgMessage message = e.Message as PrivMsgMessage;
			if (message == null)
				return;

			StatusUpdateEventArgs eventArgs = new StatusUpdateEventArgs(message, message.Content);
			String receiver = message.Receiver;
			if (!String.IsNullOrEmpty(receiver))
			{
				foreach (var item in Config.Items)
				{
					var bindable = item as IBindableNode;
					if (bindable != null)
					{
						var target = bindable.GetChannelName();
						if (String.Compare(target, receiver, true) == 0)
						{
							bindable.OnMessageReceived(eventArgs);
						}
					}
				}
			}

			e.Cancel |= eventArgs.Cancel;
		}

		/// <summary>
		/// 設定を保存します。
		/// </summary>
		internal void SaveConfig()
		{
			CurrentSession.AddInManager.SaveConfig(Config);
		}

		/// <summary>
		/// 時間を頭につける
		/// </summary>
		public String ApplyDateTime(String str, DateTime dateTime, Boolean isFirstTime)
		{
			if (isFirstTime)
				return String.Format("{0} {1}", dateTime.ToString("HH:mm"), str);
			else
				return str;
		}

		/// <summary>
		/// Twitter の Status に TypableMap をつける
		/// </summary>
		public String ApplyTypableMap(String str, Status status)
		{
			if (_typableMapCommands != null)
				return ApplyTypableMap(str, status, new TypableMapStatusRepositoryWrapper(_typableMapCommands.TypableMap));
			return str;
		}

		/// <summary>
		/// 任意の型に TypableMap をつける
		/// </summary>
		public String ApplyTypableMap<T>(String str, T value, ITypableMapGenericRepository<T> typableMap)
		{
			if (EnableTypableMap)
			{
				if (typableMap != null)
				{
					String typableMapId = typableMap.Add(value);

					// TypableMapKeyColorNumber = -1 の場合には色がつかなくなる
					if (CurrentSession.Config.TypableMapKeyColorNumber < 0)
						return str + String.Format(" ({0})", typableMapId);
					else
						return str + String.Format(" \x03{0}({1})\x03", CurrentSession.Config.TypableMapKeyColorNumber, typableMapId);
				}
			}

			return str;
		}

		/// <summary>
		/// ClientMessageWait に設定された時間だけ眠らせる
		/// </summary>
		public void SleepClientMessageWait()
		{
			if (CurrentSession.Config.ClientMessageWait > 0)
				Thread.Sleep(CurrentSession.Config.ClientMessageWait);
		}
	}
}

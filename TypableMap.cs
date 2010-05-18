using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Misuzilla.Net.Irc;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap;
using TypableMap;

namespace Spica.Applications.TwitterIrcGateway.AddIns.OtherSource
{
	#region Repository
	public interface ITypableMapGenericRepository<T>
	{
		void SetSize(Int32 size);
		String Add(T value);
		Boolean TryGetValue(String typableMapId, out T value);
	}

	public class TypableMapGenericMemoryRepository<T> : ITypableMapGenericRepository<T>
	{
		private TypableMap<T> _typableMap;

		public TypableMapGenericMemoryRepository(Int32 size)
		{
			_typableMap = new TypableMap<T>(size);
		}

		public void SetSize(int size)
		{
			_typableMap = new TypableMap<T>(size);
		}

		public String Add(T value)
		{
			return _typableMap.Add(value);
		}

		public Boolean TryGetValue(String typableMapId, out T value)
		{
			return _typableMap.TryGetValue(typableMapId, out value);
		}
	}

	public class TypableMapStatusRepositoryWrapper : ITypableMapGenericRepository<Status>
	{
		private ITypableMapStatusRepository _repository;

		public TypableMapStatusRepositoryWrapper(ITypableMapStatusRepository repository)
		{
			_repository = repository;
		}

		public void SetSize(int size)
		{
			_repository.SetSize(size);
		}

		public string Add(Status value)
		{
			return _repository.Add(value);
		}

		public bool TryGetValue(string typableMapId, out Status value)
		{
			return _repository.TryGetValue(typableMapId, out value);
		}
	}
	#endregion

	#region Factory
	public interface ITypableMapGenericRepositoryFactory<T>
	{
		ITypableMapGenericRepository<T> Create(Int32 size);
	}

	public class TypableMapGenericMemoryRepositoryFactory<T> : ITypableMapGenericRepositoryFactory<T>
	{
		public ITypableMapGenericRepository<T> Create(Int32 size)
		{
			return new TypableMapGenericMemoryRepository<T>(size);
		}
	}
	#endregion

	public interface ITypableMapGenericCommand<T>
	{
		String CommandName { get; }
		Boolean Process(TypableMapGenericCommandProcessor<T> processor, PrivMsgMessage msg, T value, String args);
	}

	public class TypableMapGenericCommandProcessor<T>
	{
		private Int32 _keySize;
		private ITypableMapGenericRepositoryFactory<T> _factory;
		private Dictionary<String, ITypableMapGenericCommand<T>> _commands;
		private Regex _regex;

		public Session Session { get; private set; }
		public ITypableMapGenericRepository<T> TypableMap { get; private set; }
		public Int32 TypableMapKeySize
		{
			get
			{
				return _keySize;
			}
			set
			{
				if (value < 1)
					value = 1;

				if (_keySize != value)
				{
					_keySize = value;
					TypableMap = _factory.Create(_keySize);
				}
			}
		}

		public TypableMapGenericCommandProcessor(ITypableMapGenericRepositoryFactory<T> factory, Session session, Int32 keySize)
		{
			Session = session;

			_keySize = keySize;
			_commands = new Dictionary<String, ITypableMapGenericCommand<T>>(StringComparer.InvariantCultureIgnoreCase);

			_factory = factory;
			TypableMap = factory.Create(keySize);
		}

		private void UpdateRegex()
		{
			List<String> keys = new List<string>();
			foreach (var key in _commands.Keys)
				keys.Add(Regex.Escape(key));

			_regex = new Regex(@"^\s*(?<cmd>" + (String.Join("|", keys.ToArray())) + @")\s+(?<tid>[^\s]+)(\s*|\s+(?<args>.*))$", RegexOptions.IgnoreCase);
		}

		public ITypableMapGenericCommand<T> AddCommand(ITypableMapGenericCommand<T> command)
		{
			_commands[command.CommandName] = command;
			UpdateRegex();
			return command;
		}

		public Boolean RemoveCommand(ITypableMapGenericCommand<T> command)
		{
			return RemoveCommand(command.CommandName);
		}

		public Boolean RemoveCommand(String command)
		{
			Boolean retVal = _commands.Remove(command);
			if (_commands.Count != 0)
			{
				UpdateRegex();
			}
			return retVal;
		}

		public Boolean Process(PrivMsgMessage message)
		{
			if (_commands.Count == 0)
				return false;

			Match match = _regex.Match(message.Content);
			if (match.Success)
			{
				T value;
				if (TypableMap.TryGetValue(match.Groups["tid"].Value, out value))
				{
					ITypableMapGenericCommand<T> command = _commands[match.Groups["cmd"].Value];
					command.Process(this, message, value, match.Groups["args"].Value);
				}
				else
				{
					Session.SendServer(new NoticeMessage
					{
						Receiver = message.Receiver,
						Content = "エラー: 指定された TypableMap の ID は存在しません。"
					});
				}

				return true; // 握りつぶす
			}

			return false;
		}
	}
}

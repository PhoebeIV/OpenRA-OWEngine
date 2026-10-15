#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using OpenRA.Network;

namespace OpenRA.Server
{
	sealed class Program
	{
		static void Main(string[] args)
		{
			try
			{
				Run(args);
			}
			catch (Exception e)
			{
				ExceptionHandler.HandleFatalError(e);

				// Flush logs before rethrowing, i.e. allowing the exception to go unhandled.
				// try-finally won't work - an unhandled exception kills our process without running the finally block!
				Log.Dispose();
				throw;
			}
			finally
			{
				Log.Dispose();
			}
		}

		static void Run(string[] args)
		{
			// Parse the friendly aliases: --port, --name, --map and --conf.
			// Everything else is passed through as a standard Key=Value argument.
			var aliases = new Dictionary<string, string>();
			var otherArgs = new List<string>();
			for (var i = 0; i < args.Length; i++)
			{
				var arg = args[i];
				if (!arg.StartsWith("--", StringComparison.Ordinal))
				{
					otherArgs.Add(arg);
					continue;
				}

				var key = arg.Contains('=') ? arg[..arg.IndexOf('=')] : arg;
				var setting = key switch
				{
					"--port" => "Server.ListenPort",
					"--name" => "Server.Name",
					"--map" => "Server.Map",
					"--conf" => "__config",
					_ => null,
				};

				if (setting == null)
				{
					otherArgs.Add(arg);
					continue;
				}

				aliases[setting] = GetArgumentValue(args, ref i, key);
			}

			var arguments = new Arguments(otherArgs.ToArray());

			var engineDirArg = arguments.GetValue("Engine.EngineDir", null);
			if (!string.IsNullOrEmpty(engineDirArg))
				Platform.OverrideEngineDir(engineDirArg);

			var supportDirArg = arguments.GetValue("Engine.SupportDir", null);
			if (!string.IsNullOrEmpty(supportDirArg))
				Platform.OverrideSupportDir(supportDirArg);

			// Load the configuration file (default: server.conf next to the engine dir,
			// overridable with --conf). Command-line args always win over the file.
			var configPath = aliases.GetValueOrDefault("__config") ?? Path.Combine(Platform.EngineDir, "server.conf");
			if (File.Exists(configPath))
			{
				foreach (var rawLine in File.ReadAllLines(configPath))
				{
					var line = rawLine.Trim();
					if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
						continue;

					var separator = line.IndexOf('=');
					if (separator < 0)
						continue;

					var key = NormalizeConfigKey(line[..separator].Trim());
					var value = line[(separator + 1)..].Trim().Trim('"');
					if (arguments.Contains(key))
						continue;

					arguments.ReplaceValue(key, value);
				}
			}

			// Friendly aliases take precedence over the config file and other args.
			foreach (var (key, value) in aliases)
				if (key != "__config")
					arguments.ReplaceValue(key, value);

			Log.AddChannel("debug", "dedicated-debug.log", true);
			Log.AddChannel("perf", "dedicated-perf.log", true);
			Log.AddChannel("server", "dedicated-server.log", true);
			Log.AddChannel("nat", "dedicated-nat.log", true);
			Log.AddChannel("geoip", "dedicated-geoip.log", true);

			// Special case handling of Game.Mod argument: if it matches a real filesystem path
			// then we use this to override the mod search path, and replace it with the mod id
			var modID = arguments.GetValue("Game.Mod", null);
			var explicitModPaths = Array.Empty<string>();
			if (modID != null && (File.Exists(modID) || Directory.Exists(modID)))
			{
				explicitModPaths = [modID];
				modID = Path.GetFileNameWithoutExtension(modID);
			}

			if (modID == null)
				throw new InvalidOperationException("Game.Mod argument missing or mod could not be found.");

			// HACK: The engine code assumes that Game.Settings is set.
			// This isn't nearly as bad as ModData, but is still not very nice.
			Game.InitializeSettings(arguments);
			var settings = Game.Settings.Server;

			Nat.Initialize();

			var envModSearchPaths = Environment.GetEnvironmentVariable("MOD_SEARCH_PATHS");
			var modSearchPaths = !string.IsNullOrWhiteSpace(envModSearchPaths) ?
				FieldLoader.GetValue<ImmutableArray<string>>("MOD_SEARCH_PATHS", envModSearchPaths) :
				[Path.Combine(Platform.EngineDir, "mods")];

			var mods = new InstalledMods(modSearchPaths, explicitModPaths);

			WriteLineWithTimeStamp($"Starting dedicated server for mod: {modID}");
			while (true)
			{
				// HACK: The engine code *still* assumes that Game.ModData is set
				var modData = Game.ModData = new ModData(mods[modID], mods);
				modData.MapCache.LoadPreviewImages = false; // PERF: Server doesn't need previews, save memory by not loading them.
				modData.MapCache.LoadMaps(modData);

				// Resolve --map to a UID if the user passed a file name or map title.
				if (!string.IsNullOrEmpty(settings.Map))
					settings.Map = ResolveMap(modData, settings.Map);
				var endpoints = new List<IPEndPoint> { new(IPAddress.IPv6Any, settings.ListenPort), new(IPAddress.Any, settings.ListenPort) };
				var server = new Server(endpoints, settings, modData, ServerType.Dedicated);

				GC.Collect();
				while (true)
				{
					Thread.Sleep(1000);
					if (server.State == ServerState.GameStarted && server.Conns.Count < 1)
					{
						WriteLineWithTimeStamp("No one is playing, shutting down...");
						server.Shutdown();
						break;
					}
				}

				modData.Dispose();
				WriteLineWithTimeStamp("Starting a new server instance...");
			}
		}

		static void WriteLineWithTimeStamp(string line)
		{
			Console.WriteLine($"[{DateTime.Now.ToString(Game.Settings.Server.TimestampFormat, CultureInfo.CurrentCulture)}] {line}");
		}

		static string GetArgumentValue(string[] args, ref int i, string name)
		{
			var prefix = name + "=";
			if (args[i].StartsWith(prefix, StringComparison.Ordinal))
				return args[i][prefix.Length..];

			if (++i >= args.Length)
				throw new InvalidOperationException($"Missing value for {name}.");

			return args[i];
		}

		static string NormalizeConfigKey(string key) => key switch
		{
			"Name" => "Server.Name",
			"ListenPort" => "Server.ListenPort",
			"Map" => "Server.Map",
			"Password" => "Server.Password",
			"AdvertiseOnline" => "Server.AdvertiseOnline",
			"AdvertiseOnLocalNetwork" => "Server.AdvertiseOnLocalNetwork",
			"RecordReplays" => "Server.RecordReplays",
			"RequireAuthentication" => "Server.RequireAuthentication",
			"ProfileIDBlacklist" => "Server.ProfileIDBlacklist",
			"ProfileIDWhitelist" => "Server.ProfileIDWhitelist",
			"EnableSingleplayer" => "Server.EnableSingleplayer",
			"EnableSyncReports" => "Server.EnableSyncReports",
			"EnableGeoIP" => "Server.EnableGeoIP",
			"EnableLintChecks" => "Server.EnableLintChecks",
			"ShareAnonymizedIPs" => "Server.ShareAnonymizedIPs",
			"FloodLimitJoinCooldown" => "Server.FloodLimitJoinCooldown",
			_ => key,
		};

		static string ResolveMap(ModData modData, string map)
		{
			if (modData.MapCache.Any(p => p.Uid == map))
				return map;

			var lower = map.ToLowerInvariant();
			var preview = modData.MapCache.FirstOrDefault(p =>
				p.Title.Equals(map, StringComparison.OrdinalIgnoreCase) ||
				p.Path.Replace('\\', '/').EndsWith(lower, StringComparison.OrdinalIgnoreCase));

			return preview?.Uid ?? map;
		}
	}
}
